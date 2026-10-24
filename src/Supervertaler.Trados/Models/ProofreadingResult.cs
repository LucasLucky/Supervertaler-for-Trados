using System;
using System.Collections.Generic;
using System.Linq;

namespace Supervertaler.Trados.Models
{
    public class ProofreadingIssue
    {
        public int SegmentIndex { get; set; }       // 0-based
        public int SegmentNumber { get; set; }       // 1-based display

        /// <summary>The file the segment is in, in a document made of several
        /// files; null otherwise. Studio numbers every file from 1, so "segment 3"
        /// alone names one segment per file.</summary>
        public string FileName { get; set; }

        /// <summary>How the Reports tab, the log, comments and the saved report
        /// name the segment: "3", or "3 of Acme - Text.docx" in a document of
        /// several files (Studio's ".sdlxliff" left off).</summary>
        public string Label
        {
            get
            {
                if (string.IsNullOrEmpty(FileName)) return SegmentNumber.ToString();
                var name = FileName.EndsWith(".sdlxliff", StringComparison.OrdinalIgnoreCase)
                    ? FileName.Substring(0, FileName.Length - ".sdlxliff".Length) : FileName;
                return SegmentNumber + " of " + name;
            }
        }

        public string SourceText { get; set; }
        public string TargetText { get; set; }
        public bool IsOk { get; set; }
        public string IssueDescription { get; set; }
        public string Suggestion { get; set; }
        public string Evidence { get; set; }          // Specific source segments cited by the model (terminology consistency etc.)

        /// <summary>The proofreader's whole target with the fix made, in the
        /// placeholders of <see cref="SourceText"/> and <see cref="TargetText"/>
        /// (both as the proofreader was shown them), or null when it gave none or
        /// it reads as the target already does. What Apply writes.</summary>
        public string CorrectedTarget { get; set; }

        /// <summary>Apply wrote <see cref="CorrectedTarget"/> into the segment. An
        /// applied issue is also <see cref="Dismissed"/>: it is dealt with.</summary>
        public bool Applied { get; set; }
        public object SegmentPairRef { get; set; }   // ISegmentPair or string[] for navigation
        public string ParagraphUnitId { get; set; }
        public string SegmentId { get; set; }

        /// <summary>Ticked off in the Reports tab (#105). Survives a restore, so a
        /// report put back after a restart shows only what is still open.</summary>
        public bool Dismissed { get; set; }
    }

    public class ProofreadingReport
    {
        public List<ProofreadingIssue> Issues { get; set; } = new List<ProofreadingIssue>();
        public int TotalSegmentsChecked { get; set; }
        public int IssueCount => Issues.Count(i => !i.IsOk);
        public int OkCount => Issues.Count(i => i.IsOk);

        /// <summary>
        /// <paramref name="issues"/> in document order: file by file, in the order
        /// the files first appear in the report, then by segment number. Sorting by
        /// number alone, as the tab and the saved report did, interleaved the files
        /// of a merged document - 3 of one file, 3 of the next, 4 of the first. In a
        /// one-file report this is the order by number, as before.
        /// </summary>
        public IEnumerable<ProofreadingIssue> InDocumentOrder(IEnumerable<ProofreadingIssue> issues)
        {
            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in Issues)
            {
                var f = i.FileName ?? "";
                if (!rank.ContainsKey(f)) rank[f] = rank.Count;
            }
            return issues
                .OrderBy(i => rank.TryGetValue(i.FileName ?? "", out var r) ? r : int.MaxValue)
                .ThenBy(i => i.SegmentNumber);
        }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }

        /// <summary>The document the run was made on (#105): a restored report is
        /// shown only when this document is the active one again.</summary>
        public string DocumentPath { get; set; }
        public string DocumentName { get; set; }
        public string SourceLang { get; set; }
        public string TargetLang { get; set; }

        /// <summary>The model that made the report, as its name is shown (e.g.
        /// "Claude Opus 5.5"): a finding added as a Trados comment names it, at
        /// the end of the run or when it is applied later. Null for a report saved
        /// before 18.20.201.</summary>
        public string Model { get; set; }

        /// <summary>The run checked the translation alone, without sending the
        /// source (<see cref="Core.ProofreadingPrompt.BuildTargetOnlySystemPrompt"/>):
        /// a reader of the report needs to know that accuracy and terminology were
        /// not checked. False for a report saved before 18.20.201.</summary>
        public bool TargetOnly { get; set; }
    }

    public enum BatchMode { Translate, Proofread }
    public enum ProofreadScope { ConfirmedOnly, TranslatedAndConfirmed, AllSegments, Filtered, FilteredConfirmedOnly, DraftOnly }
}
