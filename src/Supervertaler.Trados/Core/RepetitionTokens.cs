using System;
using System.Collections.Generic;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Supervertaler.Core;
using Supervertaler.Trados.Core.Export;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// A Trados source segment as core's <see cref="RepetitionKey"/> reads it, for
    /// keeping the repetitions of a Batch Translate run consistent
    /// (docs/design/consistent-repetitions.md).
    ///
    /// <para>Walks the segment exactly as <see cref="SegmentTagHandler.Serialize"/>
    /// does, so the tags line up with the <c>&lt;tN&gt;</c> numbers the AI sees:
    /// deleted text skipped, comments transparent, locked content and other
    /// containers as a pair. Built from the segment itself, not from the text
    /// sent - Batch Translate turns soft returns into spaces before sending, and
    /// the key must keep them apart.</para>
    ///
    /// <para>A tag's kind is its element type, the name Studio shows for it, and
    /// - for a formatting pair - whether it is bold, italic or underline. Never
    /// its id, and never its raw markup: in the files on Michael's computer
    /// (22 Oct 2026) the markup carried ids on 248 of 3,326 DOCX tags and an
    /// index on every Visio placeholder, which would have made almost every
    /// tagged segment unique. Two formatting pairs that differ only in colour
    /// or size share a kind; that is safe, because each repetition is rebuilt
    /// from its own tags, by position.</para>
    /// </summary>
    internal static class RepetitionTokens
    {
        /// <summary>The segment's match key, or null when it has nothing to group by.</summary>
        public static string KeyOf(ISegment source)
        {
            if (source == null) return null;

            var tokens = new List<RepetitionToken>();
            Walk(source, tokens);
            var key = RepetitionKey.Of(tokens);
            return key.Length == 0 ? null : key;
        }

        private static void Walk(IAbstractMarkupDataContainer container, List<RepetitionToken> tokens)
        {
            foreach (var item in container)
            {
                if (item is IText text)
                {
                    tokens.Add(RepetitionToken.Text(text.Properties.Text));
                }
                else if (item is ITagPair pair)
                {
                    var kind = PairKind(pair);
                    tokens.Add(RepetitionToken.Open(kind));
                    Walk(pair, tokens);
                    tokens.Add(RepetitionToken.Close(kind));
                }
                else if (item is IPlaceholderTag placeholder)
                {
                    tokens.Add(RepetitionToken.Standalone("ph:" + DisplayText(placeholder)));
                }
                else if (item is IRevisionMarker revision)
                {
                    if (revision.Properties.RevisionType != RevisionType.Delete)
                        Walk(revision, tokens);
                }
                else if (item is ICommentMarker comment)
                {
                    Walk(comment, tokens);
                }
                else if (item is IAbstractMarkupDataContainer nested)
                {
                    var kind = "container:" + item.GetType().Name;
                    tokens.Add(RepetitionToken.Open(kind));
                    Walk(nested, tokens);
                    tokens.Add(RepetitionToken.Close(kind));
                }
                else
                {
                    tokens.Add(RepetitionToken.Standalone("other:" + item.GetType().Name));
                }
            }
        }

        private static string PairKind(ITagPair pair)
        {
            string semantic = null;
            try
            {
                semantic = BilingualTagNamer.DetectSemantic(new TagInfo { TagType = TagType.Paired, OriginalMarkup = pair });
            }
            catch
            {
                // Read-only probing of Studio's formatting; the display name still decides.
            }

            var kind = "pair:" + DisplayText(pair);
            return semantic == null ? kind : kind + "/" + semantic;
        }

        private static string DisplayText(ITagPair pair)
        {
            try { return pair.StartTagProperties?.DisplayText ?? ""; }
            catch { return ""; }
        }

        private static string DisplayText(IPlaceholderTag placeholder)
        {
            try { return placeholder.Properties?.DisplayText ?? ""; }
            catch { return ""; }
        }
    }
}
