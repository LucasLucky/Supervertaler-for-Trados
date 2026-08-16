using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Supervertaler.Trados.Settings
{
    /// <summary>
    /// Which one-off announcements the user has already been shown.
    ///
    /// Deliberately its OWN file rather than a field in settings.json. That file
    /// is loaded into long-lived objects and written back wholesale from ~29 call
    /// sites; AiAssistantViewPart in particular caches a copy at startup - before
    /// the announcement task runs - so the next unrelated save (switching bank,
    /// changing a prompt, sending a chat turn) wrote that stale copy back and
    /// silently dropped the dismissal. The announcement then reappeared on every
    /// single start, no matter how many times the user clicked "Got it".
    ///
    /// This state is write-once, tiny, and never touched by the settings UI, so
    /// keeping it out of the contended file removes the whole class of problem
    /// rather than narrowing the window.
    /// </summary>
    internal static class AnnouncementState
    {
        private static readonly object Sync = new object();

        private static string FilePath =>
            Path.Combine(UserDataPath.TradosSettingsDir, "announcements.json");

        /// <summary>
        /// True if this announcement has already been shown. Also honours the
        /// legacy list in settings.json so anyone who dismissed one under the old
        /// scheme - and had it stick - is not shown it again.
        /// </summary>
        public static bool HasBeenShown(string announcementId)
        {
            if (string.IsNullOrWhiteSpace(announcementId)) return false;

            try
            {
                if (Read().Contains(announcementId, StringComparer.Ordinal))
                    return true;
            }
            catch { }

            try
            {
                var legacy = SettingsService.Current?.ShownAnnouncementIds;
                if (legacy != null && legacy.Contains(announcementId, StringComparer.Ordinal))
                    return true;
            }
            catch { }

            return false;
        }

        /// <summary>Records an announcement as shown. Best-effort.</summary>
        public static void MarkShown(string announcementId)
        {
            if (string.IsNullOrWhiteSpace(announcementId)) return;

            lock (Sync)
            {
                try
                {
                    var ids = Read();
                    if (ids.Contains(announcementId, StringComparer.Ordinal)) return;
                    ids.Add(announcementId);

                    Directory.CreateDirectory(UserDataPath.TradosSettingsDir);

                    // One id per line: this file is read by a human at least as
                    // often as by the plugin (working out why a notice did or did
                    // not appear), and a JSON array on one line is not that.
                    File.WriteAllText(FilePath,
                        "[\r\n  " + string.Join(",\r\n  ", ids.Select(Quote)) + "\r\n]\r\n",
                        new UTF8Encoding(false));
                }
                catch
                {
                    // An announcement that cannot record itself is a nuisance,
                    // not a failure worth surfacing.
                }
            }
        }

        private static List<string> Read()
        {
            var result = new List<string>();
            try
            {
                var path = FilePath;
                if (!File.Exists(path)) return result;

                // Small hand-rolled parse: the file is a flat array of strings,
                // and pulling in a serializer for that is not worth the assembly
                // load at startup.
                foreach (var raw in File.ReadAllText(path).Split('\n'))
                {
                    var line = raw.Trim().TrimEnd(',').Trim();
                    if (line.Length < 2 || line[0] != '"') continue;
                    var end = line.LastIndexOf('"');
                    if (end <= 0) continue;
                    var id = line.Substring(1, end - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
                    if (id.Length > 0 && !result.Contains(id, StringComparer.Ordinal))
                        result.Add(id);
                }
            }
            catch { }
            return result;
        }

        private static string Quote(string s) =>
            "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
