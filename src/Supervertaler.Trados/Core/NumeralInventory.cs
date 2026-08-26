using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Reference numerals cited in a patent's text, and how they reconcile
    /// against the drawings.
    ///
    /// <para>Step 2 of the reference-image feature, and useful before any of the
    /// rest exists. Extracting the numerals is cheap and exact, and it turns
    /// "describe these drawings" into a task that can be CHECKED: a vision pass
    /// handed a checklist can be told it missed something, where one asked an
    /// open question can only be believed.</para>
    ///
    /// <para>It also stands alone as QA. "Numerals in your drawings that never
    /// appear in the text, and vice versa" is a real finding for a patent
    /// translator — usually a drafting defect in the source, occasionally a
    /// missed sentence in the translation.</para>
    /// </summary>
    public static class NumeralInventory
    {
        /// <summary>
        /// Matches parenthesised numerals as patents cite them: <c>(12)</c>, and
        /// lists like <c>(12, 14, 16)</c>.
        ///
        /// <para>Bounded to 1-3 digits deliberately. Longer runs of digits in
        /// brackets are dates, standards references and claim counts, not part
        /// numerals, and admitting them fills the inventory with noise that the
        /// reconciliation then reports as "missing from the drawings".</para>
        /// </summary>
        private static readonly Regex NumeralRe = new Regex(
            @"\((\d{1,3}(?:\s*,\s*\d{1,3})*)\)", RegexOptions.Compiled);

        /// <summary>
        /// Every distinct numeral cited in <paramref name="text"/>, ascending,
        /// with the sentences that cite each one.
        ///
        /// <para>The sentences matter as much as the numbers: they are what lets a
        /// vision pass tie a numeral to a part without guessing, and what lets a
        /// human check the result.</para>
        /// </summary>
        public static NumeralReport Extract(IEnumerable<string> segments)
        {
            var report = new NumeralReport();
            if (segments == null) return report;

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;

                foreach (Match m in NumeralRe.Matches(segment))
                {
                    foreach (var part in m.Groups[1].Value.Split(','))
                    {
                        if (!int.TryParse(part.Trim(), out var n)) continue;

                        if (!report.Citations.TryGetValue(n, out var list))
                        {
                            list = new List<string>();
                            report.Citations[n] = list;
                        }
                        // One citation per numeral per segment; a numeral repeated
                        // in a sentence is one use of it, not several.
                        if (!list.Contains(segment)) list.Add(segment);
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Compares numerals cited in the text against numerals found in the
        /// drawings, and reports the three-way difference.
        ///
        /// <para>Reporting the differences is the point. A pass that quietly
        /// dropped the numerals it could not place would look identical to one
        /// that placed them all.</para>
        /// </summary>
        /// <param name="inText">Numerals extracted from the source text.</param>
        /// <param name="inDrawings">Numerals a vision pass reported seeing, or
        /// null when no vision pass has run.</param>
        public static ReconciliationResult Reconcile(
            IEnumerable<int> inText, IEnumerable<int> inDrawings)
        {
            var text = new SortedSet<int>(inText ?? Enumerable.Empty<int>());
            var drawn = new SortedSet<int>(inDrawings ?? Enumerable.Empty<int>());

            return new ReconciliationResult
            {
                InBoth = new SortedSet<int>(text.Intersect(drawn)),
                // Cited but not drawn: either the vision pass is incomplete, or
                // the numeral genuinely is not in any figure.
                TextOnly = new SortedSet<int>(text.Except(drawn)),
                // Drawn but never cited: a source defect worth surfacing. The
                // worked example (Acme (PROJ-001) had exactly one.
                DrawingsOnly = new SortedSet<int>(drawn.Except(text))
            };
        }

        /// <summary>Renders the report as Markdown for the chat panel or a file.</summary>
        public static string Format(NumeralReport report, ReconciliationResult reconciliation)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Reference numerals");
            sb.AppendLine();

            if (report == null || report.Citations.Count == 0)
            {
                sb.AppendLine("No parenthesised reference numerals found in the source text.");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine("**" + report.Citations.Count + " distinct numerals** cited in the source, "
                        + "from " + report.Numerals.Min() + " to " + report.Numerals.Max() + ".");
            sb.AppendLine();

            if (reconciliation != null)
            {
                if (reconciliation.DrawingsOnly.Count > 0)
                {
                    sb.AppendLine("### In the drawings but never cited in the text");
                    sb.AppendLine();
                    sb.AppendLine("Usually a drafting defect in the source, and worth raising with the client: "
                                + string.Join(", ", reconciliation.DrawingsOnly));
                    sb.AppendLine();
                }

                if (reconciliation.TextOnly.Count > 0)
                {
                    sb.AppendLine("### Cited in the text but not found in the drawings");
                    sb.AppendLine();
                    sb.AppendLine("Either the figure analysis is incomplete, or these numerals genuinely "
                                + "appear in no figure: " + string.Join(", ", reconciliation.TextOnly));
                    sb.AppendLine();
                }

                if (reconciliation.DrawingsOnly.Count == 0 && reconciliation.TextOnly.Count == 0)
                {
                    sb.AppendLine("Every numeral in the text appears in the drawings, and vice versa.");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("### Citations");
            sb.AppendLine();
            // A list rather than a table: this is read in a narrow docked
            // panel, where three columns are the wrong shape however they are
            // rendered, and repeating the column headers on every row is most
            // of the noise.
            foreach (var n in report.Numerals)
            {
                var cites = report.Citations[n];
                var first = cites.Count > 0 ? cites[0] : "";
                first = WindowAround(first, n, 160);
                first = first.Replace("\r", " ").Replace("\n", " ");

                sb.AppendLine("**(" + n + ")**  \u00b7  cited " + cites.Count
                    + (cites.Count == 1 ? " time" : " times"));
                sb.AppendLine();
                sb.AppendLine(first);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// A <paramref name="width"/>-character window of <paramref name="text"/>
        /// centred on where numeral <paramref name="n"/> is cited, with an
        /// ellipsis on whichever side was cut.
        ///
        /// <para>Truncating from the start instead put the numeral past the cut
        /// in 4 of 20 rows on a real patent, so the row for numeral 15 showed a
        /// snippet containing only 9 and 10 - a table you had to cross-reference
        /// against the document to read.</para>
        /// </summary>
        private static string WindowAround(string text, int n, int width)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= width) return text ?? "";

            // Where is this numeral actually cited? Parenthesised, possibly in a
            // list: (15) or (12, 15, 18).
            var hit = Regex.Match(text, @"\(\s*\d{1,3}(?:\s*,\s*\d{1,3})*\s*\)");
            int at = -1;
            while (hit.Success)
            {
                foreach (var part in hit.Value.Trim('(', ')').Split(','))
                {
                    int v;
                    if (int.TryParse(part.Trim(), out v) && v == n) { at = hit.Index; break; }
                }
                if (at >= 0) break;
                hit = hit.NextMatch();
            }

            if (at < 0) return text.Substring(0, width - 3) + "\u2026";

            var start = Math.Max(0, at - width / 2);
            var len = Math.Min(width, text.Length - start);
            var slice = text.Substring(start, len);

            if (start > 0) slice = "\u2026" + slice;
            if (start + len < text.Length) slice = slice + "\u2026";
            return slice;
        }
    }

    /// <summary>Numerals cited in the source, with the sentences citing them.</summary>
    public class NumeralReport
    {
        /// <summary>Numeral → the distinct segments citing it, in document order.</summary>
        public SortedDictionary<int, List<string>> Citations { get; }
            = new SortedDictionary<int, List<string>>();

        /// <summary>Every cited numeral, ascending.</summary>
        public IEnumerable<int> Numerals => Citations.Keys;

        public bool HasAny => Citations.Count > 0;
    }

    /// <summary>The three-way split between text and drawings.</summary>
    public class ReconciliationResult
    {
        public SortedSet<int> InBoth { get; set; } = new SortedSet<int>();

        /// <summary>Cited in the text, not seen in any drawing.</summary>
        public SortedSet<int> TextOnly { get; set; } = new SortedSet<int>();

        /// <summary>Seen in a drawing, never cited in the text — a source defect.</summary>
        public SortedSet<int> DrawingsOnly { get; set; } = new SortedSet<int>();

        public bool IsClean => TextOnly.Count == 0 && DrawingsOnly.Count == 0;
    }
}
