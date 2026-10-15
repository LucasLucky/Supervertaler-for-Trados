using System;
using System.Collections.Generic;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The tag-id comparison behind update_segments' post-write audit and the
    /// document-wide check_tags pass: which underlying Trados tag ids a target
    /// carries, compared against its source's. The same comparison Studio's Tag
    /// Verifier makes. Pure functions over a segment, kept out of the view part
    /// so they can be checked offline (.dev/tracked-merge-test).
    /// </summary>
    internal static class TagIdAudit
    {
        /// <summary>Depth-first list of the underlying Trados tag ids in a segment,
        /// in document order, including duplicates and leaving out tracked
        /// deletions. Tags whose id cannot be read are skipped rather than guessed
        /// at.</summary>
        public static List<string> CollectTagIds(IAbstractMarkupDataContainer container)
        {
            if (container == null) return null;
            var ids = new List<string>();
            CollectTagIdsInto(container, ids);
            return ids;
        }

        private static void CollectTagIdsInto(IAbstractMarkupDataContainer container, List<string> ids)
        {
            foreach (var item in container)
            {
                // A tracked deletion is not in the target the translator is
                // making - and SegmentTagHandler.Serialize, which gives the tag
                // COUNT this list is checked beside, already leaves it out.
                // Counting its tags here flagged every whole-segment rewrite with
                // Track Changes on as "target has 'Bold', which the source does
                // not have" - the struck-out old target's tag - until the
                // revisions were accepted.
                if (item is IRevisionMarker revision
                    && revision.Properties?.RevisionType == RevisionType.Delete)
                    continue;

                if (item is ITagPair pair)
                {
                    var id = SafeTagId(pair.StartTagProperties);
                    if (id != null) ids.Add(id);
                    CollectTagIdsInto(pair, ids);
                }
                else if (item is IPlaceholderTag ph)
                {
                    var id = SafeTagId(ph.Properties);
                    if (id != null) ids.Add(id);
                }
                else if (item is IAbstractMarkupDataContainer nested)
                {
                    CollectTagIdsInto(nested, ids);
                }
            }
        }

        /// <summary>The underlying Trados tag id, or null when it is blank.</summary>
        /// <remarks>
        /// Typed rather than reflective on purpose. Reflection here would degrade to
        /// "no audit" if the SDK ever moved TagId — that is, the check would go quiet
        /// and keep reporting success, which is the one failure mode a checker must
        /// not have. A compile error is the loud, cheap alternative.
        /// </remarks>
        private static string SafeTagId(
            Sdl.FileTypeSupport.Framework.NativeApi.IAbstractTagProperties tagProperties)
        {
            var id = tagProperties?.TagId.Id;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>
        /// The multiset comparison behind the write audit and the document-wide
        /// check_tags pass: compares how many times each underlying tag id occurs,
        /// not merely which ids occur, because the canonical failure is one id
        /// appearing twice while another goes missing – which a set comparison
        /// calls clean. Returns null when the multisets agree (or either list is
        /// unavailable), otherwise a description without trailing punctuation so
        /// callers can append their own context.
        /// </summary>
        public static string DescribeListMismatch(List<string> src, List<string> tgt)
        {
            if (src == null || tgt == null) return null;

            var srcCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in src)
                srcCounts[id] = (srcCounts.TryGetValue(id, out var c) ? c : 0) + 1;

            var tgtCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in tgt)
                tgtCounts[id] = (tgtCounts.TryGetValue(id, out var c) ? c : 0) + 1;

            var duplicated = new List<string>();
            var missing = new List<string>();

            foreach (var kv in tgtCounts)
            {
                srcCounts.TryGetValue(kv.Key, out var inSource);
                if (kv.Value > inSource)
                    duplicated.Add(kv.Key + (kv.Value > 1 ? $" (×{kv.Value})" : ""));
            }
            foreach (var kv in srcCounts)
            {
                tgtCounts.TryGetValue(kv.Key, out var inTarget);
                if (inTarget < kv.Value)
                    missing.Add(kv.Key);
            }

            if (duplicated.Count == 0 && missing.Count == 0) return null;

            var parts = new List<string>();
            if (duplicated.Count > 0)
                parts.Add("tag id(s) in the target that the source does not have (or has fewer of): "
                          + string.Join(", ", duplicated));
            if (missing.Count > 0)
                parts.Add("source tag id(s) missing from the target: " + string.Join(", ", missing));

            return "tag-id mismatch – " + string.Join("; ", parts);
        }
    }
}
