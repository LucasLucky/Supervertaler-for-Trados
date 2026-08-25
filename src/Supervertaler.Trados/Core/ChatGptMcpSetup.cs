using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// One-click setup of the Supervertaler MCP server for ChatGPT desktop.
    ///
    /// <para>Claude Desktop takes a <c>.mcpb</c> bundle and installs itself.
    /// ChatGPT has no equivalent: its plugin bundles are distributed through
    /// marketplaces, which is more work for the user than the config edit, not
    /// less. So the plugin does the edit instead — fetch the server, drop it
    /// somewhere permanent, and register it in the TOML file ChatGPT desktop
    /// shares with Codex CLI.</para>
    ///
    /// <para>This writes to another application's configuration file, so it
    /// backs the file up first, only ever touches its own named block, and
    /// leaves every other server alone.</para>
    /// </summary>
    public static class ChatGptMcpSetup
    {
        private const string LogCategory = "ChatGptSetup";
        private const string BlockName = "mcp_servers.supervertaler";
        private const string AssetName = "Supervertaler-MCP-Server-exe.zip";
        private const string ExeName = "SupervertalerMcpServer.exe";

        private const string LatestReleaseApi =
            "https://api.github.com/repos/Supervertaler/Supervertaler-for-Trados/releases/latest";

        /// <summary>The config file ChatGPT desktop and Codex CLI share.</summary>
        public static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "config.toml");

        /// <summary>
        /// Where the server is kept. Under the user's own data folder rather
        /// than the plugin directory, because a plugin update wipes and
        /// re-extracts that folder — which would break the path ChatGPT stores.
        /// </summary>
        public static string ServerDir => Path.Combine(UserDataPath.TradosDir, "mcp");

        public static string ServerExePath => Path.Combine(ServerDir, ExeName);

        /// <summary>
        /// True when ChatGPT desktop appears to be installed. On Windows it
        /// ships as the Store package <c>OpenAI.Codex</c>, so the usual
        /// Program Files check finds nothing; the package folder under
        /// LocalAppData is the reliable marker. An existing Codex config counts
        /// too, since the CLI and the IDE extension share it.
        /// </summary>
        public static bool IsChatGptInstalled()
        {
            try
            {
                var packages = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages");
                if (Directory.Exists(packages) &&
                    Directory.GetDirectories(packages, "OpenAI.Codex*").Length > 0)
                    return true;

                return File.Exists(ConfigPath);
            }
            catch { return false; }
        }

        /// <summary>True when the config already registers our server.</summary>
        public static bool IsConfigured()
        {
            try
            {
                return File.Exists(ConfigPath)
                    && File.ReadAllText(ConfigPath).Contains("[" + BlockName + "]");
            }
            catch { return false; }
        }

        /// <summary>What a run did, for reporting back to the user.</summary>
        public class Result
        {
            public bool Success;
            public string Message;
            public string BackupPath;
            public bool Downloaded;
            /// <summary>True when an existing server was replaced rather than first installed.</summary>
            public bool Updated;
        }

        // ── Server version ───────────────────────────────────────────────
        //
        // The plugin and the server exe ship from the same release and share a
        // version TAIL: plugin 18.20.184 (Studio 2024) / 19.20.184 (Studio 2026),
        // exe stamped 1.20.184 by tools/build_mcpb.py. Only the tail is
        // comparable — the major encodes the Studio generation on the plugin and
        // the bundle scheme on the exe, so comparing whole versions would say the
        // exe is permanently behind. Exes built before the stamp exist in the wild reporting
        // 1.0.0.0, which reads as "older than anything" and so offers an update.

        /// <summary>Version of the server exe on disk, or null when absent/unreadable.</summary>
        public static Version InstalledServerVersion
        {
            get
            {
                try
                {
                    if (!File.Exists(ServerExePath)) return null;
                    var raw = FileVersionInfo.GetVersionInfo(ServerExePath).FileVersion;
                    return Version.TryParse(raw, out var v) ? v : null;
                }
                catch { return null; }
            }
        }

        /// <summary>Minor.Build of a version — the part the plugin and the exe share.</summary>
        private static Version Tail(Version v)
            => v == null ? null : new Version(Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

        /// <summary>
        /// True when a server is installed but predates this plugin build. Never
        /// true when nothing is installed — that is "absent", handled separately.
        /// </summary>
        public static bool IsServerOutdated()
        {
            var installed = Tail(InstalledServerVersion);
            if (installed == null) return false;
            var plugin = Tail(Assembly.GetExecutingAssembly().GetName().Version);
            return plugin != null && installed < plugin;
        }

        /// <summary>
        /// Ensures the server exists on disk and is registered with ChatGPT.
        /// Never throws: failures come back on <see cref="Result"/>.
        /// </summary>
        /// <param name="progress">Called with short status lines for the UI.</param>
        public static async Task<Result> RunAsync(Action<string> progress = null)
        {
            var result = new Result();
            void Say(string s) { try { progress?.Invoke(s); } catch { } }

            try
            {
                var hadServer = File.Exists(ServerExePath);
                var outdated = hadServer && IsServerOutdated();

                // Until 20.185 this only ever downloaded when the file was ABSENT,
                // so anyone set up once kept that exe for ever — including past a
                // bump in the exe's own protocol level, which the plugin could
                // detect (ExeOutdated) but not act on.
                if (!hadServer || outdated)
                {
                    Say(hadServer ? "Updating the MCP server…" : "Downloading the MCP server…");
                    var error = await DownloadServerAsync().ConfigureAwait(false);
                    if (error != null)
                    {
                        result.Message = error;
                        return result;
                    }
                    result.Downloaded = true;
                    result.Updated = hadServer;
                }

                if (!File.Exists(ServerExePath))
                {
                    result.Message = "The server could not be downloaded. Check your internet "
                                   + "connection, or install it by hand — see the documentation.";
                    return result;
                }

                Say("Updating the ChatGPT configuration…");
                result.BackupPath = WriteConfig();

                result.Success = true;
                result.Message =
                    (result.Updated
                        ? "The MCP server has been updated and ChatGPT desktop is set up.\r\n\r\n"
                        : "ChatGPT desktop is set up.\r\n\r\n")
                    + "Quit ChatGPT completely — closing the window is not enough, it keeps "
                    + "running in the notification area — then start it again and ask:\r\n\r\n"
                    + "    What Trados project is open?";
                return result;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Setup failed: {ex}");
                result.Message = "Setup failed: " + ex.Message
                    + (result.BackupPath != null
                        ? "\r\n\r\nYour original config was backed up to:\r\n" + result.BackupPath
                        : "");
                return result;
            }
        }

        /// <summary>
        /// Fetch the server and put it in place. Returns null on success, or a
        /// user-facing message explaining what stopped it.
        /// </summary>
        private static async Task<string> DownloadServerAsync()
        {
            var assetUrl = await ResolveAssetUrlAsync().ConfigureAwait(false);
            if (assetUrl == null)
                return "Could not find the MCP server download on the latest release. Check your "
                     + "internet connection, or install it by hand — see the documentation.";

            Directory.CreateDirectory(ServerDir);
            SweepReplacedServers();

            var zipPath = Path.Combine(ServerDir, AssetName);
            await UpdateChecker.DownloadFileAsync(assetUrl, zipPath).ConfigureAwait(false);

            // Unpack beside the target rather than onto it. ExtractToFile truncates
            // before it writes, so extracting straight onto a live server exe turns
            // a failed download into no working server at all.
            var staged = ServerExePath + ".new";
            try
            {
                try { if (File.Exists(staged)) File.Delete(staged); } catch { }

                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var entry = zip.Entries.FirstOrDefault(e =>
                        string.Equals(e.Name, ExeName, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                        return "The downloaded package did not contain " + ExeName + ".";
                    entry.ExtractToFile(staged, overwrite: true);
                }

                return InstallStagedServer(staged);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Download failed: {ex}");
                try { if (File.Exists(staged)) File.Delete(staged); } catch { }
                return "The MCP server could not be downloaded: " + ex.Message;
            }
            finally
            {
                try { File.Delete(zipPath); } catch { /* leaving it is harmless */ }
            }
        }

        /// <summary>
        /// Swap the staged server into place. Returns null on success, or a
        /// user-facing message.
        /// </summary>
        private static string InstallStagedServer(string staged)
        {
            try
            {
                if (!File.Exists(ServerExePath))
                {
                    File.Move(staged, ServerExePath);
                    return null;
                }

                // Windows refuses to DELETE a running executable but is happy to
                // RENAME one. So retire the old server sideways and move the new
                // one into its place: a ChatGPT that is running right now keeps
                // using the renamed file until it restarts, and nothing has to be
                // quit first. Overwriting in place, by contrast, fails outright
                // with "the process cannot access the file" — which is exactly the
                // wall people hit updating the Claude Desktop extension.
                var retired = ServerExePath + ".old";
                try { if (File.Exists(retired)) File.Delete(retired); } catch { }

                File.Move(ServerExePath, retired);
                try
                {
                    File.Move(staged, ServerExePath);
                }
                catch
                {
                    // Put the working server back rather than leaving none.
                    try { File.Move(retired, ServerExePath); } catch { }
                    throw;
                }

                // Fails while the retired server is still running; harmless, and
                // the next run sweeps it.
                try { File.Delete(retired); } catch { }
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                DiagnosticLog.Log(LogCategory, $"Server replace failed: {ex}");
                try { if (File.Exists(staged)) File.Delete(staged); } catch { }
                return "The MCP server file is locked and could not be replaced. Quit ChatGPT from "
                     + "the notification area (closing the window is not enough), then press this "
                     + "button again. Your existing server keeps working in the meantime.";
            }
        }

        /// <summary>Delete servers retired by an earlier update, once nothing holds them.</summary>
        private static void SweepReplacedServers()
        {
            try
            {
                foreach (var leftover in Directory.GetFiles(ServerDir, ExeName + ".old")
                    .Concat(Directory.GetFiles(ServerDir, ExeName + ".new")))
                {
                    try { File.Delete(leftover); } catch { /* still running – next time */ }
                }
            }
            catch { /* best effort */ }
        }

        /// <summary>Finds the exe zip on the latest GitHub release.</summary>
        private static async Task<string> ResolveAssetUrlAsync()
        {
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("Supervertaler-for-Trados");
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    var json = await http.GetStringAsync(LatestReleaseApi).ConfigureAwait(false);

                    // Small, fixed shape — a regex avoids taking a JSON dependency
                    // into the plugin sandbox for one field.
                    var m = Regex.Match(json,
                        "\"browser_download_url\"\\s*:\\s*\"([^\"]*" + Regex.Escape(AssetName) + ")\"");
                    if (m.Success) return m.Groups[1].Value;

                    DiagnosticLog.Log(LogCategory, $"No {AssetName} on the latest release");
                    return null;
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Could not resolve the download URL: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds or replaces our block in config.toml, backing the file up first.
        /// Returns the backup path, or null when there was no file to back up.
        ///
        /// <para>Deliberately text-level rather than a TOML round-trip: parsing
        /// and re-emitting would reformat the whole file and could drop comments
        /// or ordering the user cares about. One named block is a small enough
        /// edit to do exactly, and everything else is passed through untouched.</para>
        /// </summary>
        private static string WriteConfig()
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string existing = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "";

            string backup = null;
            if (File.Exists(ConfigPath))
            {
                backup = ConfigPath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(ConfigPath, backup, overwrite: true);
            }

            var updated = RemoveOurBlock(existing);
            if (updated.Length > 0 && !updated.EndsWith("\n")) updated += "\n";

            var block = new StringBuilder();
            block.Append("\n# Supervertaler for Trados — live connection to the open Studio session.\n");
            block.Append("# Local stdio server; it reaches Trados on this machine only.\n");
            block.Append("[").Append(BlockName).Append("]\n");
            block.Append("type = \"stdio\"\n");
            // Single quotes make this a TOML literal string, so Windows
            // backslashes are taken exactly as written and need no escaping.
            block.Append("command = '").Append(ServerExePath).Append("'\n");
            block.Append("args = []\n");
            block.Append("enabled = true\n");
            block.Append("startup_timeout_sec = 60\n");

            File.WriteAllText(ConfigPath, updated + block, new UTF8Encoding(false));
            DiagnosticLog.Log(LogCategory, $"Registered {ServerExePath} in {ConfigPath}");
            return backup;
        }

        /// <summary>
        /// Strips a previous copy of our block — from its header to the next
        /// top-level table header — so re-running replaces rather than
        /// duplicates. Any comment lines immediately above it go too, since
        /// those are ours.
        /// </summary>
        private static string RemoveOurBlock(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var kept = new System.Collections.Generic.List<string>();
            bool skipping = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();

                if (skipping)
                {
                    // A new table header ends our block.
                    if (trimmed.StartsWith("[")) skipping = false;
                    else continue;
                }

                if (trimmed.StartsWith("[" + BlockName + "]"))
                {
                    skipping = true;
                    // Drop the comment lines we wrote directly above it.
                    while (kept.Count > 0 && kept[kept.Count - 1].TrimStart().StartsWith("#"))
                        kept.RemoveAt(kept.Count - 1);
                    while (kept.Count > 0 && kept[kept.Count - 1].Trim().Length == 0)
                        kept.RemoveAt(kept.Count - 1);
                    continue;
                }

                kept.Add(lines[i]);
            }

            return string.Join("\n", kept.ToArray());
        }
    }
}
