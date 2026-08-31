namespace Supervertaler.Trados.Core.Export
{
    /// <summary>One segment as read back from a round-tripped DOCX or Markdown.
    /// Maps to a row in the export manifest via <see cref="Number"/>.</summary>
    public class ImportedSegment
    {
        /// <summary>The segment number as Trados Studio shows it, e.g. "243"
        /// or "209a" for one half of a split. A string because a split id is
        /// not a number - and the join key for the round trip, so export and
        /// import must agree on its form. See <see cref="SegmentNumber"/>.</summary>
        public string Number { get; set; } = "";
        public string SourceText { get; set; }
        public string TargetText { get; set; }
        public string Status { get; set; }
    }
}
