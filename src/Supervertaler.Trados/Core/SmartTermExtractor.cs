using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// AI-based extraction of one term pair (plus abbreviations) from a single
    /// source/target segment pair, for the Smart-add term action.
    ///
    /// The manual flow this replaces: the translator spots a concept written out
    /// in full with its abbreviation in brackets – "Sustainable Finance Disclosure
    /// Regulation (SFDR, Verordening (EU) 2019/2088)" – selects the term on both
    /// sides, opens the term entry dialog and types the abbreviation into both
    /// abbreviation fields by hand. The model does the spotting; the translator
    /// still confirms and saves, so a wrong extraction costs one glance rather
    /// than a corrupted entry.
    ///
    /// SAFETY: every string the model returns is checked to be a verbatim
    /// substring of the segment it claims to come from (see <see cref="Parse"/>).
    /// That is what stops an invented abbreviation or a paraphrased term reaching
    /// the dialog: the model can only ever select text that is already on screen,
    /// never compose it. Anything that fails the check is dropped rather than
    /// shown. This matters because term pairs in this domain are routinely
    /// identical across languages (SFDR, radar, transponder), so a human glancing
    /// at a pre-filled dialog cannot reliably tell an invented abbreviation from
    /// a real one.
    ///
    /// This class only builds the prompt and parses the reply; the LLM call
    /// itself is made by the caller via its LlmClient, mirroring
    /// <see cref="DocumentContextClassifier"/>.
    /// </summary>
    internal static class SmartTermExtractor
    {
        public const string SystemPrompt =
            "You extract a single terminology entry from one source/target segment " +
            "pair for a professional translator's termbase. You only ever copy text " +
            "that already appears in the segments – you never translate, paraphrase, " +
            "expand or invent. Reply with ONLY a JSON object, no prose, no code fences.";

        /// <summary>Result of a parsed extraction. All fields are "" when absent.</summary>
        internal sealed class Result
        {
            /// <summary>False when the model found nothing worth adding, or the reply failed validation.</summary>
            public bool Found;
            public string SourceTerm = "";
            public string TargetTerm = "";
            public string SourceAbbreviation = "";
            public string TargetAbbreviation = "";
            /// <summary>Diagnostic note – why nothing was returned, or what was dropped.</summary>
            public string Note = "";
        }

        /// <summary>
        /// Builds the extraction prompt. <paramref name="sourceSelection"/> /
        /// <paramref name="targetSelection"/> are optional hints: when the
        /// translator had text selected, the model is steered to the term
        /// overlapping it rather than picking its own favourite from the segment.
        /// </summary>
        public static string BuildUserPrompt(
            string sourceSegment, string targetSegment,
            string sourceLang, string targetLang,
            string sourceSelection = null, string targetSelection = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Extract ONE terminology entry from the segment pair below.");
            sb.AppendLine();
            sb.AppendLine($"Source language: {Safe(sourceLang, "the source language")}");
            sb.AppendLine($"Target language: {Safe(targetLang, "the target language")}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("1. \"sourceTerm\" MUST be copied character-for-character from the SOURCE segment.");
            sb.AppendLine("   \"targetTerm\" MUST be copied character-for-character from the TARGET segment.");
            sb.AppendLine("   Never translate, never rephrase, never correct spelling. Copy only.");
            sb.AppendLine("2. Choose the most useful terminological unit: prefer the complete multi-word");
            sb.AppendLine("   term over a fragment of it, and prefer a domain term over ordinary wording.");
            sb.AppendLine("3. Do NOT include in the term itself: the abbreviation, any brackets, any");
            sb.AppendLine("   surrounding punctuation, or a leading article/determiner.");
            sb.AppendLine("4. Abbreviations: fill these ONLY if the abbreviation genuinely appears in that");
            sb.AppendLine("   segment, normally in brackets directly after the full term. Copy it verbatim.");
            sb.AppendLine("   If a side has no abbreviation in its segment, return \"\" for that side.");
            sb.AppendLine("   NEVER invent, guess, derive or translate an abbreviation. An empty field is");
            sb.AppendLine("   always better than one you constructed yourself.");
            sb.AppendLine("5. The same abbreviation often serves both languages (e.g. an EU regulation");
            sb.AppendLine("   name). That is fine – but only report it on a side where it really appears.");
            sb.AppendLine("6. If the segment pair contains no term worth storing, return {\"found\": false}.");
            sb.AppendLine();

            bool haveHint = !string.IsNullOrWhiteSpace(sourceSelection)
                         || !string.IsNullOrWhiteSpace(targetSelection);
            if (haveHint)
            {
                sb.AppendLine("The translator has selected the text below. Extract the term that this");
                sb.AppendLine("selection is part of, completing it to the full term where the selection");
                sb.AppendLine("is only a fragment. Do not pick an unrelated term from elsewhere.");
                if (!string.IsNullOrWhiteSpace(sourceSelection))
                    sb.AppendLine($"  Selected in source: {sourceSelection.Trim()}");
                if (!string.IsNullOrWhiteSpace(targetSelection))
                    sb.AppendLine($"  Selected in target: {targetSelection.Trim()}");
                sb.AppendLine();
            }

            sb.AppendLine("Return exactly this shape, with no other keys:");
            sb.AppendLine("{\"found\": true, \"sourceTerm\": \"...\", \"targetTerm\": \"...\", " +
                          "\"sourceAbbreviation\": \"...\", \"targetAbbreviation\": \"...\"}");
            sb.AppendLine();
            sb.AppendLine("=== SOURCE SEGMENT ===");
            sb.AppendLine(sourceSegment ?? "");
            sb.AppendLine();
            sb.AppendLine("=== TARGET SEGMENT ===");
            sb.Append(targetSegment ?? "");
            return sb.ToString();
        }

        /// <summary>
        /// Parses the model's JSON reply and validates every returned string
        /// against the segment it must have come from. Tolerant of surrounding
        /// prose and code fences. Never throws.
        ///
        /// A term that is not found verbatim in its segment invalidates the whole
        /// result – if the model paraphrased one side it cannot be trusted on the
        /// other. An abbreviation that is not found is dropped on its own, which
        /// is the common and harmless case (the model inferring "SFDR" for a side
        /// that does not spell it out).
        /// </summary>
        public static Result Parse(string response, string sourceSegment, string targetSegment)
        {
            var r = new Result();
            if (string.IsNullOrWhiteSpace(response))
            {
                r.Note = "empty reply";
                return r;
            }

            string json;
            try
            {
                var m = Regex.Match(response, @"\{.*\}", RegexOptions.Singleline);
                if (!m.Success) { r.Note = "no JSON object in reply"; return r; }
                json = m.Value;
            }
            catch { r.Note = "reply could not be scanned"; return r; }

            if (Regex.IsMatch(json, "\"found\"\\s*:\\s*false", RegexOptions.IgnoreCase))
            {
                r.Note = "model reported no term worth adding";
                return r;
            }

            var srcTerm = Field(json, "sourceTerm");
            var tgtTerm = Field(json, "targetTerm");
            if (string.IsNullOrWhiteSpace(srcTerm) || string.IsNullOrWhiteSpace(tgtTerm))
            {
                r.Note = "reply did not contain both terms";
                return r;
            }

            // Both terms must genuinely appear in their own segment.
            if (!AppearsIn(srcTerm, sourceSegment))
            {
                r.Note = "source term was not found verbatim in the source segment";
                return r;
            }
            if (!AppearsIn(tgtTerm, targetSegment))
            {
                r.Note = "target term was not found verbatim in the target segment";
                return r;
            }

            r.SourceTerm = srcTerm.Trim();
            r.TargetTerm = tgtTerm.Trim();

            // Abbreviations are dropped individually when unverifiable.
            var srcAbbr = Field(json, "sourceAbbreviation");
            var tgtAbbr = Field(json, "targetAbbreviation");
            if (!string.IsNullOrWhiteSpace(srcAbbr) && AppearsIn(srcAbbr, sourceSegment))
                r.SourceAbbreviation = srcAbbr.Trim();
            else if (!string.IsNullOrWhiteSpace(srcAbbr))
                r.Note = AppendNote(r.Note, "source abbreviation was not in the segment and was dropped");

            if (!string.IsNullOrWhiteSpace(tgtAbbr) && AppearsIn(tgtAbbr, targetSegment))
                r.TargetAbbreviation = tgtAbbr.Trim();
            else if (!string.IsNullOrWhiteSpace(tgtAbbr))
                r.Note = AppendNote(r.Note, "target abbreviation was not in the segment and was dropped");

            r.Found = true;
            return r;
        }

        /// <summary>
        /// Verbatim-appearance test, forgiving only about things that carry no
        /// meaning for a term: surrounding whitespace, runs of whitespace, case,
        /// and non-breaking vs ordinary spaces. A non-breaking space is treated as
        /// an ordinary one here deliberately – Studio shows the two identically,
        /// so requiring an exact match would reject correct extractions for a
        /// difference nobody can see.
        /// </summary>
        private static bool AppearsIn(string needle, string haystack)
        {
            if (string.IsNullOrWhiteSpace(needle) || string.IsNullOrEmpty(haystack)) return false;
            return Normalize(haystack).IndexOf(Normalize(needle),
                StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var swapped = s.Replace(' ', ' ').Replace(' ', ' ');
            return Regex.Replace(swapped, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Pulls a string field out of the JSON. Handles the escapes that can
        /// realistically occur inside a term (\" and \\); anything more exotic is
        /// not worth a JSON dependency here, and would fail the verbatim check
        /// harmlessly rather than producing a wrong entry.
        /// </summary>
        private static string Field(string json, string name)
        {
            try
            {
                var m = Regex.Match(json,
                    "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                    RegexOptions.IgnoreCase);
                if (!m.Success) return "";
                return m.Groups[1].Value
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }
            catch { return ""; }
        }

        private static string AppendNote(string existing, string add)
            => string.IsNullOrEmpty(existing) ? add : existing + "; " + add;

        private static string Safe(string s, string fallback)
            => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
    }
}
