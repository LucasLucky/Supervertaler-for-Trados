namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// A segment as a log line or a message names it: the way Trados Studio does,
    /// by the number its grid shows ("12", "331 a" for a split segment) and, in a
    /// document of several files, the file - Studio numbers each file's segments
    /// from 1, so a number alone does not say which segment is meant. The prompts
    /// number a merged document the same way (CollectDocumentContext).
    ///
    /// <para>Until 201 the Batch Operations log named a segment Studio declined
    /// to change by Supervertaler's own 0-based position: Studio's "segment 2" of
    /// the second file was "Failed to write segment 14" from Batch Translate and
    /// "Failed to write segment 0" from Alt+T (seen 24 Oct 2026).</para>
    /// </summary>
    internal static class SegmentLabel
    {
        /// <summary>"segment 12", or "segment 2 of Text.docx" with a file.</summary>
        public static string Of(string number, string file = null, bool capital = false)
        {
            var name = (capital ? "Segment " : "segment ")
                + (string.IsNullOrWhiteSpace(number) ? "?" : number.Trim());
            return string.IsNullOrEmpty(file) ? name : name + " of " + file;
        }
    }
}
