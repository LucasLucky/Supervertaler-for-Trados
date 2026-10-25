using System;
using System.Collections.Generic;
using System.IO;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// Reads back a round-tripped DOCX or Markdown bilingual file, joins each
    /// row to the originating Trados segment via the sidecar manifest, and
    /// produces a list of pending diffs.
    ///
    /// The class is pure: it doesn't write to Trados itself. The caller (the
    /// ViewPart) applies confirmed diffs via <c>ProcessSegmentPair</c>.
    /// </summary>
    public class BilingualImporter
    {
        /// <summary>Build the diff list. Caller supplies "current target"
        /// lookups via <paramref name="currentTargetLookup"/> — given a
        /// (paragraphUnitId, segmentId) pair return the current target text,
        /// or <c>null</c> if no such segment exists in the active document.
        /// Lock/confirmation status is supplied via
        /// <paramref name="isWriteable"/> — return false to mark a segment
        /// as Locked.</summary>
        // Pattern that counts only STRUCTURAL tag markers (numbered <tN>
        // / <tN/>) in a proofreader's edit. Semantic formatting markers
        // (<b>, <i>, <u>, <bi>) are deliberately excluded because adding
        // or removing character formatting in the target is harmless from
        // a Trados-QA perspective — it just changes the cosmetic
        // formatting of the translation. Structural tags (field codes,
        // page numbers, custom format pairs, line breaks) are the ones
        // Trados QA enforces; those must round-trip 1:1.
        private static readonly System.Text.RegularExpressions.Regex
            OpeningStructuralMarkerRe = new System.Text.RegularExpressions.Regex(
                @"<(?!/)t\d+\b",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public BilingualImportResult Build(
            string importedFilePath,
            ExportManifest manifest,
            Func<string, string, string> currentTargetLookup,
            Func<string, string, bool> isWriteable,
            Func<string, string, int> currentSourceTagCountLookup = null)
        {
            var ext = Path.GetExtension(importedFilePath).ToLowerInvariant();
            List<ImportedSegment> imported;
            if (ext == ".docx")
                imported = new DocxImporter().Parse(importedFilePath);
            else
                imported = new MarkdownImporter().Parse(importedFilePath);

            var result = new BilingualImportResult { Manifest = manifest };

            // Every manifest entry under its number. A combined export of a
            // merged document has one entry per file for each number Studio
            // repeats - it numbers every file from 1 - so a number is a list.
            // This was a Dictionary from number to one entry, and the last file
            // overwrote the others: a row of the first file was then joined to
            // the same-numbered segment of the last one, and where both sources
            // read the same ("Fig.", "1") its edit was written into the wrong
            // file (found live 24 Oct 2026).
            var manifestByNumber = new Dictionary<string, List<ExportManifestSegment>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var m in manifest.Segments)
            {
                if (!manifestByNumber.TryGetValue(m.Number, out var list))
                    manifestByNumber[m.Number] = list = new List<ExportManifestSegment>();
                list.Add(m);
            }
            bool multiFile = SpansSeveralFiles(manifest);

            foreach (var row in imported)
            {
                List<ExportManifestSegment> candidates;
                if (!manifestByNumber.TryGetValue(row.Number, out candidates))
                {
                    result.Diffs.Add(new ImportSegmentDiff
                    {
                        Number = row.Number,
                        File = multiFile ? NullIfEmpty(row.File) : null,
                        NewTarget = row.TargetText,
                        Kind = ImportChangeKind.SegmentMissing,
                        Detail = "No manifest entry for segment #" + row.Number
                    });
                    continue;
                }

                var m = PickEntry(candidates, row.File, out var why);
                if (m == null)
                {
                    result.Diffs.Add(new ImportSegmentDiff
                    {
                        Number = row.Number,
                        File = NullIfEmpty(row.File),
                        NewTarget = row.TargetText,
                        Kind = ImportChangeKind.FileUnknown,
                        Detail = why
                    });
                    continue;
                }

                var diff = new ImportSegmentDiff
                {
                    Number = row.Number,
                    File = multiFile ? NullIfEmpty(m.SourceFileName) : null,
                    ParagraphUnitId = m.ParagraphUnitId,
                    SegmentId = m.SegmentId,
                    NewTarget = row.TargetText ?? "",
                    Status = row.Status
                };

                // Source-text tamper check, whenever the imported file kept its
                // source column and the manifest recorded a hash for that row.
                //
                // This used to be gated on a `currentSourceLookup` callback being
                // supplied - and the only caller passed null, so the check never
                // ran once. Worse, the gate was meaningless: the comparison below
                // hashes the source AS READ FROM THE FILE and compares it with the
                // manifest, so it never needed a lookup at all. A proofreader could
                // rewrite the source column and re-import would say nothing.
                if (!string.IsNullOrEmpty(row.SourceText) && !string.IsNullOrEmpty(m.SourceHash))
                {
                    var currentHash = BilingualExporter.HashPrefix(row.SourceText);
                    if (!string.Equals(currentHash, m.SourceHash, StringComparison.Ordinal))
                    {
                        diff.Kind = ImportChangeKind.SourceMismatch;
                        diff.Detail = "Source text has been changed in the round-tripped file";
                        result.Diffs.Add(diff);
                        continue;
                    }
                }

                var currentTarget = currentTargetLookup?.Invoke(m.ParagraphUnitId, m.SegmentId);
                if (currentTarget == null)
                {
                    diff.Kind = ImportChangeKind.SegmentMissing;
                    diff.Detail = "Segment not present in the active Trados document";
                    result.Diffs.Add(diff);
                    continue;
                }
                diff.OldTarget = currentTarget;

                if (string.Equals(NormaliseForCompare(currentTarget), NormaliseForCompare(row.TargetText ?? ""),
                        StringComparison.Ordinal))
                {
                    diff.Kind = ImportChangeKind.Unchanged;
                    result.Diffs.Add(diff);
                    continue;
                }

                if (isWriteable != null && !isWriteable(m.ParagraphUnitId, m.SegmentId))
                {
                    diff.Kind = ImportChangeKind.Locked;
                    diff.Detail = "Segment is locked or rejected; needs explicit override";
                    result.Diffs.Add(diff);
                    continue;
                }

                // Structural-tag integrity check. Counts only numbered
                // <tN> markers (NOT semantic <b>/<i>/<u>/<bi>) in both
                // the source and the proofreader's edit, then enforces
                // strict equality:
                //
                // - Semantic formatting tags (bold / italic / underline)
                //   can be freely added or removed in the target — they
                //   only affect cosmetic rendering and don't drive Trados
                //   QA rules.
                // - Structural tags (field codes, page numbers, custom
                //   format pairs, line breaks — anything that didn't get
                //   a friendly name via BilingualTagNamer) MUST round-trip
                //   1:1 with the source. Adding one creates a tag the
                //   target file can't render; removing one drops a tag
                //   Trados expects.
                //
                // The source-side count is supplied by the caller's
                // currentSourceStructuralCountLookup so the importer
                // doesn't need to re-serialise the segment itself.
                if (currentSourceTagCountLookup != null)
                {
                    int sourceStructural = currentSourceTagCountLookup(m.ParagraphUnitId, m.SegmentId);
                    int targetStructural = OpeningStructuralMarkerRe.Matches(diff.NewTarget ?? "").Count;
                    if (sourceStructural != targetStructural)
                    {
                        diff.Kind = ImportChangeKind.TagMismatch;
                        diff.Detail =
                            $"Edit has {targetStructural} structural tag marker(s) (<tN>) " +
                            $"but source has {sourceStructural}; applying would break Trados " +
                            "(structural tags must round-trip exactly – semantic <b>/<i>/<u> " +
                            "can be freely added or removed)";
                        diff.Apply = false; // strict mode default — caller can flip to true to force
                        result.Diffs.Add(diff);
                        continue;
                    }
                }

                diff.Kind = ImportChangeKind.Changed;
                diff.Apply = true; // default: include changed segments in the writeback
                result.Diffs.Add(diff);
            }

            return result;
        }

        private static string NormaliseForCompare(string s)
        {
            if (s == null) return "";
            // Normalise newlines + collapse trailing whitespace per line.
            s = s.Replace("\r\n", "\n");
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();
            return string.Join("\n", lines).Trim();
        }

        /// <summary>
        /// The manifest entry a row stands for, or null with the reason. A number
        /// in one file only is that entry, whatever the row says about its file,
        /// so every export that worked before still does. A number in several
        /// files needs the row's file to match exactly one of them: an export
        /// written before the readers kept the file, or a row whose file cell was
        /// edited, is skipped rather than joined to a segment by guesswork.
        /// </summary>
        internal static ExportManifestSegment PickEntry(List<ExportManifestSegment> candidates, string rowFile,
            out string why)
        {
            why = null;
            if (candidates == null || candidates.Count == 0) { why = "No manifest entry for this segment."; return null; }
            if (candidates.Count == 1) return candidates[0];

            var number = candidates[0].Number;
            if (string.IsNullOrWhiteSpace(rowFile))
            {
                why = "Segment #" + number + " is in " + candidates.Count + " files of this export, and the row "
                    + "does not say which, so it was not imported.";
                return null;
            }

            ExportManifestSegment found = null;
            int matches = 0;
            foreach (var c in candidates)
            {
                if (!SameFile(c.SourceFileName, rowFile)) continue;
                found = c;
                matches++;
            }
            if (matches == 1) return found;

            why = matches == 0
                ? "Segment #" + number + " is in " + candidates.Count + " files of this export, and none of them is \""
                  + rowFile.Trim() + "\", the file the row names, so it was not imported."
                : "Segment #" + number + " appears more than once in \"" + rowFile.Trim()
                  + "\" in this export, so which one the row is cannot be told; it was not imported.";
            return null;
        }

        /// <summary>
        /// File names as the export wrote them, compared without case or
        /// surrounding space, and with or without the ".sdlxliff" Studio adds -
        /// the manifest and the file column both come from the same name, but a
        /// proofreader's tools should not have to keep the suffix intact.
        /// </summary>
        internal static bool SameFile(string a, string b)
        {
            string Norm(string s)
            {
                s = (s ?? "").Trim();
                if (s.EndsWith(".sdlxliff", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(0, s.Length - ".sdlxliff".Length);
                return s;
            }
            var x = Norm(a);
            return x.Length > 0 && string.Equals(x, Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the export came from a document of several files.</summary>
        private static bool SpansSeveralFiles(ExportManifest manifest)
        {
            string first = null;
            foreach (var m in manifest.Segments)
            {
                var f = m.SourceFileName ?? "";
                if (first == null) first = f;
                else if (!string.Equals(first, f, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
