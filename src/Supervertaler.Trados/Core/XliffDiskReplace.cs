using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// SuperSearch's Replace All for SDLXLIFF files that are not open in the
    /// editor: the edit is made in the file on disk, where Ctrl+Z cannot undo it.
    /// So, for every file:
    ///
    ///   - a file open in the editor is never written: Studio holds its own copy
    ///     and would save over the edit, or the edit over the user's work;
    ///   - a segment is changed only if it still reads exactly as the search
    ///     found it, and only that segment: a result whose segment is gone is
    ///     refused, never widened to the whole paragraph;
    ///   - the file as it was is copied to a backup folder first, and nothing
    ///     is written if that copy fails;
    ///   - the new file is written in one step (<see cref="AtomicFile"/>), so an
    ///     interrupted write leaves the old file, never half of the new one;
    ///   - a file that changed on disk while being edited is not written.
    ///
    /// Until 203 the file was loaded and saved straight over itself, with none
    /// of these checks.
    /// </summary>
    internal static class XliffDiskReplace
    {
        /// <summary>Backup runs kept; older ones are deleted when a new one starts.</summary>
        internal const int BackupRunsKept = 10;

        internal sealed class FileReport
        {
            public string FilePath;
            /// <summary>Segments changed in the written file (0 if it was not written).</summary>
            public int Replaced;
            /// <summary>Matches that run across a tag or a tracked change: left alone.</summary>
            public int SpansMarkup;
            /// <summary>Segments that no longer read as the search found them, or are gone.</summary>
            public int ChangedSinceSearch;
            /// <summary>Why the file was not written, when it had changes to write; else null.</summary>
            public string NotWritten;
        }

        internal sealed class RunReport
        {
            public readonly List<FileReport> Files = new List<FileReport>();
            /// <summary>The folder this run's backups went to; null if nothing was backed up.</summary>
            public string BackupDir;
        }

        /// <summary>
        /// Replaces in each file of <paramref name="files"/> (results grouped by
        /// file path). <paramref name="openInEditor"/> holds the full paths of the
        /// files open in Studio; null means they could not be told, and then no
        /// file is written. Backups go to a new folder under
        /// <paramref name="backupRoot"/>. A result's TargetText is updated only
        /// once its file has been written.
        /// </summary>
        internal static RunReport Run(IEnumerable<IGrouping<string, SearchResult>> files,
            ICollection<string> openInEditor, Func<string, string> perform,
            string backupRoot, DateTime now)
        {
            var report = new RunReport();
            var backup = new BackupRun(backupRoot, now);

            foreach (var group in files)
            {
                var file = new FileReport { FilePath = group.Key };
                report.Files.Add(file);

                if (openInEditor == null)
                    file.NotWritten = "could not tell which files are open in the editor";
                else if (IsOpen(group.Key, openInEditor))
                    file.NotWritten = "open in the editor";
                else
                    ReplaceInFile(file, group.ToList(), perform, backup);
            }

            report.BackupDir = backup.Dir;
            return report;
        }

        internal static bool IsOpen(string path, ICollection<string> openInEditor)
        {
            var full = FullPath(path);
            return openInEditor.Any(p => string.Equals(FullPath(p), full, StringComparison.OrdinalIgnoreCase));
        }

        private static string FullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private static void ReplaceInFile(FileReport file, List<SearchResult> results,
            Func<string, string> perform, BackupRun backup)
        {
            var path = file.FilePath;
            var pending = new List<KeyValuePair<SearchResult, string>>();
            try
            {
                var before = new FileInfo(path);
                var stamp = Stamp(before);
                var original = AtomicFile.ReadAllBytes(path);

                var doc = new XmlDocument { PreserveWhitespace = true };
                using (var ms = new MemoryStream(original))
                    doc.Load(ms);

                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                var xliffNs = doc.DocumentElement?.NamespaceURI ?? "";
                if (!string.IsNullOrEmpty(xliffNs))
                    nsMgr.AddNamespace("x", xliffNs);
                var prefix = string.IsNullOrEmpty(xliffNs) ? "" : "x:";

                foreach (var result in results)
                {
                    var node = FindSegment(doc, result, prefix, nsMgr);

                    // The text the search found, compared the way it was read
                    // (XliffSearcher trims it). Anything else means the file
                    // changed since: the user has to search again.
                    if (node == null || !string.Equals(XliffSearcher.AcceptedText(node).Trim(),
                            (result.TargetText ?? "").Trim(), StringComparison.Ordinal))
                    {
                        file.ChangedSinceSearch++;
                        continue;
                    }

                    var outcome = SuperSearchController.ReplaceInSegmentNode(node, perform, out var newText);
                    if (outcome == TargetTextReplace.Outcome.Replaceable)
                        pending.Add(new KeyValuePair<SearchResult, string>(result, newText));
                    else if (outcome == TargetTextReplace.Outcome.SpansMarkup)
                        file.SpansMarkup++;
                }

                if (pending.Count == 0) return;

                byte[] updated;
                using (var ms = new MemoryStream())
                {
                    doc.Save(ms);
                    updated = ms.ToArray();
                }

                var why = backup.Save(path, original);
                if (why != null) { file.NotWritten = "no backup copy could be made: " + why; return; }

                if (Stamp(new FileInfo(path)) != stamp)
                {
                    file.NotWritten = "the file changed on disk while it was being edited";
                    return;
                }

                string failure = null;
                if (AtomicFile.Write(path, updated, replaceExisting: true, log: m => failure = m)
                    != AtomicFile.Outcome.Written)
                {
                    file.NotWritten = failure ?? "the file could not be written";
                    return;
                }

                foreach (var p in pending) p.Key.TargetText = p.Value;
                file.Replaced = pending.Count;
            }
            catch (Exception ex)
            {
                file.NotWritten = ex.Message;
            }
        }

        /// <summary>
        /// The target node to edit for <paramref name="result"/>: its segment's
        /// <c>mrk</c>, or the whole target only when the target has no segment
        /// markers at all (a genuinely unsegmented unit). Null when the segment
        /// cannot be found: until 203 that fell back to the whole target, so a
        /// stale result edited every segment of the paragraph.
        /// </summary>
        internal static XmlNode FindSegment(XmlDocument doc, SearchResult result,
            string prefix, XmlNamespaceManager nsMgr)
        {
            var unit = doc.SelectSingleNode($"//{prefix}trans-unit[@id='{result.ParagraphUnitId}']", nsMgr);
            var target = unit?.SelectSingleNode($"{prefix}target", nsMgr);
            if (target == null) return null;

            if (!string.IsNullOrEmpty(result.SegmentId))
            {
                var seg = target.SelectSingleNode(
                    $".//{prefix}mrk[@mtype='seg'][@mid='{result.SegmentId}']", nsMgr);
                if (seg != null) return seg;
            }

            var segmented = target.SelectSingleNode($".//{prefix}mrk[@mtype='seg']", nsMgr) != null;
            return segmented ? null : target;
        }

        private static string Stamp(FileInfo f)
        {
            f.Refresh();
            return f.Exists
                ? f.Length.ToString(CultureInfo.InvariantCulture) + "|" + f.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                : "missing";
        }

        /// <summary>
        /// One Replace All's backup folder, made on the first file it needs and
        /// named by the run's time. Each file goes under its own folder's name
        /// (the target language, in a Studio project), and restore.txt says where
        /// each copy came from.
        /// </summary>
        private sealed class BackupRun
        {
            private readonly string _root;
            private readonly DateTime _now;
            public string Dir { get; private set; }

            public BackupRun(string root, DateTime now) { _root = root; _now = now; }

            /// <summary>Copies <paramref name="bytes"/> in as <paramref name="original"/>'s backup. Null, or why not.</summary>
            public string Save(string original, byte[] bytes)
            {
                try
                {
                    if (Dir == null) Start();

                    var sub = Path.GetFileName(Path.GetDirectoryName(original)) ?? "";
                    var name = Path.GetFileName(original);
                    var dest = Path.Combine(Dir, sub, name);
                    for (int n = 2; File.Exists(dest); n++)
                        dest = Path.Combine(Dir, sub, Path.GetFileNameWithoutExtension(name) + " (" + n + ")" + Path.GetExtension(name));

                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.WriteAllBytes(dest, bytes);
                    if (!File.ReadAllBytes(dest).SequenceEqual(bytes))
                        return "the copy did not read back the same";

                    File.AppendAllText(Path.Combine(Dir, "restore.txt"),
                        dest.Substring(Dir.Length + 1) + Environment.NewLine
                        + "    was " + original + Environment.NewLine, Encoding.UTF8);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            private void Start()
            {
                Directory.CreateDirectory(_root);
                Prune(_root, BackupRunsKept - 1);

                var stamp = _now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
                var dir = Path.Combine(_root, stamp);
                for (int n = 2; Directory.Exists(dir); n++)
                    dir = Path.Combine(_root, stamp + "_" + n);
                Directory.CreateDirectory(dir);

                File.WriteAllText(Path.Combine(dir, "restore.txt"),
                    "SuperSearch Replace All, " + _now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Environment.NewLine
                    + "These are the files as they were before it changed them. To undo, close the file in" + Environment.NewLine
                    + "Trados Studio and copy the backup back over the original (the path under each one)." + Environment.NewLine
                    + Environment.NewLine, Encoding.UTF8);
                Dir = dir;
            }
        }

        /// <summary>Deletes all but the <paramref name="keep"/> newest run folders.</summary>
        internal static void Prune(string root, int keep)
        {
            try
            {
                // The names are timestamps, so their order is their age.
                foreach (var old in Directory.GetDirectories(root)
                             .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
                             .Skip(keep))
                {
                    try { Directory.Delete(old, recursive: true); } catch { }
                }
            }
            catch { }
        }
    }
}
