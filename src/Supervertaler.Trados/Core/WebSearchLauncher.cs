using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Opens a set of web searches in the user's own browser — SuperSearch's
    /// "Browser" web-results mode.
    ///
    /// <para>The naive approach, <see cref="Process"/>.Start per URL, scatters
    /// tabs into whatever window happens to be focused. Instead we hand the whole
    /// set to the browser in one launch so it lands in a single new window the
    /// user can close when they are done. Chromium browsers take any number of
    /// URLs after <c>--new-window</c>; Firefox needs <c>-new-window</c> for the
    /// first and <c>-new-tab</c> for the rest. Anything else falls back to
    /// launching each URL through the shell.</para>
    ///
    /// <para>Browser mode is not only a fallback for a missing WebView2 Runtime:
    /// some users will prefer it permanently, because their own browser brings
    /// their ad blocker, their logged-in sessions (ProZ, Juremy, DeepL Pro) and
    /// no Cloudflare challenges.</para>
    /// </summary>
    public static class WebSearchLauncher
    {
        private const string LogCategory = "WebSearch";

        /// <summary>How a browser wants to be handed several URLs at once.</summary>
        private enum LaunchStyle
        {
            /// <summary>Chrome, Edge, Brave, Vivaldi, Opera: --new-window u1 u2 u3</summary>
            Chromium,
            /// <summary>Firefox: -new-window u1 -new-tab u2 -new-tab u3</summary>
            Firefox,
        }

        /// <summary>
        /// Opens every target in one new browser window.
        /// </summary>
        /// <returns>
        /// True if the set was handed to a browser in a single launch. False when
        /// we fell back to opening the URLs individually — the pages still open,
        /// they are just not corralled into their own window.
        /// </returns>
        public static bool OpenAll(IEnumerable<WebSearchTarget> targets)
        {
            var urls = (targets ?? Enumerable.Empty<WebSearchTarget>())
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Url))
                .Select(t => t.Url)
                .ToList();
            return OpenAll(urls);
        }

        /// <summary>Opens every URL in one new browser window.</summary>
        public static bool OpenAll(IList<string> urls)
        {
            if (urls == null || urls.Count == 0) return true;

            string exePath;
            LaunchStyle style;
            if (TryResolveBrowser(out exePath, out style))
            {
                try
                {
                    var args = BuildArguments(urls, style);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = args,
                        UseShellExecute = false,
                    });
                    DiagnosticLog.Log(LogCategory,
                        $"Opened {urls.Count} URL(s) in a new {style} window via {Path.GetFileName(exePath)}");
                    return true;
                }
                catch (Exception ex)
                {
                    // A browser that is installed but refuses to launch (locked
                    // profile, policy restriction) should still get the user their
                    // pages, so fall through to the shell rather than surfacing this.
                    DiagnosticLog.Log(LogCategory,
                        $"Direct launch of {exePath} failed ({ex.GetType().Name}: {ex.Message}); " +
                        "falling back to individual shell opens");
                }
            }

            OpenIndividually(urls);
            return false;
        }

        /// <summary>Opens a single URL in the default browser.</summary>
        public static void OpenOne(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Could not open {url}: {ex.Message}");
            }
        }

        private static void OpenIndividually(IEnumerable<string> urls)
        {
            foreach (var url in urls) OpenOne(url);
        }

        private static string BuildArguments(IList<string> urls, LaunchStyle style)
        {
            // URLs are already percent-encoded by WebSearchUrlBuilder, so they
            // hold no literal spaces — but quote anyway, since a hand-written
            // custom resource template could contain anything.
            Func<string, string> q = u => "\"" + u.Replace("\"", "%22") + "\"";

            if (style == LaunchStyle.Firefox)
            {
                var parts = new List<string> { "-new-window", q(urls[0]) };
                foreach (var url in urls.Skip(1))
                {
                    parts.Add("-new-tab");
                    parts.Add(q(url));
                }
                return string.Join(" ", parts);
            }

            return "--new-window " + string.Join(" ", urls.Select(q));
        }

        /// <summary>
        /// Finds the browser to launch, preferring whichever one the user has set
        /// as default so their sessions and extensions are the ones in play. Only
        /// returns true for browsers we know how to hand a multi-URL window to.
        /// </summary>
        private static bool TryResolveBrowser(out string exePath, out LaunchStyle style)
        {
            exePath = null;
            style = LaunchStyle.Chromium;

            var progId = GetDefaultHttpProgId();
            if (!string.IsNullOrEmpty(progId))
            {
                var fromDefault = GetExeFromProgId(progId);
                if (!string.IsNullOrEmpty(fromDefault) && File.Exists(fromDefault))
                {
                    var known = StyleForExe(fromDefault);
                    if (known.HasValue)
                    {
                        exePath = fromDefault;
                        style = known.Value;
                        return true;
                    }
                    // A default browser we do not know how to batch (Safari, some
                    // niche build): fall through to the shell rather than guessing
                    // at its command line.
                    return false;
                }
            }

            // No usable default — fall back to whichever known browser is installed.
            foreach (var candidate in new[] { "chrome.exe", "msedge.exe", "firefox.exe" })
            {
                var path = GetExeFromAppPaths(candidate);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                var known = StyleForExe(path);
                if (!known.HasValue) continue;
                exePath = path;
                style = known.Value;
                return true;
            }
            return false;
        }

        private static LaunchStyle? StyleForExe(string exePath)
        {
            var name = Path.GetFileName(exePath ?? string.Empty).ToLowerInvariant();
            switch (name)
            {
                case "chrome.exe":
                case "msedge.exe":
                case "brave.exe":
                case "vivaldi.exe":
                case "opera.exe":
                    return LaunchStyle.Chromium;
                case "firefox.exe":
                    return LaunchStyle.Firefox;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The ProgId Windows uses for http:, e.g. "ChromeHTML", "MSEdgeHTM",
        /// "FirefoxURL". UserChoice reflects what the user actually picked, which
        /// the machine-wide association does not.
        /// </summary>
        private static string GetDefaultHttpProgId()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"))
                {
                    return key?.GetValue("ProgId") as string;
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Could not read default browser ProgId: {ex.Message}");
                return null;
            }
        }

        /// <summary>Extracts the executable path from a ProgId's shell open command.</summary>
        private static string GetExeFromProgId(string progId)
        {
            try
            {
                using (var key = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                {
                    var command = key?.GetValue(null) as string;
                    return ExtractExecutable(command);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log(LogCategory, $"Could not resolve ProgId {progId}: {ex.Message}");
                return null;
            }
        }

        private static string GetExeFromAppPaths(string exeName)
        {
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (var key = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName))
                    {
                        var path = key?.GetValue(null) as string;
                        if (!string.IsNullOrWhiteSpace(path)) return path.Trim('"');
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Log(LogCategory, $"App Paths lookup for {exeName} failed: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Pulls the executable out of a registry shell command, which looks like
        /// <c>"C:\...\chrome.exe" -- "%1"</c> — or, occasionally, is unquoted.
        /// </summary>
        private static string ExtractExecutable(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            command = command.Trim();

            if (command[0] == '"')
            {
                var close = command.IndexOf('"', 1);
                return close > 1 ? command.Substring(1, close - 1) : null;
            }

            // Unquoted: the path runs to ".exe" (a bare space split would break on
            // "C:\Program Files\...").
            var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? command.Substring(0, exe + 4) : null;
        }
    }
}
