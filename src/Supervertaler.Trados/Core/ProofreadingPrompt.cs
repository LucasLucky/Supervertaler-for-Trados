using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Supervertaler.Core;
using Supervertaler.Trados.Models;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Builds system and user prompts for AI proofreading, and parses batch responses.
    /// Parallel to TranslationPrompt – uses the same composable prompt assembly pattern.
    /// </summary>
    public static class ProofreadingPrompt
    {
        /// <summary>
        /// Builds the full system prompt for proofreading by composing:
        /// Base proofreading instructions -> Language-specific checks -> Termbase -> Custom prompt
        /// -> SuperMemory -> Document -> Output contract.
        /// </summary>
        /// <param name="sourceLang">Source language display name</param>
        /// <param name="targetLang">Target language display name</param>
        /// <param name="terms">Optional termbase terms to check against</param>
        /// <param name="customPromptContent">Optional custom prompt content (already variable-substituted)</param>
        /// <param name="kbContext">Optional SuperMemory block, as Batch Translate sends it
        /// (<see cref="TranslationBankContext"/>); null or blank sends none</param>
        public static string BuildSystemPrompt(string sourceLang, string targetLang,
            List<TermEntry> terms = null, string customPromptContent = null,
            List<(string source, string target)> documentSegments = null,
            bool includeTermMetadata = true,
            string kbContext = null)
        {
            var hasBank = !string.IsNullOrWhiteSpace(kbContext);
            var sb = new StringBuilder(4096);

            // Persona and task
            sb.AppendLine("You are an expert " + sourceLang + " to " + targetLang +
                " translation quality reviewer with deep understanding of both languages.");
            sb.AppendLine();
            sb.AppendLine("**YOUR TASK**: Review the source-target segment pairs below and identify quality issues in the translations.");
            sb.AppendLine();

            // Quality check categories
            sb.AppendLine("**CHECK FOR THE FOLLOWING ISSUES**:");
            sb.AppendLine("1. **Accuracy** \u2013 Does the translation convey the same meaning as the source?");
            sb.AppendLine("2. **Completeness** \u2013 Is anything missing or added that shouldn't be?");
            sb.AppendLine("3. **Terminology** \u2013 Are terms translated consistently and correctly?");
            sb.AppendLine("4. **Grammar & Style** \u2013 Is the target grammatically correct and stylistically appropriate?");
            sb.AppendLine("5. **Number Formatting** \u2013 Are numbers, dates, and measurements formatted correctly for the target language?");
            sb.AppendLine();

            // Language-specific checks
            AppendLanguageSpecificChecks(sb, targetLang);

            // Rules. The reply's format is NOT here: it is the output contract at the
            // very end (OutputContract.ProofreadText, core), where a custom prompt
            // cannot override it. It was once stated here and again in the clipboard
            // request, the two drifted, and a model obeying the later one dropped
            // Evidence: - so it is written down once.
            sb.AppendLine("**IMPORTANT RULES**:");
            sb.AppendLine("- Answer in the format set out in the OUTPUT CONTRACT at the end of these instructions.");
            sb.AppendLine("- Be concise but specific in your descriptions.");
            sb.AppendLine("- Do NOT flag stylistic preferences unless they are clear errors.");

            // How a proofreader treats the memory bank: as the client's instructions,
            // which the translation is checked AGAINST, the way it is checked against
            // the termbase. A departure from a rule there is an error even where it
            // would otherwise pass as style - the rule above this one would let it
            // through - because the client or the translator decided it. The Issue
            // line names the rule, so the translator can find it and judge it. And a
            // point the bank says nothing about is not an error on the bank's
            // account: without that, a model reads a silent bank as a rule broken.
            if (hasBank)
                sb.AppendLine("- The # SUPERMEMORY section below holds the client's decisions and the translator's standing rules for this work. Check every translation against it as you do against the termbase: a translation that departs from a rule, term decision or style instruction there is an ISSUE even where it would otherwise pass as a matter of style. Name the rule it breaks in the Issue line. Where the memory bank says nothing about a point, it gives no grounds for an issue.");

            sb.AppendLine("- Segment numbers in your verdicts and any Evidence citations refer to the document-absolute numbers shown in [SEGMENT XXXX] headers and in # DOCUMENT CONTENT \u2013 NOT to within-batch positions.");

            // The batch carries each segment's inline tags (BatchProofreader,
            // SegmentTagHandler.ToProofreadText), so that a Corrected line comes
            // back with them and can be written into the segment. Without this the
            // model was shown the tags only in # DOCUMENT CONTENT, and a correction
            // of "WARNING!" would have lost the bold around it.
            sb.AppendLine("- Segments show their inline tags as numbered placeholders: <t1>...</t1> around formatted text, <t2/> for a standalone code. A tag in a target has the number of the source tag it stands for. Tags are formatting, not words: report one only when it is missing or wraps the wrong words.");

            // Termbase injection
            if (terms != null && terms.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("# TERMBASE");
                sb.AppendLine();
                sb.AppendLine("Check that these approved terms are used correctly in the translations:");
                sb.AppendLine();
                foreach (var term in terms)
                {
                    if (string.IsNullOrEmpty(term.SourceTerm) || string.IsNullOrEmpty(term.TargetTerm))
                        continue;

                    if (term.Forbidden)
                        sb.AppendLine("- " + term.SourceTerm + " \u2192 \u26A0\uFE0F DO NOT USE: " + term.TargetTerm);
                    else if (term.IsNonTranslatable)
                        sb.AppendLine("- " + term.SourceTerm + " \u2192 " + term.TargetTerm + " (do not translate)");
                    else
                        sb.AppendLine("- " + term.SourceTerm + " \u2192 " + term.TargetTerm);

                    // Include term metadata (domain, definition, notes)
                    if (includeTermMetadata)
                    {
                        if (!string.IsNullOrWhiteSpace(term.Domain))
                            sb.Append("  Domain: ").AppendLine(term.Domain);
                        if (!string.IsNullOrWhiteSpace(term.Definition))
                            sb.Append("  Definition: ").AppendLine(term.Definition);
                        if (!string.IsNullOrWhiteSpace(term.Notes))
                            sb.Append("  Notes: ").AppendLine(term.Notes);
                    }

                    // Pipe-separated variants are expanded rather than emitted raw \u2013
                    // see the matching block in TranslationPrompt for the rationale.
                    // All source spellings are listed so the proofreader recognises
                    // each of them; one canonical target form is named as correct.
                    var srcAbbrVariants = term.GetSourceAbbreviationVariants();
                    var primaryTgtAbbr = term.PrimaryTargetAbbreviation;
                    if (srcAbbrVariants.Length > 0 && !string.IsNullOrEmpty(primaryTgtAbbr))
                        sb.AppendLine("- " + string.Join(", ", srcAbbrVariants) + " \u2192 " + primaryTgtAbbr + " (abbreviation of: " + term.SourceTerm + ")");
                }
            }

            // Custom prompt
            if (!string.IsNullOrWhiteSpace(customPromptContent))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("# CUSTOM INSTRUCTIONS");
                sb.AppendLine();
                sb.Append(customPromptContent);
            }

            // SuperMemory, after the custom prompt as on the translation side
            // (TranslationPrompt.BuildSystemPrompt). In the system prompt, which is
            // the same for every batch of a run, so the bank is read from the
            // provider's prompt cache after the first request rather than paid for
            // in full every time.
            if (hasBank)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(kbContext.TrimEnd());
            }

            // Document context – full bilingual document (source + target) with no truncation.
            // Segment numbers here are document-absolute and match the [SEGMENT XXXX] numbers
            // used in the user-prompt batch, so the model can cross-reference both directions.
            if (documentSegments != null && documentSegments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("# DOCUMENT CONTENT");
                sb.AppendLine("The following is the FULL document with both source and target text for every segment.");
                sb.AppendLine("Use it to verify any claim about consistency, recurring terminology, or whether a");
                sb.AppendLine("variant appears elsewhere – both on the source side AND the target side – before");
                sb.AppendLine("flagging an issue. Do not raise a consistency concern you have not confirmed against");
                sb.AppendLine("this document.");
                sb.AppendLine();
                sb.AppendLine("Segment numbers below are document-absolute and match the [SEGMENT XXXX] numbers in");
                sb.AppendLine("the batch you are reviewing. An empty Target line means that segment has not yet");
                sb.AppendLine("been translated.");
                sb.AppendLine();

                for (int i = 0; i < documentSegments.Count; i++)
                {
                    var pair = documentSegments[i];
                    sb.Append("[SEGMENT ").Append((i + 1).ToString("D4")).AppendLine("]");
                    sb.Append("Source: ").AppendLine(pair.source ?? "");
                    sb.Append("Target: ").AppendLine(pair.target ?? "");
                    sb.AppendLine();
                }
            }

            // Last, after the custom prompt and the document, as on the translation
            // side (BatchTranslator.SystemPromptFor).
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(OutputContract.ProofreadText);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Appends language-specific quality checks based on the target language.
        /// </summary>
        private static void AppendLanguageSpecificChecks(StringBuilder sb, string targetLang)
        {
            if (string.IsNullOrEmpty(targetLang))
                return;

            var lower = targetLang.ToLowerInvariant();

            if (lower.Contains("dutch") || lower.Contains("nederland") || lower.Contains("flemish") || lower.Contains("vlaams"))
            {
                sb.AppendLine("**DUTCH-SPECIFIC CHECKS**:");
                sb.AppendLine("- Compound words: check that compound nouns are written as one word (e.g., 'softwareontwikkeling', not 'software ontwikkeling').");
                sb.AppendLine("- dt-errors: verify correct conjugation of verbs ending in -d, -t, or -dt.");
                sb.AppendLine("- de/het: check correct article usage (de/het).");
                sb.AppendLine();
            }
            else if (lower.Contains("german") || lower.Contains("deutsch"))
            {
                sb.AppendLine("**GERMAN-SPECIFIC CHECKS**:");
                sb.AppendLine("- Compound nouns: check that compound nouns are written as one word (e.g., 'Softwareentwicklung', not 'Software Entwicklung').");
                sb.AppendLine("- Capitalization: all nouns must be capitalized.");
                sb.AppendLine("- Cases: check correct use of nominative, accusative, dative, and genitive cases.");
                sb.AppendLine();
            }
            else if (lower.Contains("french") || lower.Contains("fran\u00E7ais"))
            {
                sb.AppendLine("**FRENCH-SPECIFIC CHECKS**:");
                sb.AppendLine("- Accents: verify correct use of accents (\u00E9, \u00E8, \u00EA, \u00EB, \u00E0, \u00E2, etc.).");
                sb.AppendLine("- Gender/number agreement: check that adjectives and past participles agree with their nouns.");
                sb.AppendLine("- Punctuation spacing: ensure spaces before colons, semicolons, question marks, and exclamation marks.");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Builds the user prompt with numbered source-target segment pairs for batch proofreading.
        /// </summary>
        public static string BuildBatchUserPrompt(List<(int number, string source, string target)> segments)
        {
            var sb = new StringBuilder(segments.Count * 300);

            sb.AppendLine("**SEGMENTS TO REVIEW (" + segments.Count + " segments):**");
            sb.AppendLine();

            foreach (var seg in segments)
            {
                sb.AppendLine("[SEGMENT " + seg.number.ToString("D4") + "]");
                sb.AppendLine("Source: " + seg.source);
                sb.AppendLine("Target: " + seg.target);
                sb.AppendLine();
            }

            sb.Append("**YOUR REVIEW (one verdict per segment):**");

            return sb.ToString();
        }

        /// <summary>
        /// Parses a batch proofreading response into structured results.
        /// Tolerant of formatting variations in AI output.
        /// </summary>
        /// <param name="response">Raw AI response text</param>
        /// <param name="batchStartNumber">1-based number of the first segment in this batch</param>
        /// <param name="batchSize">Number of segments expected in this batch</param>
        /// <returns>List of parsed results. <c>corrected</c> is the Corrected
        /// line as written (several lines joined with LF), or null; it is read
        /// only on an ISSUE verdict.</returns>
        public static List<(int segmentNumber, bool isOk, string issueDescription, string suggestion, string evidence, string corrected)>
            ParseBatchResponse(string response, int batchStartNumber, int batchSize)
        {
            var results = new List<(int, bool, string, string, string, string)>();
            if (string.IsNullOrWhiteSpace(response))
                return results;

            var lines = response.Split(new[] { '\n' }, StringSplitOptions.None);

            // Pattern to match segment header: [SEGMENT 0001] OK/ISSUE
            var segmentPattern = new Regex(
                @"^\s*\[SEGMENT\s+(\d+)\]\s*(OK|ISSUE|PASS|WARNING|FAIL|\u2713|\u2717|\u26A0)",
                RegexOptions.IgnoreCase);
            // Pattern to match Issue: line
            var issuePattern = new Regex(@"^\s*Issue\s*:\s*(.+)", RegexOptions.IgnoreCase);
            // Pattern to match Suggestion: line
            var suggestionPattern = new Regex(@"^\s*Suggestion\s*:\s*(.+)", RegexOptions.IgnoreCase);
            // Pattern to match Evidence: line (optional \u2014 only used for issues citing other segments)
            var evidencePattern = new Regex(@"^\s*Evidence\s*:\s*(.+)", RegexOptions.IgnoreCase);
            // The corrected translation (core OutputContract.ProofreadText). Its text
            // starts after the colon, spaces trimmed, and may be empty on this line.
            var correctedPattern = new Regex(@"^\s*Corrected\s*:[ \t]*(.*)$", RegexOptions.IgnoreCase);

            int currentNumber = -1;
            bool currentIsOk = true;
            string currentIssue = null;
            string currentSuggestion = null;
            string currentEvidence = null;

            // The Corrected line comes last and keeps a translation's own line
            // breaks, so the lines after it belong to it until the next segment
            // header or a labelled line, which closes it without discarding it.
            // Null while the verdict has none.
            List<string> corrected = null;
            bool correctedOpen = false;

            string Corrected()
            {
                if (corrected == null) return null;
                // Blank lines between this verdict and the next are not the translation's.
                int end = corrected.Count;
                while (end > 0 && corrected[end - 1].Trim().Length == 0) end--;
                var text = string.Join("\n", corrected.Take(end));
                return text.Trim().Length == 0 ? null : text;
            }

            void Flush()
            {
                if (currentNumber >= 0)
                    results.Add((currentNumber, currentIsOk, currentIssue, currentSuggestion, currentEvidence,
                        currentIsOk ? null : Corrected()));
            }

            for (int i = 0; i < lines.Length; i++)
            {
                // A reply with CR LF line ends leaves a CR on every line; inside a
                // multi-line Corrected it would be written as a hard return.
                var line = lines[i].TrimEnd('\r');
                var segMatch = segmentPattern.Match(line);

                if (segMatch.Success)
                {
                    // Flush previous segment if any
                    Flush();

                    currentNumber = int.Parse(segMatch.Groups[1].Value);
                    var verdict = segMatch.Groups[2].Value.Trim().ToUpperInvariant();

                    currentIsOk = verdict == "OK" || verdict == "PASS" || verdict == "\u2713";
                    currentIssue = null;
                    currentSuggestion = null;
                    currentEvidence = null;
                    corrected = null;
                    correctedOpen = false;
                }
                else if (currentNumber >= 0)
                {
                    var issueMatch = issuePattern.Match(line);
                    if (issueMatch.Success)
                    {
                        currentIssue = issueMatch.Groups[1].Value.Trim();
                        correctedOpen = false;
                        continue;
                    }

                    var evMatch = evidencePattern.Match(line);
                    if (evMatch.Success)
                    {
                        currentEvidence = evMatch.Groups[1].Value.Trim();
                        correctedOpen = false;
                        continue;
                    }

                    var sugMatch = suggestionPattern.Match(line);
                    if (sugMatch.Success)
                    {
                        currentSuggestion = sugMatch.Groups[1].Value.Trim();
                        correctedOpen = false;
                        continue;
                    }

                    var corMatch = correctedPattern.Match(line);
                    if (corMatch.Success)
                    {
                        corrected = new List<string>();
                        correctedOpen = true;
                        if (corMatch.Groups[1].Value.Length > 0)
                            corrected.Add(corMatch.Groups[1].Value);
                        continue;
                    }

                    // A line of the translation the Corrected line holds.
                    if (correctedOpen && (corrected.Count > 0 || line.Trim().Length > 0))
                        corrected.Add(line);
                }
            }

            // Flush last segment
            Flush();

            return results;
        }
    }
}
