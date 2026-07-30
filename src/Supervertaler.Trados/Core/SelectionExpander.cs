using System;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Expands a partial text selection to full word boundaries.
    ///
    /// In the Trados editor grid, users often select across word boundaries by
    /// grabbing just a few letters at the end of one word and the start of the
    /// next (e.g. selecting "ing pr" to mean "warning profiles"). This class
    /// finds the partial selection within the full segment text and expands it
    /// outward to encompass complete words.
    ///
    /// Selection priority (highest to lowest):
    ///   1. Exact word-boundary match – selection is already a complete word
    ///   2. Shortest expansion – when multiple words contain the selection,
    ///      the shortest enclosing word wins (e.g. "echt" → "hechting" not
    ///      "hechtingsbevorderaars")
    /// </summary>
    public static class SelectionExpander
    {
        /// <summary>
        /// Expands a partial text selection to full word boundaries within the
        /// full segment text.
        ///
        /// Example: fullText = "selecting warning profiles, reading out event logs"
        ///          partialSelection = "ing pr"
        ///          result = "warning profiles"
        ///
        /// If the selection already sits at word boundaries somewhere in the
        /// text, it is returned as-is (no expansion).
        ///
        /// Example: fullText = "hechtingsbevorderaars ... de hechting kunnen"
        ///          partialSelection = "hechting"
        ///          result = "hechting"   (NOT "hechtingsbevorderaars")
        ///
        /// When the selection is embedded inside multiple words, the shortest
        /// enclosing word is preferred.
        ///
        /// Example: fullText = "hechtingsbevorderaars ... de hechting kunnen"
        ///          partialSelection = "echt"
        ///          result = "hechting"   (8 chars, shorter than "hechtingsbevorderaars")
        /// </summary>
        /// <param name="fullText">The complete segment text.</param>
        /// <param name="partialSelection">The user's (possibly partial) selection.</param>
        /// <returns>The expanded text, or the original selection if it can't be found.</returns>
        /// <summary>
        /// Overload that can skip auto-expansion. When <paramref name="autoExpand"/>
        /// is false the exact (trimmed) selection is returned unchanged — used for
        /// Korean/Japanese where expanding to the whitespace token would swallow
        /// an attached particle (saving 장치의 instead of the intended 장치).
        /// </summary>
        public static string ExpandToWordBoundaries(string fullText, string partialSelection, bool autoExpand)
        {
            if (!autoExpand)
                return (partialSelection ?? "").Trim();
            return ExpandToWordBoundaries(fullText, partialSelection);
        }

        public static string ExpandToWordBoundaries(string fullText, string partialSelection)
        {
            if (string.IsNullOrEmpty(fullText) || string.IsNullOrEmpty(partialSelection))
                return (partialSelection ?? "").Trim();

            // Strip leading/trailing whitespace before matching – a selection like
            // "trimethoxysilaan " (trailing space) would otherwise cause endPos to land
            // on the next word, making the expansion loop swallow it ("trimethoxysilaan of").
            partialSelection = partialSelection.Trim();
            if (string.IsNullOrEmpty(partialSelection))
                return "";

            // Try case-sensitive first, then case-insensitive
            string result = FindBestExpansion(fullText, partialSelection, StringComparison.Ordinal);
            if (result == null)
                result = FindBestExpansion(fullText, partialSelection, StringComparison.OrdinalIgnoreCase);

            return result ?? partialSelection.Trim();
        }

        /// <summary>
        /// Scans all occurrences of <paramref name="needle"/> inside
        /// <paramref name="haystack"/>, expands each to word boundaries,
        /// and returns the best result.
        ///
        /// Priority: (1) exact word-boundary match (no expansion needed),
        /// (2) shortest expanded word among all candidates.
        /// </summary>
        private static string FindBestExpansion(string haystack, string needle,
            StringComparison comparison)
        {
            string bestExpansion = null;
            int bestLength = int.MaxValue;
            int pos = 0;

            while (pos <= haystack.Length - needle.Length)
            {
                int idx = haystack.IndexOf(needle, pos, comparison);
                if (idx < 0) break;

                bool atLeft = idx == 0 || !IsWordChar(haystack[idx - 1]);
                int endPos = idx + needle.Length;
                bool atRight = endPos >= haystack.Length || !IsWordChar(haystack[endPos]);

                if (atLeft && atRight)
                {
                    // Perfect word-boundary match – return immediately
                    return TrimNonWordEdges(needle);
                }

                // Expand outward to word boundaries
                int start = idx;
                while (start > 0 && !char.IsWhiteSpace(haystack[start - 1]))
                    start--;

                int end = endPos;
                while (end < haystack.Length && !char.IsWhiteSpace(haystack[end]))
                    end++;

                string expanded = TrimNonWordEdges(haystack.Substring(start, end - start));

                // Prefer the shortest expansion – the user most likely
                // intended the simpler/base word, not a longer compound
                if (expanded.Length < bestLength)
                {
                    bestLength = expanded.Length;
                    bestExpansion = expanded;
                }

                pos = idx + 1;
            }

            return bestExpansion;
        }

        /// <summary>
        /// Trims non-word characters (punctuation, brackets, quotes) from the
        /// edges of a string, keeping hyphens and apostrophes which are valid
        /// inside terms.
        /// </summary>
        private static string TrimNonWordEdges(string text)
        {
            int trimStart = 0;
            while (trimStart < text.Length && !IsWordChar(text[trimStart]))
                trimStart++;

            int trimEnd = text.Length - 1;
            while (trimEnd >= trimStart && !IsWordChar(text[trimEnd]))
            {
                // Don't strip a closing bracket that is balanced inside what we
                // are keeping – "tekst (met noot)" must not become "tekst (met
                // noot", which is both wrong and unbalanced.
                if (IsBalancedCloser(text, trimStart, trimEnd)) break;
                trimEnd--;
            }

            if (trimStart > trimEnd)
                return text.Trim(); // degenerate case

            var kept = text.Substring(trimStart, trimEnd - trimStart + 1);
            return DropAttachedBracketSuffix(DropAttachedBracketPrefix(text, trimStart, trimEnd));
        }

        /// <summary>
        /// Mirror of <see cref="DropAttachedBracketSuffix"/> for the other end:
        /// "(re)certification" and "(her)certificering" yield "certification" /
        /// "certificering". Reads from the ORIGINAL text, because the leading
        /// bracket has already been trimmed off the kept range by the time we
        /// get here — that trim is what turns "(re)certification" into the
        /// unbalanced "re)certification" seen in real termbase data.
        /// </summary>
        private static string DropAttachedBracketPrefix(string text, int trimStart, int trimEnd)
        {
            const string openers = "([{";
            const string closers = ")]}";

            var kept = text.Substring(trimStart, trimEnd - trimStart + 1);

            // Only relevant when an opener sat immediately before the kept range.
            if (trimStart == 0 || openers.IndexOf(text[trimStart - 1]) < 0) return kept;
            int o = openers.IndexOf(text[trimStart - 1]);

            int close = kept.IndexOf(closers[o]);
            if (close < 0) return kept;                        // no closer to pair with

            // Attached to the word that follows, or a standalone parenthetical?
            if (close + 1 >= kept.Length || !IsWordChar(kept[close + 1])) return kept;

            var tail = kept.Substring(close + 1);
            int s = 0;
            while (s < tail.Length && !IsWordChar(tail[s])) s++;
            return s >= tail.Length ? kept : tail.Substring(s);
        }

        /// <summary>True when text[end] closes a bracket opened at or after
        /// <paramref name="start"/> – i.e. the pair is intact within the range.</summary>
        private static bool IsBalancedCloser(string text, int start, int end)
        {
            const string openers = "([{";
            const string closers = ")]}";
            int c = closers.IndexOf(text[end]);
            if (c < 0) return false;

            int depth = 0;
            for (int i = end; i >= start; i--)
            {
                if (text[i] == closers[c]) depth++;
                else if (text[i] == openers[c] && --depth == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Drops a trailing bracket group that is attached directly to a word,
        /// so "verkoper(s)" yields the base term "verkoper".
        ///
        /// The "(s)" optional-plural convention is everywhere in Dutch and
        /// English legal text, and the base word is what belongs in the
        /// termbase: TermMatcher tokenises "verkoper(s)" in a segment as
        /// "verkoper" + "s", so an entry storing the brackets would never be
        /// highlighted.
        ///
        /// Attachment is the signal. "verkoper(s)" has no space before the
        /// bracket and marks an inflection; "tekst (met noot)" is a parenthetical
        /// phrase the user deliberately selected, and is left intact.
        /// </summary>
        private static string DropAttachedBracketSuffix(string text)
        {
            const string openers = "([{";
            const string closers = ")]}";

            if (text.Length < 3) return text;
            int c = closers.IndexOf(text[text.Length - 1]);
            if (c < 0) return text;                       // doesn't end with a bracket

            // Walk back to the matching opener.
            int depth = 0, open = -1;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == closers[c]) depth++;
                else if (text[i] == openers[c] && --depth == 0) { open = i; break; }
            }
            if (open <= 0) return text;                   // unmatched, or the whole string

            // Attached to the preceding word, or a separate parenthetical?
            if (!IsWordChar(text[open - 1])) return text;

            var head = text.Substring(0, open);
            int end = head.Length - 1;
            while (end >= 0 && !IsWordChar(head[end])) end--;
            return end < 0 ? text : head.Substring(0, end + 1);
        }

        /// <summary>
        /// Returns true if the character is part of a "word" for term purposes:
        /// letters, digits, hyphens (compound words), and apostrophes (contractions).
        /// </summary>
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '\'' || c == '\u2019'; // right single quote
        }
    }
}
