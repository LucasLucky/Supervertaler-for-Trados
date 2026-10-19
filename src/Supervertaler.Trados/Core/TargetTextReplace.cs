using System;
using System.Linq;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Find and replace in a segment's target as it reads with its tracked
    /// changes accepted: what the MCP server's find_and_replace and SuperSearch's
    /// Replace search, show and change. Text inside a tracked deletion is not
    /// searched, shown or changed. Until 201 both walked every container,
    /// deletions included, so a dry run's "before" ran deleted and inserted text
    /// together, a search found words the segment no longer reads, and a replace
    /// could edit text inside a deletion (found live on 19 Oct 2026).
    ///
    /// <para>A replace is made inside each text node of the accepted view, so
    /// tags, comments and tracked changes stay where they are. When that does
    /// not give the same text as replacing in the whole accepted text, the match
    /// runs from one node into the next - across a tag, a comment boundary or a
    /// tracked change, as in "foo" + deleted "bar" + "baz" searched for "foobaz" -
    /// and the segment is left for a manual edit
    /// (<see cref="Outcome.SpansMarkup"/>) rather than guessing which node
    /// should get the new text.</para>
    /// </summary>
    internal static class TargetTextReplace
    {
        internal enum Outcome
        {
            /// <summary>The accepted text does not change.</summary>
            NoMatch,
            /// <summary>Every match lies inside one text node: <see cref="Apply"/> gives <c>after</c>.</summary>
            Replaceable,
            /// <summary>A match runs across a tag, comment or tracked change, or
            /// there is no text node to put the result in.</summary>
            SpansMarkup
        }

        /// <summary>
        /// What a replace would do to <paramref name="target"/>, without changing
        /// it: the accepted text before and after, and whether
        /// <see cref="Apply"/> can make that change node by node.
        /// </summary>
        /// <param name="replace">The caller's replace, applied to a string (the
        /// whole accepted text, and each node's text).</param>
        public static Outcome Plan(IAbstractMarkupDataContainer target, Func<string, string> replace,
            out string before, out string after)
        {
            var texts = SegmentTagHandler.AcceptedTexts(target);
            before = string.Concat(texts.Select(t => t.Properties?.Text ?? ""));
            after = replace(before);
            if (after == before) return Outcome.NoMatch;
            if (texts.Count == 0) return Outcome.SpansMarkup;
            var nodeByNode = string.Concat(texts.Select(t => replace(t.Properties?.Text ?? "")));
            return nodeByNode == after ? Outcome.Replaceable : Outcome.SpansMarkup;
        }

        /// <summary>
        /// Replaces inside each text node of the accepted view, in place; deleted
        /// text is not touched. For use inside ProcessSegmentPair, on a target
        /// <see cref="Plan"/> found <see cref="Outcome.Replaceable"/>.
        /// </summary>
        public static void Apply(IAbstractMarkupDataContainer target, Func<string, string> replace)
        {
            foreach (var t in SegmentTagHandler.AcceptedTexts(target))
            {
                if (t.Properties == null) continue;
                var oldVal = t.Properties.Text ?? "";
                var newVal = replace(oldVal);
                if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                    t.Properties.Text = newVal;
            }
        }
    }
}
