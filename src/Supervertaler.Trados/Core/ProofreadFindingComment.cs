using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Supervertaler.Trados.Models;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The text half of "Also add issues as Trados comments": what a proofreading
    /// finding reads as when it is put on its segment as a Trados comment, and
    /// whether a segment already carries it. Needs no document, so it can be
    /// checked offline; adding the comment is the view part's.
    ///
    /// <para>The comment carries the issue and the suggestion, never the corrected
    /// translation: that is what Apply writes into the segment, and a reviser
    /// reading the comment next to the tracked change needs to know why, not the
    /// same words again.</para>
    /// </summary>
    internal static class ProofreadFindingComment
    {
        /// <summary>A label written by <see cref="Text"/>: "AI proofreader: " or
        /// "AI proofreader (Claude Opus 5.5): ". A model's own name may hold
        /// brackets ("Gemini 3.1 Pro (Preview)"), so the lazy match runs to the
        /// first "): ". One line only: the label never spans one.</summary>
        private static readonly Regex Label = new Regex(@"^AI proofreader(?: \(.*?\))?: ", RegexOptions.CultureInvariant);

        /// <summary>
        /// The comment for <paramref name="issue"/>, or null when it has nothing to
        /// say (a segment the proofreader passed, or a finding with no issue text).
        /// With <paramref name="withLabel"/> it starts "AI proofreader (model): " -
        /// the person who gets the file may not know the translator used an AI
        /// check - and without a model, "AI proofreader: ".
        ///
        /// <para>Line ends are LF: Studio keeps a comment in the SDLXLIFF, and XML
        /// turns CR LF into LF on the next open - so a comment written with CR LF
        /// would no longer read as the same comment after a save.</para>
        /// </summary>
        public static string Text(ProofreadingIssue issue, string model, bool withLabel)
        {
            if (issue == null || issue.IsOk) return null;
            var description = Clean(issue.IssueDescription);
            if (description.Length == 0) return null;

            var text = description;
            var suggestion = Clean(issue.Suggestion);
            if (suggestion.Length > 0) text += "\nSuggestion: " + suggestion;

            if (!withLabel) return text;
            model = Clean(model).Replace('\n', ' ');
            return (model.Length > 0 ? "AI proofreader (" + model + "): " : "AI proofreader: ") + text;
        }

        /// <summary>
        /// True when a segment whose comments read <paramref name="existing"/>
        /// should not be given <paramref name="text"/>: one comment from the
        /// proofreader per segment (Michael, 17 Oct 2026). So when it already
        /// carries
        /// <list type="bullet">
        /// <item>any comment with the proofreader's label - a later run words the
        /// same finding differently every time, so only "it is ours" catches it;
        /// a different issue found later stays on the Reports tab;</item>
        /// <item>or the same comment, compared without the label - which is all
        /// there is to go on for a comment added with the label switched off, and
        /// keeps a comment from counting as new because the label was switched
        /// off, or names the model, since it was added.</item>
        /// </list>
        /// This is also what stops Apply adding a second copy of the comment the
        /// run added. Asked of the segment itself rather than remembered: the
        /// document is what the reviser sees, and it survives a restart, an undo
        /// and a comment deleted by hand, which a flag would not.
        /// </summary>
        public static bool AlreadyOn(IEnumerable<string> existing, string text)
        {
            if (existing == null || text == null) return false;
            var wanted = Body(text);
            foreach (var e in existing)
            {
                var clean = Clean(e);
                if (Label.IsMatch(clean) || string.Equals(Body(clean), wanted, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>A comment's text without the label, if it has one.</summary>
        private static string Body(string comment) => Label.Replace(Clean(comment), "", 1).Trim();

        /// <summary>Line ends as LF, no blank edges.</summary>
        private static string Clean(string s) =>
            (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
}
