using System.Collections.Generic;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>Per-segment outcome of a bilingual round-trip import.</summary>
    public enum ImportChangeKind
    {
        /// <summary>Imported target equals current target — nothing to do.</summary>
        Unchanged,

        /// <summary>Imported target differs from current target — change pending.</summary>
        Changed,

        /// <summary>Manifest entry exists but no matching Trados segment could
        /// be found (e.g. segment was deleted or the project changed).</summary>
        SegmentMissing,

        /// <summary>Source text in the round-tripped file no longer matches
        /// the original source. Either the proofreader edited the source
        /// (forbidden) or the manifest/file are mismatched.</summary>
        SourceMismatch,

        /// <summary>The Trados segment is locked or already
        /// confirmed; needs explicit user override before overwriting.</summary>
        Locked,

        /// <summary>The proofreader's edit has fewer tag markers than the
        /// live source has tags. Applying it would create a Trados QA
        /// failure (source tags must appear in the target). Skipped by
        /// default; can be force-applied by turning off the strict
        /// tag-integrity check in the Import / Export tab.</summary>
        TagMismatch
    }

    public class ImportSegmentDiff
    {
        /// <summary>The segment number as Trados Studio shows it, e.g. "243"
        /// or "209a" for one half of a split. A string because a split id is
        /// not a number - and the join key for the round trip, so export and
        /// import must agree on its form. See <see cref="SegmentNumber"/>.</summary>
        public string Number { get; set; } = "";
        public string ParagraphUnitId { get; set; }
        public string SegmentId { get; set; }
        public string OldTarget { get; set; }
        public string NewTarget { get; set; }
        public string Status { get; set; }
        public ImportChangeKind Kind { get; set; }
        public string Detail { get; set; }   // e.g. mismatch reason

        /// <summary>Whether the user has opted in to apply this change. Set
        /// by the UI before the writeback pass; the import core only writes
        /// segments where <c>Apply &amp;&amp; Kind == Changed</c>.</summary>
        public bool Apply { get; set; }
    }

    public class BilingualImportResult
    {
        public ExportManifest Manifest { get; set; }
        public List<ImportSegmentDiff> Diffs { get; set; } = new List<ImportSegmentDiff>();

        public int TotalImported => Diffs.Count;
        public int ChangedCount
        {
            get { int n = 0; foreach (var d in Diffs) if (d.Kind == ImportChangeKind.Changed) n++; return n; }
        }
        public int UnchangedCount
        {
            get { int n = 0; foreach (var d in Diffs) if (d.Kind == ImportChangeKind.Unchanged) n++; return n; }
        }
        public int IssueCount
        {
            get
            {
                int n = 0;
                foreach (var d in Diffs)
                    if (d.Kind == ImportChangeKind.SegmentMissing
                        || d.Kind == ImportChangeKind.SourceMismatch
                        || d.Kind == ImportChangeKind.Locked
                        || d.Kind == ImportChangeKind.TagMismatch) n++;
                return n;
            }
        }

        /// <summary>Rows whose source column no longer matches what was exported.
        /// Counted apart from the other issues because it means something quite
        /// different: not "this segment could not be updated" but "the file you are
        /// importing has been altered where it should not have been".</summary>
        public int SourceMismatchCount
        {
            get { int n = 0; foreach (var d in Diffs) if (d.Kind == ImportChangeKind.SourceMismatch) n++; return n; }
        }

        /// <summary>The segment numbers whose source was altered, so the message can
        /// name them instead of leaving the user to hunt through the file.</summary>
        public List<string> SourceMismatchNumbers
        {
            get
            {
                var list = new List<string>();
                foreach (var d in Diffs)
                    if (d.Kind == ImportChangeKind.SourceMismatch) list.Add(d.Number);
                return list;
            }
        }

        public int TagMismatchCount
        {
            get { int n = 0; foreach (var d in Diffs) if (d.Kind == ImportChangeKind.TagMismatch) n++; return n; }
        }
    }
}
