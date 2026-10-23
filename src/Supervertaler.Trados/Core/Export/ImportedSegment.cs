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

        /// <summary>The file the row says it is in, in an export of a document
        /// made of several files: the DOCX "File" column or file heading row, the
        /// text format's "📄 File:" line. Null when the format does not say.
        /// Studio numbers each file of a merged document from 1, so in a
        /// combined export the number alone does not name a segment.</summary>
        public string File { get; set; }

        public string SourceText { get; set; }
        public string TargetText { get; set; }
        public string Status { get; set; }
    }
}
