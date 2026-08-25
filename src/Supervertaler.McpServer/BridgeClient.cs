using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supervertaler.McpServer;

/// <summary>
/// One live Trados Studio instance, as advertised by its handshake file.
/// </summary>
/// <remarks>
/// Everything past <c>StartedAt</c> is optional: a plugin older than the
/// per-instance handshake (issue #72) writes only the first five fields, and a
/// Studio with no project open legitimately has no project name.
/// </remarks>
public sealed record BridgeInstance(
    int Port,
    string Token,
    int Pid,
    string? StudioVersion,
    string? PluginVersion,
    string? ProjectName,
    string? ActiveFile,
    string? StartedAt,
    string? ProcessName)
{
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>How this instance is named to the user (and to the AI) when
    /// there is more than one and they have to be told apart.</summary>
    public string Label
    {
        get
        {
            var studio = StudioVersion is { Length: > 0 } v ? $"Studio {v}" : "Trados Studio";
            var project = ProjectName is { Length: > 0 } p ? $" (project \"{p}\", PID {Pid})" : $" (no project open, PID {Pid})";
            return studio + project;
        }
    }
}

/// <summary>
/// Which instance a call will go to, and what else was live at the time.
/// </summary>
public sealed record BridgeSelection(BridgeInstance Chosen, IReadOnlyList<BridgeInstance> Live)
{
    /// <summary>More than one Studio is live and nothing has said which to use.
    /// Writes are refused in this state; reads are served from <see cref="Chosen"/>
    /// with a warning. See issue #72.</summary>
    public bool IsAmbiguous => Live.Count > 1;

    /// <summary>The instances that were NOT chosen, for the warning text.</summary>
    public IEnumerable<BridgeInstance> Others => Live.Where(i => i.Pid != Chosen.Pid);
}

/// <summary>
/// Talks to the Supervertaler for Trados bridge (localhost HTTP inside the
/// Trados Studio process). Discovery mirrors the plugin's UserDataPath:
///   1. Resolve the shared user-data root from %APPDATA%\Supervertaler\config.json
///      (key "user_data_path"); fall back to %USERPROFILE%\Supervertaler.
///   2. Enumerate per-instance handshakes in &lt;root&gt;\trados\runtime\instances\
///      (bridge-&lt;pid&gt;.json), falling back to the single shared
///      &lt;root&gt;\trados\runtime\bridge.json when an older plugin wrote no
///      instances folder.
///   3. Drop instances whose process is gone (stale handshakes survive hard kills).
///   4. Send requests to http://127.0.0.1:&lt;port&gt; with the bearer token.
///
/// The handshake is re-read on every call: Trados may start/stop between
/// tool calls, and ports/tokens change per session.
///
/// Two Studio versions can run side by side (Studio 2024 and 2026), each with
/// its own bridge on its own port. When several are live, the newest by
/// StartedAt is chosen — the same instance the old single-file discovery would
/// have landed on, since the last to start was the last to write bridge.json.
/// Deciding what to DO about the ambiguity is the caller's job, not this
/// class's: see the write gate in Program.cs.
/// </summary>
public sealed class BridgeClient
{
    /// <summary>
    /// Exe protocol level, sent to the bridge on every request so the plugin can
    /// tell whether this exe supports the features it needs. NOT the marketing
    /// version: bump only when the exe's own machinery changes (forwarding
    /// semantics, MCP capabilities, discovery/auth). History:
    ///   (no header) = pre-handshake exes (dynamic tools + list_changed or older)
    ///   2 = adds this version header
    ///   3 = per-instance discovery + the read/write gate on ambiguity (#72)
    /// The plugin compares against its RequiredExeVersion and, when this exe is
    /// too old, tells the AI to tell the user to update the extension.
    /// </summary>
    public const int ExeProtocolVersion = 3;

    // Generous on purpose. A write that lands but times out before its
    // confirmation is the worst outcome available: the caller cannot tell it
    // from a failure, and retrying re-applies the edit. Field report: batches of
    // ~45 segment updates exceeded the old 30 s ceiling on a large document,
    // and the writes had all applied. The bridge caps batch sizes itself
    // (SupervertalerBridge.MaxUpdatesPerRequest), so this only has to be longer
    // than the slowest legitimate call, not a safety limit in its own right.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <summary>Set when a custom handshake path is passed via SUPERVERTALER_BRIDGE_FILE.
    /// Pins this exe to exactly one handshake, which is also the manual way to aim two
    /// chat clients at two Studios before the selector work in #72 stage 2 lands.</summary>
    private static string? HandshakeOverride => Environment.GetEnvironmentVariable("SUPERVERTALER_BRIDGE_FILE");

    private sealed record Handshake(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("port")] int Port,
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("startedAt")] string? StartedAt,
        [property: JsonPropertyName("studioVersion")] string? StudioVersion,
        [property: JsonPropertyName("pluginVersion")] string? PluginVersion,
        [property: JsonPropertyName("projectName")] string? ProjectName,
        [property: JsonPropertyName("activeFile")] string? ActiveFile,
        [property: JsonPropertyName("processName")] string? ProcessName);

    public async Task<string> GetAsync(string path, CancellationToken ct = default)
        => await GetAsync(Resolve().Chosen, path, ct);

    public async Task<string> GetAsync(BridgeInstance target, string path, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, target.BaseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Token);
        req.Headers.Add("X-Supervertaler-Mcp-Exe-Version", ExeProtocolVersion.ToString());
        return await SendAsync(req, ct);
    }

    public async Task<string> PostAsync(string path, object body, CancellationToken ct = default)
        => await PostAsync(Resolve().Chosen, path, body, ct);

    public async Task<string> PostAsync(BridgeInstance target, string path, object body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, target.BaseUrl + path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Token);
        req.Headers.Add("X-Supervertaler-Mcp-Exe-Version", ExeProtocolVersion.ToString());
        return await SendAsync(req, ct);
    }

    private static async Task<string> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new BridgeUnavailableException(
                "Could not reach the Supervertaler bridge inside Trados Studio " +
                $"({ex.Message}). Is Trados Studio running with the Supervertaler plugin enabled?");
        }

        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new BridgeUnavailableException(
                $"Bridge returned HTTP {(int)resp.StatusCode}: {Truncate(text, 500)}");
        }
        return text;
    }

    /// <summary>
    /// Find every live Studio instance and pick the one calls go to.
    /// Throws <see cref="BridgeUnavailableException"/> when none is running.
    /// </summary>
    public BridgeSelection Resolve() => Resolve(ResolveRuntimeDir());

    /// <summary>Resolve against an explicit runtime directory. Test seam.</summary>
    internal BridgeSelection Resolve(string runtimeDir)
    {
        var live = DiscoverLiveInstances(runtimeDir);

        if (live.Count == 0)
        {
            throw new BridgeUnavailableException(
                "Supervertaler bridge handshake file not found. Start Trados Studio, open a project " +
                "in the editor, and make sure the Supervertaler for Trados plugin is installed with " +
                "the bridge enabled (Supervertaler settings > AI Assistant).");
        }

        // Newest first. Ordinal works because StartedAt is round-trip ("o")
        // UTC, which sorts lexicographically; a handshake without one (there
        // should be none) sorts last rather than throwing.
        var chosen = live
            .OrderByDescending(i => i.StartedAt ?? "", StringComparer.Ordinal)
            .First();

        return new BridgeSelection(chosen, live);
    }

    private static List<BridgeInstance> DiscoverLiveInstances(string runtimeDir)
    {
        // An explicit override pins us to one handshake and suppresses the
        // ambiguity machinery entirely: the user has already chosen.
        if (!string.IsNullOrEmpty(HandshakeOverride))
        {
            var pinned = ReadInstance(HandshakeOverride!);
            return pinned != null && IsInstanceAlive(pinned)
                ? new List<BridgeInstance> { pinned }
                : new List<BridgeInstance>();
        }

        var result = new List<BridgeInstance>();
        var seenPids = new HashSet<int>();

        var instancesDir = Path.Combine(runtimeDir, "instances");
        if (Directory.Exists(instancesDir))
        {
            string[] files;
            try { files = Directory.GetFiles(instancesDir, "bridge-*.json"); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files)
            {
                var inst = ReadInstance(file);
                if (inst == null || !IsInstanceAlive(inst)) continue;
                if (seenPids.Add(inst.Pid)) result.Add(inst);
            }
        }

        // Fall back to the shared handshake when the plugin is older than
        // per-instance discovery, or wrote no instance file. Skipped once we
        // already have instances: bridge.json duplicates one of them.
        if (result.Count == 0)
        {
            var shared = ReadInstance(Path.Combine(runtimeDir, "bridge.json"));
            if (shared != null && IsInstanceAlive(shared)) result.Add(shared);
        }

        return result;
    }

    private static BridgeInstance? ReadInstance(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var hs = JsonSerializer.Deserialize<Handshake>(File.ReadAllText(path));
            if (hs == null || hs.Port <= 0 || string.IsNullOrEmpty(hs.Token)) return null;

            return new BridgeInstance(
                hs.Port, hs.Token, hs.Pid,
                hs.StudioVersion, hs.PluginVersion, hs.ProjectName, hs.ActiveFile,
                hs.StartedAt, hs.ProcessName);
        }
        catch
        {
            // A malformed or half-written handshake is indistinguishable from a
            // dead one for our purposes: skip it rather than failing discovery.
            return null;
        }
    }

    private static string ResolveRuntimeDir()
    {
        // Default root matches the plugin's UserDataPath.DefaultRoot (~\Supervertaler);
        // %APPDATA%\Supervertaler\config.json may point elsewhere via "user_data_path".
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, "Supervertaler");

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configPath = Path.Combine(appData, "Supervertaler", "config.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("user_data_path", out var loc)
                    && loc.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(loc.GetString()))
                {
                    root = loc.GetString()!;
                }
            }
            catch
            {
                // Unreadable config – stay on the default root.
            }
        }

        return Path.Combine(root, "trados", "runtime");
    }

    /// <summary>
    /// PID liveness, plus a process-name match where the handshake records one.
    /// Windows reuses PIDs: without the name check a recycled PID resurrects a
    /// dead instance, which here would mean a phantom second Studio blocking
    /// every write. Handshakes from older plugins carry no name and are trusted
    /// on PID alone, exactly as before.
    /// </summary>
    private static bool IsInstanceAlive(BridgeInstance inst)
    {
        if (inst.Pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(inst.Pid);
            if (p.HasExited) return false;
            if (string.IsNullOrEmpty(inst.ProcessName)) return true;
            return string.Equals(p.ProcessName, inst.ProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}

/// <summary>Thrown when the bridge can't be reached; the message is user-facing (shown to the AI).</summary>
public sealed class BridgeUnavailableException : Exception
{
    public BridgeUnavailableException(string message) : base(message) { }
}
