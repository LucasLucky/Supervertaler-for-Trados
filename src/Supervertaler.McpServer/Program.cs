using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Supervertaler.McpServer;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Supervertaler MCP Server – stdio MCP server that fronts the localhost HTTP
// bridge hosted by Supervertaler for Trados inside Trados Studio.
//
// The tool list is NOT hard-coded here. It is fetched from the bridge's
// /v1/tools registry (with a disk cache + a bundled fallback), so new tools
// ship in a plugin update with no extension reinstall. This exe is a generic
// forwarder: it advertises whatever the registry says, and forwards each call
// to the bridge path the registry maps it to.
//
// IMPORTANT: stdout belongs to the MCP protocol – all logging goes to stderr.

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var bridge = new BridgeClient();

// --instance <selector> pins this server to one Studio, for a client whose config
// format has no env block. ChatGptMcpSetup writes `args`, so it can set this.
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--instance" or "-i")
    {
        bridge.CommandLineSelector = args[i + 1];
        break;
    }
}

// Lazily loaded, refreshed on each tools/list so a plugin update (new tools)
// is picked up on the next Claude Desktop connection – no reinstall.
List<ToolDef> tools = new();
var loadLock = new SemaphoreSlim(1, 1);

async Task<List<ToolDef>> GetToolsAsync(CancellationToken ct, bool forceRefresh = false)
{
    await loadLock.WaitAsync(ct);
    try
    {
        if (forceRefresh || tools.Count == 0)
            tools = await ToolRegistry.LoadAsync(bridge, ct);
        return tools;
    }
    finally { loadLock.Release(); }
}

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithListToolsHandler(async (ctx, ct) =>
    {
        var defs = await GetToolsAsync(ct, forceRefresh: true);
        var tools = defs.Select(d => new Tool
        {
            Name = d.Name,
            Description = d.Description,
            InputSchema = d.InputSchema,
        }).ToList();

        // The instance tools are the exe's own – no bridge can answer "which
        // bridge?" – so they are appended rather than coming from the registry.
        tools.AddRange(LocalTools.Definitions().Select(d => new Tool
        {
            Name = d.Name,
            Description = d.Description,
            InputSchema = d.Schema,
        }));

        return new ListToolsResult { Tools = tools };
    })
    .WithCallToolHandler(async (ctx, ct) =>
    {
        var name = ctx.Params?.Name ?? "";
        var callArgs = ctx.Params?.Arguments ?? new Dictionary<string, JsonElement>();

        // Answered here, not forwarded: these decide which bridge everything else
        // goes to, so they must work even while the choice is ambiguous.
        if (LocalTools.Handles(name))
        {
            try { return TextResult(LocalTools.Invoke(name, callArgs, bridge)); }
            catch (Exception ex)
            {
                return TextResult(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
            }
        }

        var defs = await GetToolsAsync(ct);
        var def = defs.FirstOrDefault(d => d.Name == name);
        if (def == null)
            return TextResult($"{{\"ok\":false,\"error\":\"unknown tool '{name}'\"}}", isError: true);

        var args = callArgs;
        try
        {
            // Which Studio are we talking to? With two open (e.g. Studio 2024 and
            // 2026 side by side) this is genuinely ambiguous, and the two halves
            // of that are not equally dangerous: a read from the wrong instance is
            // misleading, a write into the wrong instance destroys work. So writes
            // are refused until an instance is selected, and reads go through with
            // a warning naming the instance they came from. See issue #72.
            var selection = bridge.Resolve();

            // isError:false to match the bridge-unavailable path below: the whole
            // value of this refusal is the AI reading it and asking the user which
            // Studio they meant, so it goes back as ordinary tool output.
            if (selection.IsAmbiguous && def.IsWrite(args))
                return TextResult(RefuseAmbiguousWrite(def.Name, selection), isError: false);

            string result = def.Method == "POST"
                ? await bridge.PostAsync(selection.Chosen, def.Path, BuildBody(def, args), ct)
                : await bridge.GetAsync(selection.Chosen, def.Path + BuildQuery(def, args), ct);

            return selection.IsAmbiguous
                ? WarnedResult(AmbiguousReadWarning(selection), result)
                : TextResult(result);
        }
        catch (Exception ex) when (ex is BridgeUnavailableException or HttpRequestException or TaskCanceledException)
        {
            // Return the actionable message as tool output (the SDK hides thrown text).
            return TextResult(JsonSerializer.Serialize(new { ok = false, error = ex.Message }), isError: false);
        }
    });

// Advertise that our tool list can change at runtime, so clients honour the
// tools/list_changed notification the poller below sends.
builder.Services.Configure<McpServerOptions>(o =>
{
    o.Capabilities ??= new ServerCapabilities();
    o.Capabilities.Tools ??= new ToolsCapability();
    o.Capabilities.Tools.ListChanged = true;
});

var host = builder.Build();

// Background watcher: the client asks for the tool list once per connection, so
// if Trados's bridge wasn't up yet at connect time (or a plugin update changes
// the tools mid-session) the client would be stuck on a stale list. Poll the
// bridge and push tools/list_changed whenever the set differs from what we last
// advertised, so the client re-lists on its own – no restart, no reinstall.
_ = Task.Run(async () =>
{
    string lastSig;
    try { lastSig = ToolSignature(await GetToolsAsync(CancellationToken.None)); }
    catch { lastSig = ""; }

    while (true)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20)); } catch { break; }
        try
        {
            var sig = ToolSignature(await GetToolsAsync(CancellationToken.None, forceRefresh: true));
            if (sig != lastSig)
            {
                lastSig = sig;
                var server = host.Services.GetService<IMcpServer>();
                if (server != null)
                {
                    try { await server.SendNotificationAsync("notifications/tools/list_changed", CancellationToken.None); }
                    catch { /* client not connected yet – it'll get the fresh list on connect */ }
                }
            }
        }
        catch { /* keep polling */ }
    }
});

await host.RunAsync();
return;

// ── helpers ─────────────────────────────────────────────────────────────

static CallToolResult TextResult(string text, bool isError = false) => new()
{
    IsError = isError,
    Content = new List<ContentBlock> { new TextContentBlock { Text = text } }
};

// Warning and payload as separate content blocks, so the tool's JSON stays
// parseable — prepending the warning into the string would corrupt it.
static CallToolResult WarnedResult(string warning, string text) => new()
{
    IsError = false,
    Content = new List<ContentBlock>
    {
        new TextContentBlock { Text = warning },
        new TextContentBlock { Text = text },
    }
};

// Deliberately on EVERY ambiguous read, not just the first: the failure this
// guards against is an agent acting confidently on the wrong project's data,
// and a warning issued forty turns ago has been compacted away. Kept to two
// lines because every byte returned is re-sent on every later turn — the exact
// cost session_report exists to measure.
static string AmbiguousReadWarning(BridgeSelection sel) =>
    $"⚠ Multiple Trados instances are live. This result is from {sel.Chosen.Label}. "
    + string.Join("; ", sel.Others.Select(o => o.Label)) + " also running.\n"
    + "Tell the user which instance this describes. Editing is refused until one is chosen — "
    + "ask which project they mean, then call select_trados_instance.";

static string RefuseAmbiguousWrite(string toolName, BridgeSelection sel)
{
    var instances = sel.Live
        .Select(i => new
        {
            studioVersion = i.StudioVersion,
            project = i.ProjectName,
            activeFile = i.ActiveFile,
            pid = i.Pid,
        })
        .ToList();

    return JsonSerializer.Serialize(new
    {
        ok = false,
        error = $"Refusing to run '{toolName}': {sel.Candidates.Count} Trados Studio instances are running "
              + "and nothing says which one to write to. Writing to the wrong one would edit the wrong "
              + "project's document. Ask the user which project they mean, then call "
              + "select_trados_instance with \"2024\", \"2026\", or part of the project name — and run "
              + "this tool again. Closing the other Studio works too.",
        instances,
        note = "Read-only tools still work and report which instance answered.",
    });
}

// Stable fingerprint of the advertised tool set (names + descriptions), so the
// watcher only fires tools/list_changed on a real change.
static string ToolSignature(List<ToolDef> defs) =>
    string.Join("|", defs
        .Select(d => d.Name + "::" + d.Description)
        .OrderBy(s => s, StringComparer.Ordinal));

static string BuildQuery(ToolDef def, IReadOnlyDictionary<string, JsonElement> args)
{
    var parts = new List<string>();
    foreach (var kv in def.FixedQuery)
        if (kv.Value != null) parts.Add(Enc(kv.Key, kv.Value.ToJsonString().Trim('"')));
    foreach (var kv in args)
    {
        var bridgeName = def.ParamMap.TryGetValue(kv.Key, out var mapped) ? mapped : kv.Key;
        var val = ScalarToString(kv.Value);
        if (val != null) parts.Add(Enc(bridgeName, val));
    }
    return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
}

static object BuildBody(ToolDef def, IReadOnlyDictionary<string, JsonElement> args)
{
    var body = new JsonObject();
    foreach (var kv in def.FixedBody)
        body[kv.Key] = kv.Value?.DeepClone();
    foreach (var kv in args)
    {
        var bridgeName = def.ParamMap.TryGetValue(kv.Key, out var mapped) ? mapped : kv.Key;
        body[bridgeName] = JsonNode.Parse(kv.Value.GetRawText());
    }
    return body;
}

static string Enc(string k, string v) => $"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}";

static string? ScalarToString(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.String => e.GetString(),
    JsonValueKind.Number => e.GetRawText(),
    JsonValueKind.True => "true",
    JsonValueKind.False => "false",
    JsonValueKind.Null or JsonValueKind.Undefined => null,
    _ => e.GetRawText(),   // arrays/objects rarely used as query args
};
