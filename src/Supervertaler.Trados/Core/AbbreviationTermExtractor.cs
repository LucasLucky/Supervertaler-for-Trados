using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// AI-assisted extraction of a term that is written out in full alongside its
    /// abbreviation, from a single source/target segment pair. Backs the
    /// "Add term with abbreviation" action.
    ///
    /// The manual flow this replaces: the translator spots a concept spelled out
    /// with its abbreviation in brackets – "Sustainable Finance Disclosure
    /// Regulation (SFDR, Verordening (EU) 2019/2088)" – selects the term on both
    /// sides, opens the term entry dialog and types the abbreviation into both
    /// abbreviation fields by hand.
    ///
    /// SCOPE: an abbreviation must actually be present. This deliberately does
    /// NOT extract ordinary term pairs. An abbreviation is a strong objective
    /// anchor – someone bothered to abbreviate the term, which says
    /// unambiguously which span matters. Without one, "the most useful term" is
    /// guesswork, and the translator already has Alt+Down and Ctrl+Alt+T, both of
    /// which use their own selection exactly; guessing could only diverge from
    /// what they picked. When no abbreviation is found the caller falls back to
    /// those rather than offering a term nobody asked for.
    ///
    /// SAFETY: every string the model returns is checked to be a verbatim
    /// substring of the segment it claims to come from (see <see cref="Parse"/>).
    /// That is what stops an invented abbreviation reaching the dialog: the model
    /// can only ever select text already on screen, never compose it. This
    /// matters because term pairs here are routinely identical across languages
    /// (SFDR, radar, transponder), so a human confirming a pre-filled dialog
    /// cannot reliably tell an invented abbreviation from a real one.
    ///
    /// This class only builds the prompt and parses the reply; the LLM call
    /// itself is made by the caller via its LlmClient, mirroring
    /// <see cref="DocumentContextClassifier"/>.
    /// </summary>
    internal static class AbbreviationTermExtractor
    {
        public const string SystemPrompt =
            "You find a term that appears in full alongside its abbreviation, within " +
            "one source/target segment pair, for a professional translator's termbase. " +
            "You only ever copy text that already appears in the segments – you never " +
            "translate, paraphrase, expand or invent. Reply with ONLY a JSON object, " +
            "no prose, no code fences.";

        /// <summary>Result of a parsed extraction. All fields are "" when absent.</summary>
        internal sealed class Result
        {
            /// <summary>
            /// False when no abbreviated term was found, or the reply failed
            /// validation. The caller falls back to the plain selection.
            /// </summary>
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
        /// <paramref name="targetSelection"/> are binding when present: the
        /// translator has said which concept they mean, so the model's job is to
        /// complete it to the full term and find its abbreviation, not to choose
        /// a different term it finds more interesting. <see cref="Parse"/>
        /// enforces this rather than trusting it.
        /// </summary>
        public static string BuildUserPrompt(
            string sourceSegment, string targetSegment,
            string sourceLang, string targetLang,
            string sourceSelection = null, string targetSelection = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Find the term in the segment pair below that is accompanied by an");
            sb.AppendLine("abbreviation, and return the term together with that abbreviation.");
            sb.AppendLine();
            sb.AppendLine($"Source language: {Safe(sourceLang, "the source language")}");
            sb.AppendLine($"Target language: {Safe(targetLang, "the target language")}");
            sb.AppendLine();
            sb.AppendLine("An abbreviation here means a short form appearing in the SAME segment as");
            sb.AppendLine("the full term, normally in brackets straight after it – for example");
            sb.AppendLine("\"Sustainable Finance Disclosure Regulation (SFDR)\", where SFDR is the");
            sb.AppendLine("abbreviation and the regulation name is the term.");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("1. \"sourceTerm\" MUST be copied character-for-character from the SOURCE segment.");
            sb.AppendLine("   \"targetTerm\" MUST be copied character-for-character from the TARGET segment.");
            sb.AppendLine("   Never translate, never rephrase, never correct spelling. Copy only.");
            sb.AppendLine("2. Abbreviations must likewise be copied verbatim from their own segment.");
            sb.AppendLine("   NEVER invent, guess, derive or translate one. If a side spells the term out");
            sb.AppendLine("   but gives no abbreviation for it, return \"\" for that side.");
            sb.AppendLine("3. Do NOT include in the term itself: the abbreviation, any brackets, any");
            sb.AppendLine("   surrounding punctuation, or a leading article/determiner.");
            sb.AppendLine("4. IF NO TERM IN THIS SEGMENT PAIR HAS AN ABBREVIATION, return {\"found\": false}.");
            sb.AppendLine("   Do not fall back to some other term. A term without an abbreviation is not");
            sb.AppendLine("   wanted here, and returning one is worse than returning nothing.");
            sb.AppendLine("5. The same abbreviation often serves both languages (e.g. an EU regulation");
            sb.AppendLine("   name). That is fine – but only report it on a side where it really appears.");
            sb.AppendLine();

            bool haveHint = !string.IsNullOrWhiteSpace(sourceSelection)
                         || !string.IsNullOrWhiteSpace(targetSelection);
            if (haveHint)
            {
                sb.AppendLine("The translator has selected the text below, which tells you WHICH concept");
                sb.AppendLine("they mean. The term you return must be that concept – complete it to the");
                sb.AppendLine("full term where the selection is only a fragment, but do not return a");
                sb.AppendLine("different term from elsewhere in the segment. If that concept has no");
                sb.AppendLine("abbreviation, return {\"found\": false} rather than switching to one that has.");
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
        /// Parses the model's JSON reply and validates it. Tolerant of surrounding
        /// prose and code fences. Never throws.
        ///
        /// Three things must hold, or the result is discarded and the caller falls
        /// back to the plain selection:
        ///  1. Both terms appear verbatim in their own segment. If the model
        ///     paraphrased one side it cannot be trusted on the other, so a
        ///     failure here invalidates everything.
        ///  2. Where the translator had a selection, the returned term overlaps
        ///     it. The selection says which concept they meant; a term from
        ///     elsewhere in the segment is not an answer to their request.
        ///  3. At least one abbreviation survived the same verbatim check – that
        ///     is the whole point of this action.
        /// An abbreviation that fails the verbatim check is dropped on its own,
        /// which is the common benign case (the model inferring an abbreviation
        /// for a side that does not spell one out).
        /// </summary>
        public static Result Parse(string response, string sourceSegment, string targetSegment,
            string sourceSelection = null, string targetSelection = null)
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
                r.Note = "no abbreviated term in this segment";
                return r;
            }

            var srcTerm = Field(json, "sourceTerm");
            var tgtTerm = Field(json, "targetTerm");
            if (string.IsNullOrWhiteSpace(srcTerm) || string.IsNullOrWhiteSpace(tgtTerm))
            {
                r.Note = "reply did not contain both terms";
                return r;
            }

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

            // The selection is the translator's statement of which concept they
            // meant, so it binds. Checked per side, only where that side had one.
            if (!string.IsNullOrWhiteSpace(sourceSelection) && !Overlaps(srcTerm, sourceSelection))
            {
                r.Note = "source term did not overlap the selection";
                return r;
            }
            if (!string.IsNullOrWhiteSpace(targetSelection) && !Overlaps(tgtTerm, targetSelection))
            {
                r.Note = "target term did not overlap the selection";
                return r;
            }

            var srcAbbr = Field(json, "sourceAbbreviation");
            var tgtAbbr = Field(json, "targetAbbreviation");
            if (!string.IsNullOrWhiteSpace(srcAbbr) && AppearsIn(srcAbbr, sourceSegment))
                r.SourceAbbreviation = srcAbbr.Trim();
            if (!string.IsNullOrWhiteSpace(tgtAbbr) && AppearsIn(tgtAbbr, targetSegment))
                r.TargetAbbreviation = tgtAbbr.Trim();

            // No surviving abbreviation means this is an ordinary term pair, which
            // this action deliberately does not handle.
            if (string.IsNullOrEmpty(r.SourceAbbreviation) && string.IsNullOrEmpty(r.TargetAbbreviation))
            {
                r.Note = string.IsNullOrWhiteSpace(srcAbbr) && string.IsNullOrWhiteSpace(tgtAbbr)
                    ? "term has no abbreviation"
                    : "the proposed abbreviation was not in the segment";
                return r;
            }

            r.SourceTerm = srcTerm.Trim();
            r.TargetTerm = tgtTerm.Trim();
            r.Found = true;
            return r;
        }

        /// <summary>
        /// True when the term and the selection refer to the same stretch of text:
        /// either the translator selected part of the term, or selected more than
        /// the term (a whole phrase, or the segment). Both mean "this is the
        /// concept I meant"; only a term from somewhere else entirely fails.
        /// </summary>
        private static bool Overlaps(string term, string selection)
        {
            var t = Normalize(term);
            var s = Normalize(selection);
            if (t.Length == 0 || s.Length == 0) return true; // nothing to contradict
            return t.IndexOf(s, StringComparison.CurrentCultureIgnoreCase) >= 0
                || s.IndexOf(t, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        /// <summary>
        /// Verbatim-appearance test, forgiving only about things that carry no
        /// meaning for a term: surrounding whitespace, runs of whitespace, case,
        /// and non-breaking vs ordinary spaces. Studio shows the two kinds of
        /// space identically, so demanding an exact match would reject correct
        /// extractions over a difference nobody can see.
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
            // \s in .NET matches Unicode whitespace, which includes U+00A0
            // (non-breaking) and U+202F (narrow no-break), so collapsing runs of
            // whitespace to one ordinary space makes the kinds compare equal.
            return Regex.Replace(s, @"\s+", " ").Trim();
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

        private static string Safe(string s, string fallback)
            => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
    }
}
