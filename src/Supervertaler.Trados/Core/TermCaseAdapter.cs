using System;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Adapts the casing of a target term to the casing of the actual source
    /// occurrence in the segment. The termbase stores "More preferably" ↦
    /// "Meer bij voorkeur", but when the segment contains "more preferably"
    /// mid-sentence, TermLens should display (and insert) "meer bij voorkeur"
    /// – and conversely capitalise a stored lower-case term when the segment
    /// occurrence is sentence-initial.
    ///
    /// Rules (conservative on purpose – when in doubt, leave the target as stored):
    ///   • Only fires when the occurrence and the stored source term are the
    ///     same string ignoring case. Abbreviation matches, suffix-tolerant CJK
    ///     matches and punctuation-stripped variants fail this test and are
    ///     left untouched.
    ///   • Occurrence lower-case + stored source capitalised → lower-case the
    ///     target's initial, unless the target looks like an acronym or
    ///     mixed-case name (second letter upper-case, e.g. "MRI scan").
    ///   • Occurrence capitalised + stored source lower-case → capitalise the
    ///     target's initial (sentence start).
    ///   • Occurrence ALL-CAPS (≥2 letters, e.g. a heading) + stored source not
    ///     all-caps → upper-case the whole target.
    /// </summary>
    public static class TermCaseAdapter
    {
        /// <summary>
        /// Master switch, mirrored from TermLensSettings.AdaptTermCasing by
        /// TermLensEditorViewPart whenever settings are loaded or saved
        /// (same pattern as TermBlock.UseRepeatedDigitBadges).
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Returns <paramref name="target"/> with its casing adapted to how
        /// <paramref name="occurrence"/> (the matched text in the segment)
        /// differs from <paramref name="storedSource"/> (the term as stored
        /// in the termbase). Returns the target unchanged when adaptation is
        /// disabled or no safe rule applies.
        /// </summary>
        public static string Adapt(string occurrence, string storedSource, string target)
        {
            if (!Enabled) return target;
            if (string.IsNullOrEmpty(occurrence) || string.IsNullOrEmpty(storedSource)
                || string.IsNullOrEmpty(target))
                return target;

            // Fold space/apostrophe/sub-superscript variants so an IDML no-break
            // space never defeats the equality test (normalisation is
            // length-preserving, so indices stay valid).
            var occ = TermMatcher.NormalizeScriptChars(occurrence.Trim());
            var src = TermMatcher.NormalizeScriptChars(storedSource.Trim());

            // Same term differing by case alone – anything else (abbreviations,
            // CJK particles) is not ours to touch.
            if (!string.Equals(occ, src, StringComparison.OrdinalIgnoreCase))
                return target;
            if (string.Equals(occ, src, StringComparison.Ordinal))
                return target; // cases already agree

            // ALL-CAPS occurrence (headings): upper-case the whole target
            if (IsAllUpper(occ) && !IsAllUpper(src))
                return target.ToUpperInvariant();

            int occIdx = FirstLetterIndex(occ);
            int srcIdx = FirstLetterIndex(src);
            int tgtIdx = FirstLetterIndex(target);
            if (occIdx < 0 || srcIdx < 0 || tgtIdx < 0) return target;

            char occFirst = occ[occIdx];
            char srcFirst = src[srcIdx];
            char tgtFirst = target[tgtIdx];

            // "more preferably" in the segment, stored as "More preferably"
            // → lower the target initial. Guard: never touch acronyms /
            // mixed-case targets ("MRI scan") – only lower when the letter
            // after the target's initial is not upper-case.
            if (char.IsLower(occFirst) && char.IsUpper(srcFirst) && char.IsUpper(tgtFirst))
            {
                if (tgtIdx + 1 < target.Length && char.IsUpper(target[tgtIdx + 1]))
                    return target;
                return target.Substring(0, tgtIdx)
                    + char.ToLowerInvariant(tgtFirst)
                    + target.Substring(tgtIdx + 1);
            }

            // Sentence-initial occurrence of a term stored lower-case
            // → capitalise the target initial.
            if (char.IsUpper(occFirst) && char.IsLower(srcFirst) && char.IsLower(tgtFirst))
            {
                return target.Substring(0, tgtIdx)
                    + char.ToUpperInvariant(tgtFirst)
                    + target.Substring(tgtIdx + 1);
            }

            return target;
        }

        /// <summary>Index of the first letter character, or -1 if none.</summary>
        private static int FirstLetterIndex(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (char.IsLetter(s[i])) return i;
            return -1;
        }

        /// <summary>True when the string has ≥2 letters and every letter is upper-case.</summary>
        private static bool IsAllUpper(string s)
        {
            int letters = 0;
            foreach (var c in s)
            {
                if (!char.IsLetter(c)) continue;
                if (!char.IsUpper(c)) return false;
                letters++;
            }
            return letters >= 2;
        }
    }
}
