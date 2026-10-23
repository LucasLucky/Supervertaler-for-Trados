using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// Parses a Supervertaler Bilingual Table DOCX back into a list of
    /// <see cref="ImportedSegment"/>s.
    ///
    /// Expects the first table in the document to have a header row whose
    /// first cell is "#" and at least Source, Target and Status after it.
    /// A trailing Notes column is tolerated but ignored: it was dropped from
    /// the exports, and files written before that (or by the Workbench) still
    /// carry one.
    /// Falls back to ordering if the header isn't an exact match — the
    /// segment number from the first cell is the authoritative key.
    /// </summary>
    public class DocxImporter
    {
        public List<ImportedSegment> Parse(string filePath)
        {
            var rows = new List<ImportedSegment>();

            using (var doc = WordprocessingDocument.Open(filePath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return rows;

                var table = body.Elements<Table>().FirstOrDefault();
                if (table == null) return rows;

                var tableRows = table.Elements<TableRow>().ToList();
                if (tableRows.Count < 2) return rows; // header + at least one data row

                // Detect column layout. Cells 0-2 are always #, Source,
                // Target; the trailing columns vary: [File] (multi-file
                // exports), Status, [Comments] (exports where segments
                // carry Trados comments), Notes. We locate the fixed-name
                // columns in the header row by their literal text — the
                // Source/Target headers are language display names and
                // never collide with them. Falls back to the legacy
                // count-based sniff (>= 6 cells = multi-file) for files
                // whose header doesn't parse.
                var headerRow = tableRows[0];
                var headerCells = headerRow.Elements<TableCell>().ToList();
                int statusIdx = -1, fileIdx = -1;
                for (int c = 3; c < headerCells.Count; c++)
                {
                    var h = ExtractCellText(headerCells[c]).Trim();
                    if (h.Equals("Status", System.StringComparison.OrdinalIgnoreCase)) statusIdx = c;

                    // The "File" column says which file of a merged document a
                    // row is in, which the number alone does not: Studio numbers
                    // every file from 1. "Comments" carries nothing re-importable.
                    if (h.Equals("File", System.StringComparison.OrdinalIgnoreCase)) fileIdx = c;
                }
                bool headerMapped = statusIdx >= 0;
                bool sixColumn = headerCells.Count >= 6;
                if (fileIdx < 0 && !headerMapped && sixColumn) fileIdx = 3;   // legacy: #, Src, Tgt, File, Status, Notes

                // The file the rows below belong to, from the last file heading
                // row: the fallback when a row has no File cell of its own.
                string sectionFile = null;

                // Skip the header row (row 0); data starts at row 1.
                for (int i = 1; i < tableRows.Count; i++)
                {
                    var cells = tableRows[i].Elements<TableCell>().ToList();

                    // Multi-file section-break row: a single spanned cell
                    // carrying "File: <name>". No segment data, but it says
                    // which file the rows after it are in. (This check used to
                    // come after "fewer than 3 cells: skip", so it never ran.)
                    if (cells.Count == 1)
                    {
                        sectionFile = FileFromHeading(ExtractCellText(cells[0]));
                        continue;
                    }
                    if (cells.Count < 3) continue;

                    // The "#" column carries Studio's number, which may end in a
                    // letter ("209a"). Requiring a leading digit still skips the header
                    // and file-name rows, which is all the old int.TryParse did here.
                    var numText = ExtractCellText(cells[0]).Trim();
                    if (numText.Length == 0 || !char.IsDigit(numText[0])) continue;
                    var number = SegmentNumber.Canonical(numText);

                    var seg = new ImportedSegment
                    {
                        Number = number,
                        SourceText = ExtractCellText(cells[1]),
                        TargetText = ExtractCellText(cells[2])
                    };
                    var fileCell = fileIdx >= 0 && fileIdx < cells.Count ? ExtractCellText(cells[fileIdx]).Trim() : "";
                    seg.File = fileCell.Length > 0 ? fileCell : sectionFile;
                    if (headerMapped)
                    {
                        seg.Status = statusIdx < cells.Count ? ExtractCellText(cells[statusIdx]) : "";
                    }
                    else if (sixColumn && cells.Count >= 6)
                    {
                        // cells: 0=#, 1=Src, 2=Tgt, 3=File, 4=Status[, 5=Notes]
                        // A trailing Notes column may be present in files written
                        // before it was dropped, or by the Workbench. Ignored.
                        seg.Status = ExtractCellText(cells[4]);
                    }
                    else
                    {
                        // cells: 0=#, 1=Src, 2=Tgt, 3=Status[, 4=Notes]
                        seg.Status = cells.Count > 3 ? ExtractCellText(cells[3]) : "";
                    }
                    rows.Add(seg);
                }
            }

            return rows;
        }

        /// <summary>The name in a file heading row ("📄  File: Acme - Figures.docx"),
        /// or null for "(unknown)" or anything else. The icon in front is a
        /// surrogate pair plus spaces, so "File:" is looked for near the start
        /// rather than at it.</summary>
        private static string FileFromHeading(string text)
        {
            var t = (text ?? "").Trim();
            var at = t.IndexOf("File:", System.StringComparison.OrdinalIgnoreCase);
            if (at < 0 || at > 4) return null;
            t = t.Substring(at + "File:".Length).Trim();
            return t.Length == 0 || t == "(unknown)" ? null : t;
        }

        /// <summary>Concatenate all text runs in a cell, inserting newlines
        /// for Break elements so soft returns survive round-trip.</summary>
        private static string ExtractCellText(TableCell cell)
        {
            var sb = new StringBuilder();
            // Walk paragraph-by-paragraph; insert \n between paragraphs.
            var paragraphs = cell.Elements<Paragraph>().ToList();
            for (int p = 0; p < paragraphs.Count; p++)
            {
                if (p > 0) sb.Append('\n');
                AppendParagraphText(paragraphs[p], sb);
            }
            return sb.ToString().Trim();
        }

        private static void AppendParagraphText(Paragraph p, StringBuilder sb)
        {
            foreach (var child in p.Descendants())
            {
                if (child is Text t)
                {
                    sb.Append(t.Text);
                }
                else if (child is Break)
                {
                    sb.Append('\n');
                }
                else if (child is TabChar)
                {
                    sb.Append('\t');
                }
            }
        }
    }
}
