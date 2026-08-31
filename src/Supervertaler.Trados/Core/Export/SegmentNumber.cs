using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// The number a segment is known by: the one Trados Studio shows in its
    /// grid, not a count of exported rows.
    /// </summary>
    /// <remarks>
    /// A bilingual export used to number its rows 1..N with a running counter.
    /// That agrees with Studio only until a document contains a split segment.
    /// Splitting 209 gives Studio 209a and 209b, and Studio carries on with 210
    /// - it does not renumber. The counter instead wrote 209 and 210 for the two
    /// halves, so every later row was one higher than the segment it stood for,
    /// and a proofreader's "see row 244" pointed at segment 243.
    ///
    /// The number therefore has to be a string: Studio's own id for a split
    /// segment is "209 a", which no integer can hold. That is also why this
    /// class exists rather than a couple of inline calls - export and import
    /// must agree exactly on the form, or the round trip stops matching.
    ///
    /// Three forms, and the distinction matters:
    ///
    ///   raw        what Studio's API returns:  "209 a"
    ///   canonical  what the manifest keys on:  "209a"
    ///   display    what a reader sees:         "0209a" (text export, padded)
    ///
    /// Canonical is the join key at both ends. Importers run whatever they read
    /// back through <see cref="Canonical"/> before looking it up, so padding and
    /// spacing can differ between formats without breaking re-import.
    /// </remarks>
    public static class SegmentNumber
    {
        /// <summary>
        /// The comparable form of a segment number: no spaces, lower-case
        /// suffix, no leading zeros on the numeric part. "209 a", "0209A" and
        /// "209a" all reduce to "209a".
        /// </summary>
        public static string Canonical(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if (char.IsWhiteSpace(ch)) continue;
                sb.Append(char.ToLowerInvariant(ch));
            }
            var s = sb.ToString();
            if (s.Length == 0) return "";

            // Trim leading zeros from the numeric part only, and never to
            // nothing: "0209a" -> "209a", "0000" -> "0".
            int i = 0;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 0) return s;                       // no numeric part at all

            var digits = s.Substring(0, i).TrimStart('0');
            if (digits.Length == 0) digits = "0";
            return digits + s.Substring(i);
        }

        /// <summary>
        /// How wide to zero-pad the numeric part so the markers in a text
        /// export line up. Minimum 4, matching the Workbench's "0001".
        /// </summary>
        public static int PadWidthFor(IEnumerable<ExportSegment> segments)
        {
            int widest = 0;
            if (segments != null)
            {
                foreach (var seg in segments)
                {
                    var n = NumericPart(seg == null ? null : seg.Number);
                    if (n.Length > widest) widest = n.Length;
                }
            }
            return widest > 4 ? widest : 4;
        }

        /// <summary>
        /// The padded form for a text export: "209a" at width 4 becomes
        /// "0209a". Any letter suffix is preserved and never padded.
        /// </summary>
        public static string Display(string canonical, int padWidth)
        {
            if (string.IsNullOrEmpty(canonical)) return "";
            var digits = NumericPart(canonical);
            if (digits.Length == 0) return canonical;   // nothing to pad

            var suffix = canonical.Substring(digits.Length);
            return digits.PadLeft(padWidth, '0') + suffix;
        }

        private static string NumericPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = 0;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            return s.Substring(0, i);
        }
    }
}
