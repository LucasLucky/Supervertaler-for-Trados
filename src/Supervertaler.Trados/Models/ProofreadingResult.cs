using System;
using System.Collections.Generic;
using System.Linq;

namespace Supervertaler.Trados.Models
{
    public class ProofreadingIssue
    {
        public int SegmentIndex { get; set; }       // 0-based
        public int SegmentNumber { get; set; }       // 1-based display
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
    }

    public enum BatchMode { Translate, Proofread }
    public enum ProofreadScope { ConfirmedOnly, TranslatedAndConfirmed, AllSegments, Filtered, FilteredConfirmedOnly, DraftOnly }
}
