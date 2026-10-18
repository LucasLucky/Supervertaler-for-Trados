using System;
using System.Collections.Generic;
using System.Text;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Rewrites a target as word-level tracked changes (issue #138): given the
    /// target as it was and the target as it should become, builds content that
    /// shows only what actually changed - a four-character insertion as a
    /// four-character insertion, not the whole sentence struck through and
    /// retyped.
    ///
    /// <para><b>Why this exists at all.</b> Studio's <c>ProcessSegmentPair</c>
    /// replaces the entire segment range with whatever the callback built, so
    /// with Track Changes on, every write is recorded as delete-everything plus
    /// insert-everything however small the change. No amount of care in the
    /// callback survives that. So the caller switches tracking off around the
    /// write, and the revisions a reviewer sees are the ones built here.</para>
    ///
    /// <para><b>Diff each text run separately, never across a tag.</b> Content is
    /// runs of text separated by tags. Each run is diffed on its own, so a
    /// revision can never straddle a tag boundary, tags are never wrapped, moved
    /// or duplicated, and a change that really does span a tag becomes two
    /// adjacent revisions - which is what Studio records when a person makes the
    /// same edit.</para>
    ///
    /// <para><b>Comments are transparent.</b> They wrap text rather than
    /// separating it, and an ordinary write re-anchors every comment over the
    /// whole new target. The merge builds on the OLD tree, so each comment keeps
    /// the span it had.</para>
    ///
    /// <para><b>Tags that changed are replaced, not revised.</b> A write often
    /// changes only a tag's id - <c>&lt;g id="Bold"&gt;</c> left by an earlier
    /// edit becomes the source's <c>&lt;g id="332"&gt;</c> - and both look the
    /// same in the editor, even in full tag view. Recorded as a revision, that
    /// is the whole tagged text struck out and retyped, or (before this) the
    /// whole segment, with nothing visible to say why. So the old target's tags
    /// are first made the new target's, in place and without a revision, and
    /// the text is diffed as usual afterwards: a tag swap alone records nothing,
    /// a tag swap plus a changed word records that word. Rejecting the
    /// revisions then gives the old text with the new tags.</para>
    ///
    /// <para>Only where the two targets line up unambiguously: the same tags in
    /// the same places with some ids changed (text edits allowed), or - with the
    /// text unchanged - whole tag pairs only added or only removed, placed by
    /// their exact character positions. And never when a tag moves: if an id
    /// leaves one place and turns up in another, rejecting the text revisions
    /// would leave formatting on the wrong words, so that is refused.
    /// Placeholders are only ever swapped like for like, never added or
    /// removed this way - those are content, not formatting.</para>
    ///
    /// <para><b>Refuses rather than guesses.</b> If the tags cannot be lined up
    /// that way, or the target already carries tracked changes, it returns a
    /// reason and changes nothing the caller will use. The caller then writes
    /// the way it always has, with tracking left on - so the worst case is
    /// today's behaviour.</para>
    ///
    /// <para><b>And it checks itself.</b> Before handing anything back it projects
    /// the result both ways: accepting every change must give exactly the new
    /// target, and rejecting every change exactly the old one (with any replaced
    /// tags, which no revision covers). If either fails,
    /// that is reported as a refusal like any other. A bug here therefore costs
    /// a coarse revision, not a corrupted segment.</para>
    /// </summary>
    internal static class TrackedTargetMerge
    {
        private sealed class Run
        {
            public readonly List<IText> Leaves = new List<IText>();
            public readonly StringBuilder Text = new StringBuilder();
            // Where an empty run sits, for inserting into it: before AnchorBefore
            // in AnchorContainer, or at the end when AnchorBefore is null.
            public IAbstractMarkupDataContainer AnchorContainer;
            public IAbstractMarkupData AnchorBefore;
        }

        private enum TokKind { Open, Close, Placeholder, Locked }

        /// <summary>One tag in a flattened target, in document order.</summary>
        private sealed class Tok
        {
            public TokKind Kind;
            public string Key;                // compared between targets: kind, id, locked text
            public string Id;                 // the tag id; null for locked content
            public IAbstractMarkupData Node;  // the pair (Open and Close), placeholder or locked content
            public int Index;                 // position in Flat.Tokens
            public int Offset;                // characters of text before it
        }

        private sealed class Flat
        {
            // Runs[i] is the text before Tokens[i]; the last run follows the last tag.
            public readonly List<Run> Runs = new List<Run>();
            public readonly List<Tok> Tokens = new List<Tok>();
            public string Refusal;
        }

        private const string TagsDiffer = "the inline tags differ between the old and new target";

        /// <summary>
        /// Rewrites <paramref name="oldTarget"/> in place so that it carries
        /// <paramref name="newTarget"/>'s text as revisions. Returns null on
        /// success, or the reason it could not - in which case
        /// <paramref name="oldTarget"/> may have been partly modified and must be
        /// discarded. <paramref name="newTarget"/> is only read.
        /// </summary>
        /// <param name="tagsReplaced">On success, how many tags were replaced,
        /// added or removed without a revision to make the old target's tags the
        /// new target's (a pair counts once).</param>
        /// <param name="textRevised">On success, whether any text revision was
        /// recorded.</param>
        public static string TryMerge(
            IAbstractMarkupDataContainer oldTarget,
            IAbstractMarkupDataContainer newTarget,
            IDocumentItemFactory factory,
            IRevisionProperties insertProps,
            IRevisionProperties deleteProps,
            out int tagsReplaced,
            out bool textRevised)
        {
            tagsReplaced = 0;
            textRevised = false;
            if (oldTarget == null || newTarget == null) return "no target to compare";
            if (factory == null || insertProps == null || deleteProps == null)
                return "revision markers could not be created";

            var before = Flatten(oldTarget);
            if (before.Refusal != null) return before.Refusal;
            var after = Flatten(newTarget);
            if (after.Refusal != null) return after.Refusal;

            if (!SameSkeleton(before.Tokens, after.Tokens))
            {
                string why;
                try { why = ReconcileTags(oldTarget, before, after, out tagsReplaced); }
                catch (Exception ex) { why = "replacing them failed (" + ex.Message + ")"; }
                if (why != null) return TagsDiffer + ": " + why;

                // Lined up from scratch rather than patched: the runs moved.
                before = Flatten(oldTarget);
                if (before.Refusal != null) return before.Refusal;
                if (!SameSkeleton(before.Tokens, after.Tokens))
                    return TagsDiffer + ": they could not be lined up";
            }

            // What the result must project to, taken before any revision is
            // built - and after the tags were lined up, since no revision covers
            // that, so rejecting everything keeps the replaced tags.
            var expectAccept = Project(newTarget, accept: true);
            var expectReject = Project(oldTarget, accept: false);

            try
            {
                for (int i = 0; i < before.Runs.Count; i++)
                {
                    var oldRun = before.Runs[i];
                    var newRun = after.Runs[i];
                    var oldText = oldRun.Text.ToString();
                    var newText = newRun.Text.ToString();
                    if (oldText == newText) continue;
                    textRevised = true;

                    var pieces = TokenDiff.Diff(oldText, newText);

                    if (oldRun.Leaves.Count > 0)
                    {
                        RewriteLeaves(oldRun, pieces, factory, insertProps, deleteProps);
                    }
                    else
                    {
                        // Nothing was there: a pure insertion, placed where the
                        // empty run sits. Its text node is modelled on the new
                        // run's, which is where this text came from.
                        var template = newRun.Leaves.Count > 0 ? newRun.Leaves[0] : null;
                        if (template == null) return "no text node to model an insertion on";
                        var marker = factory.CreateRevision(insertProps);
                        marker.Add(TextLike(template, newText));
                        InsertAt(oldRun.AnchorContainer, oldRun.AnchorBefore, marker);
                    }
                }
            }
            catch (Exception ex)
            {
                return "building the revisions failed: " + ex.Message;
            }

            // The round trip, per segment, on every write. Cheap string walks, and
            // the one check that proves nothing was dropped or duplicated.
            if (Project(oldTarget, accept: true) != expectAccept)
                return "self-check failed: accepting the changes would not give the new target";
            if (Project(oldTarget, accept: false) != expectReject)
                return "self-check failed: rejecting the changes would not give the old target";

            return null;
        }

        // ─── Flattening ──────────────────────────────────────────────

        private static Flat Flatten(IAbstractMarkupDataContainer root)
        {
            var flat = new Flat();
            var current = new Run();
            flat.Runs.Add(current);
            Walk(root, flat, ref current);
            if (flat.Refusal == null)
            {
                Close(current, root, null);
                int pos = 0;
                for (int i = 0; i < flat.Tokens.Count; i++)
                {
                    pos += flat.Runs[i].Text.Length;
                    flat.Tokens[i].Index = i;
                    flat.Tokens[i].Offset = pos;
                }
            }
            return flat;
        }

        private static void Close(Run run, IAbstractMarkupDataContainer container, IAbstractMarkupData before)
        {
            run.AnchorContainer = container;
            run.AnchorBefore = before;
        }

        private static void Boundary(Flat flat, ref Run current,
            IAbstractMarkupDataContainer container, IAbstractMarkupData before, Tok token)
        {
            Close(current, container, before);
            flat.Tokens.Add(token);
            current = new Run();
            flat.Runs.Add(current);
        }

        private static Tok Token(TokKind kind, string id, IAbstractMarkupData node, string key)
        {
            return new Tok { Kind = kind, Id = id, Node = node, Key = key };
        }

        // A revision can sit on a tag itself, not only around text. Treated
        // like any other existing revision: not edited into.
        private static bool Revised(IRevisionProperties p)
        {
            return p != null && p.RevisionType != RevisionType.Unchanged;
        }

        /// <summary>
        /// True when a tag in <paramref name="target"/> carries a revision of its
        /// own - one deleted or inserted with Track Changes on. Must be asked of
        /// the LIVE target, before it is cloned for the merge: Clone() keeps
        /// revision markers and a placeholder's revision, but drops a tag pair's
        /// StartTagRevisionProperties and EndTagRevisionProperties without a
        /// word (measured on Studio 2024's own implementation, 2026-10-15). The
        /// merge would never see such a revision, and would write the tag back
        /// as though nobody had touched it.
        /// </summary>
        public static bool HasTagRevisions(IAbstractMarkupDataContainer target)
        {
            if (target == null) return false;
            foreach (var item in target)
            {
                if (item is ITagPair pair
                    && (Revised(pair.StartTagRevisionProperties) || Revised(pair.EndTagRevisionProperties)))
                    return true;
                if (item is IPlaceholderTag ph && Revised(ph.RevisionProperties)) return true;
                if (item is IAbstractMarkupDataContainer nested && HasTagRevisions(nested)) return true;
            }
            return false;
        }

        private static void Walk(IAbstractMarkupDataContainer container, Flat flat, ref Run current)
        {
            // A snapshot: nothing is mutated during flattening, but a live
            // enumerator over a Studio container is not something to lean on.
            var items = new List<IAbstractMarkupData>(container);
            foreach (var item in items)
            {
                if (flat.Refusal != null) return;

                if (item is IText text)
                {
                    current.Leaves.Add(text);
                    current.Text.Append(text.Properties?.Text ?? "");
                }
                else if (item is IRevisionMarker)
                {
                    // Diffing against a target that already carries revisions
                    // would mean editing inside someone else's marks. Refused
                    // rather than flattened.
                    flat.Refusal = "the segment already carries tracked changes";
                }
                else if (item is ICommentMarker comment)
                {
                    Walk(comment, flat, ref current);   // transparent
                }
                else if (item is ITagPair pair)
                {
                    if (Revised(pair.StartTagRevisionProperties) || Revised(pair.EndTagRevisionProperties))
                    {
                        flat.Refusal = "the segment already carries tracked changes";
                        return;
                    }
                    var id = TagId(pair.StartTagProperties);
                    Boundary(flat, ref current, container, pair,
                        Token(TokKind.Open, id, pair, "<" + id + ">"));
                    Walk(pair, flat, ref current);
                    if (flat.Refusal != null) return;
                    Close(current, pair, null);
                    flat.Tokens.Add(Token(TokKind.Close, id, pair, "</" + id + ">"));
                    current = new Run();
                    flat.Runs.Add(current);
                }
                else if (item is IPlaceholderTag ph)
                {
                    if (Revised(ph.RevisionProperties))
                    {
                        flat.Refusal = "the segment already carries tracked changes";
                        return;
                    }
                    var id = TagId(ph.Properties);
                    Boundary(flat, ref current, container, ph,
                        Token(TokKind.Placeholder, id, ph, "<" + id + "/>"));
                }
                else if (item is ILockedContent locked)
                {
                    // Opaque: compared whole, never diffed into.
                    Boundary(flat, ref current, container, locked,
                        Token(TokKind.Locked, null, locked, "[locked:" + PlainText(locked.Content) + "]"));
                }
                else
                {
                    // Anything else - a sub-segment reference, a marker type this
                    // was not written for - is not guessed at.
                    flat.Refusal = "the segment contains content this cannot track ("
                                 + item.GetType().Name + ")";
                }
            }
        }

        private static string TagId(Sdl.FileTypeSupport.Framework.NativeApi.IAbstractTagProperties p)
        {
            // Typed, not reflective, for the same reason as the tag audit: if the
            // SDK moves this, the build should break rather than the check go
            // quiet. A blank id still has to take part in the comparison.
            var id = p?.TagId.Id;
            return string.IsNullOrEmpty(id) ? "?" : id;
        }

        private static bool SameSkeleton(List<Tok> a, List<Tok> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i].Key, b[i].Key, StringComparison.Ordinal)) return false;
            return true;
        }

        // ─── Lining up the tags ──────────────────────────────────────

        /// <summary>A tag pair as a span of text: where it opens and closes, in
        /// characters and in tokens, and which pair encloses it.</summary>
        private sealed class PairSpan
        {
            public Tok Open;
            public int OpenIndex, CloseIndex;
            public int Start, End;
            public int Parent = -1;   // index of the enclosing pair in the same list
            public int Match = -1;    // index of its counterpart in the other target's list
        }

        private static List<PairSpan> Pairs(List<Tok> tokens)
        {
            var list = new List<PairSpan>();
            var open = new Stack<int>();
            foreach (var t in tokens)
            {
                if (t.Kind == TokKind.Open)
                {
                    list.Add(new PairSpan
                    {
                        Open = t, OpenIndex = t.Index, Start = t.Offset,
                        Parent = open.Count > 0 ? open.Peek() : -1
                    });
                    open.Push(list.Count - 1);
                }
                else if (t.Kind == TokKind.Close)
                {
                    var p = list[open.Pop()];   // balanced: a pair is a container
                    p.CloseIndex = t.Index;
                    p.End = t.Offset;
                }
            }
            return list;
        }

        /// <summary>
        /// Makes the old target's tags the new target's, in place and without a
        /// revision, leaving its text and comments as they are. Returns null on
        /// success, otherwise why not - the old target may then be half done and
        /// is discarded by the caller.
        /// </summary>
        private static string ReconcileTags(IAbstractMarkupDataContainer oldRoot,
            Flat before, Flat after, out int replaced)
        {
            replaced = 0;
            var a = before.Tokens;
            var b = after.Tokens;
            var swaps = new List<KeyValuePair<Tok, Tok>>();

            // The same tags in the same places, some with another id: line them up
            // by position. The text may have changed as well - it is diffed after.
            if (SameShape(a, b))
            {
                for (int i = 0; i < a.Count; i++)
                    if ((a[i].Kind == TokKind.Open || a[i].Kind == TokKind.Placeholder)
                        && !string.Equals(a[i].Id, b[i].Id, StringComparison.Ordinal))
                        swaps.Add(new KeyValuePair<Tok, Tok>(a[i], b[i]));
                var moved = MovedTag(swaps, null, null);
                if (moved != null) return moved;
                foreach (var s in swaps) Swap(s.Key.Node, s.Value.Node);
                replaced = swaps.Count;
                return null;
            }

            // A different number of tags is only taken on when nothing else
            // changed. With identical text every tag has an exact character
            // position in both targets, so there is no guessing which words an
            // added tag belongs around; with the text changing too, there would be.
            if (PlainText(before) != PlainText(after))
                return "tags were added or removed and the text changed as well";

            // Placeholders and locked content must pair off one for one: adding
            // or removing one changes the content, not its formatting.
            var aOther = a.FindAll(t => t.Kind == TokKind.Placeholder || t.Kind == TokKind.Locked);
            var bOther = b.FindAll(t => t.Kind == TokKind.Placeholder || t.Kind == TokKind.Locked);
            if (aOther.Count != bOther.Count) return "a placeholder tag was added or removed";
            for (int i = 0; i < aOther.Count; i++)
            {
                var x = aOther[i];
                var y = bOther[i];
                if (x.Kind != y.Kind || x.Offset != y.Offset) return "tags were moved or reordered";
                if (x.Kind == TokKind.Locked)
                {
                    if (x.Key != y.Key) return "locked content differs";
                }
                else if (!string.Equals(x.Id, y.Id, StringComparison.Ordinal))
                    swaps.Add(new KeyValuePair<Tok, Tok>(x, y));
            }

            // Tag pairs pair off by the exact text they cover; several over the
            // same text pair off outermost first, as both targets list them.
            var ap = Pairs(a);
            var bp = Pairs(b);
            for (int j = 0; j < bp.Count; j++)
                for (int i = 0; i < ap.Count; i++)
                    if (ap[i].Match < 0 && ap[i].Start == bp[j].Start && ap[i].End == bp[j].End)
                    {
                        ap[i].Match = j;
                        bp[j].Match = i;
                        break;
                    }
            var removed = ap.FindAll(p => p.Match < 0);
            var added = bp.FindAll(p => p.Match < 0);

            // One pair gone and another arrived elsewhere is a tag moving to
            // other words: formatting changing place, which is a revision's job.
            if (removed.Count > 0 && added.Count > 0) return "tags were moved or reordered";

            foreach (var p in ap)
                if (p.Match >= 0 && !string.Equals(p.Open.Id, bp[p.Match].Open.Id, StringComparison.Ordinal))
                    swaps.Add(new KeyValuePair<Tok, Tok>(p.Open, bp[p.Match].Open));
            var movedTag = MovedTag(swaps,
                removed.ConvertAll(p => p.Open.Id), added.ConvertAll(p => p.Open.Id));
            if (movedTag != null) return movedTag;

            // Apply. Swaps first, keeping track of which node in the old target
            // now stands for each tag of the new one: added pairs are placed
            // inside the right pair, and beside the right placeholders, by it.
            var standIn = new IAbstractMarkupData[b.Count];
            for (int i = 0; i < aOther.Count; i++) standIn[bOther[i].Index] = aOther[i].Node;
            foreach (var p in ap)
                if (p.Match >= 0) standIn[bp[p.Match].OpenIndex] = p.Open.Node;
            foreach (var s in swaps) standIn[s.Value.Index] = Swap(s.Key.Node, s.Value.Node);

            foreach (var p in removed) Unwrap((ITagPair)p.Open.Node);

            foreach (var p in added)   // document order: an outer pair before what it holds
            {
                var host = p.Parent < 0 ? oldRoot : (IAbstractMarkupDataContainer)standIn[bp[p.Parent].OpenIndex];
                int hostStart = p.Parent < 0 ? 0 : bp[p.Parent].Start;
                var wrapper = Wrap(host, hostStart, p, standIn, out var why);
                if (wrapper == null) return why;
                standIn[p.OpenIndex] = wrapper;
            }

            replaced = swaps.Count + removed.Count + added.Count;
            return null;
        }

        private static bool SameShape(List<Tok> a, List<Tok> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Kind != b[i].Kind) return false;
                if (a[i].Kind == TokKind.Locked && a[i].Key != b[i].Key) return false;
            }
            return true;
        }

        private static string PlainText(Flat flat)
        {
            var sb = new StringBuilder();
            foreach (var run in flat.Runs) sb.Append(run.Text);
            return sb.ToString();
        }

        /// <summary>
        /// Null unless some tag id both leaves one place and arrives at another.
        /// Then a tag has moved to other words, and replacing it without a
        /// revision would leave its formatting there even after every text
        /// revision is rejected - a state the segment was never in. An id only
        /// going away (a stale <c>Bold</c>, a duplicate) or only arriving is safe.
        /// </summary>
        private static string MovedTag(List<KeyValuePair<Tok, Tok>> swaps,
            List<string> removedIds, List<string> addedIds)
        {
            var leaving = new HashSet<string>(StringComparer.Ordinal);
            var arriving = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in swaps)
            {
                leaving.Add(s.Key.Id);
                arriving.Add(s.Value.Id);
            }
            if (removedIds != null) leaving.UnionWith(removedIds);
            if (addedIds != null) arriving.UnionWith(addedIds);
            return leaving.Overlaps(arriving) ? "tags were moved or reordered" : null;
        }

        /// <summary>Puts a copy of the new target's tag where the old one was. A
        /// pair keeps the old pair's content - text, comments, nested tags - so
        /// only the tag itself changes. Returns the copy.</summary>
        private static IAbstractMarkupData Swap(IAbstractMarkupData old, IAbstractMarkupData model)
        {
            var parent = old.Parent;
            int at = old.IndexInParent;
            var copy = (IAbstractMarkupData)model.Clone();
            if (old is ITagPair oldPair && copy is ITagPair newPair)
            {
                newPair.Clear();
                var content = new List<IAbstractMarkupData>(oldPair);
                old.RemoveFromParent();
                foreach (var item in content)
                {
                    item.RemoveFromParent();
                    newPair.Add(item);
                }
            }
            else old.RemoveFromParent();
            parent.Insert(at, copy);
            return copy;
        }

        /// <summary>Removes a tag pair and leaves its content where it was.</summary>
        private static void Unwrap(ITagPair pair)
        {
            var parent = pair.Parent;
            int at = pair.IndexInParent;
            var content = new List<IAbstractMarkupData>(pair);
            pair.RemoveFromParent();
            foreach (var item in content)
            {
                item.RemoveFromParent();
                parent.Insert(at++, item);
            }
        }

        /// <summary>
        /// Puts a copy of the new pair <paramref name="pair"/> around the text it
        /// covers in <paramref name="host"/>, which starts at character
        /// <paramref name="hostStart"/>. Text nodes are split at its edges; any
        /// other node must fall wholly inside or wholly outside, and a node that
        /// already stands for a tag of the new target goes inside exactly when
        /// that tag is inside the pair there. Returns the new pair, or null with
        /// <paramref name="why"/>.
        /// </summary>
        private static ITagPair Wrap(IAbstractMarkupDataContainer host, int hostStart,
            PairSpan pair, IAbstractMarkupData[] standIn, out string why)
        {
            why = null;
            int s = pair.Start, e = pair.End;
            if (s == e) { why = "an empty tag pair was added"; return null; }

            // A comment wraps text without being a tag: a range that lies wholly
            // inside one is wrapped in there.
            for (bool deeper = true; deeper; )
            {
                deeper = false;
                int pos = hostStart;
                foreach (var child in new List<IAbstractMarkupData>(host))
                {
                    int len = TextLength(child);
                    if (child is ICommentMarker comment && pos <= s && e <= pos + len
                        && (pos < s || e < pos + len))
                    {
                        host = comment;
                        hostStart = pos;
                        deeper = true;
                        break;
                    }
                    pos += len;
                }
            }

            SplitTextAt(host, hostStart, s);
            SplitTextAt(host, hostStart, e);

            var kids = new List<IAbstractMarkupData>(host);
            var outside = new bool[kids.Count];
            int first = -1, last = -1, at = hostStart;
            for (int i = 0; i < kids.Count; i++)
            {
                var child = kids[i];
                int cs = at, ce = at + TextLength(child);
                at = ce;

                bool inside;
                int k = Array.FindIndex(standIn, x => ReferenceEquals(x, child));
                if (k >= 0)
                {
                    inside = pair.OpenIndex < k && k < pair.CloseIndex;
                    if (inside ? (cs < s || ce > e) : (cs < e && ce > s))
                    {
                        why = "an added tag could not be placed around the existing text";
                        return null;
                    }
                }
                else if (cs == ce) continue;   // empty text, empty comment: goes with its neighbours
                else if (cs >= s && ce <= e) inside = true;
                else if (ce <= s || cs >= e) inside = false;
                else
                {
                    why = "an added tag would cut across a comment";
                    return null;
                }

                if (inside)
                {
                    if (first < 0) first = i;
                    last = i;
                }
                else outside[i] = true;
            }
            if (first < 0) { why = "an added tag has no text to go around"; return null; }
            for (int i = first; i <= last; i++)
                if (outside[i])
                {
                    why = "an added tag could not be placed around the existing text";
                    return null;
                }

            var wrapper = (ITagPair)pair.Open.Node.Clone();
            wrapper.Clear();
            int index = kids[first].IndexInParent;
            for (int i = first; i <= last; i++)
            {
                kids[i].RemoveFromParent();
                wrapper.Add(kids[i]);
            }
            host.Insert(index, wrapper);
            return wrapper;
        }

        /// <summary>Splits the text node of <paramref name="host"/> that has
        /// character <paramref name="at"/> strictly inside it, if there is one,
        /// into two nodes with the same formatting.</summary>
        private static void SplitTextAt(IAbstractMarkupDataContainer host, int hostStart, int at)
        {
            int pos = hostStart;
            foreach (var child in new List<IAbstractMarkupData>(host))
            {
                int len = TextLength(child);
                if (child is IText text && pos < at && at < pos + len)
                {
                    var whole = text.Properties?.Text ?? "";
                    int index = text.IndexInParent;
                    text.RemoveFromParent();
                    host.Insert(index, TextLike(text, whole.Substring(0, at - pos)));
                    host.Insert(index + 1, TextLike(text, whole.Substring(at - pos)));
                    return;
                }
                pos += len;
            }
        }

        /// <summary>Characters of text in a node, counted as flattening counts
        /// them: tags and locked content take none.</summary>
        private static int TextLength(IAbstractMarkupData node)
        {
            if (node is IText t) return (t.Properties?.Text ?? "").Length;
            if (node is ILockedContent || node is IPlaceholderTag) return 0;
            if (node is IAbstractMarkupDataContainer c)
            {
                int n = 0;
                foreach (var item in c) n += TextLength(item);
                return n;
            }
            return 0;
        }

        // ─── Rewriting one run ───────────────────────────────────────

        private struct Fragment
        {
            public TokenDiff.Op Op;
            public string Text;
        }

        /// <summary>
        /// Spreads the edit script over the run's text nodes by position in the old
        /// text. Unchanged and deleted text stays in the node it came from; an
        /// insertion goes to the node ending where it occurs, so text inserted at
        /// a comment's edge lands outside the comment rather than inside it.
        /// </summary>
        private static void RewriteLeaves(Run run, List<TokenDiff.Piece> pieces,
            IDocumentItemFactory factory, IRevisionProperties ins, IRevisionProperties del)
        {
            int n = run.Leaves.Count;
            var starts = new int[n];
            var ends = new int[n];
            int pos = 0;
            for (int i = 0; i < n; i++)
            {
                starts[i] = pos;
                pos += (run.Leaves[i].Properties?.Text ?? "").Length;
                ends[i] = pos;
            }

            var frags = new List<Fragment>[n];
            for (int i = 0; i < n; i++) frags[i] = new List<Fragment>();

            int oldPos = 0;
            foreach (var p in pieces)
            {
                if (p.Op == TokenDiff.Op.Insert)
                {
                    int leaf = 0;
                    for (int i = 0; i < n; i++)
                        if (oldPos > starts[i] && oldPos <= ends[i]) { leaf = i; break; }
                    Append(frags[leaf], p.Op, p.Text);
                    continue;
                }

                int from = oldPos, to = oldPos + p.Text.Length;
                for (int i = 0; i < n; i++)
                {
                    int s = Math.Max(from, starts[i]), e = Math.Min(to, ends[i]);
                    if (e > s) Append(frags[i], p.Op, p.Text.Substring(s - from, e - s));
                }
                oldPos = to;
            }

            for (int i = 0; i < n; i++)
            {
                var leaf = run.Leaves[i];
                var original = leaf.Properties?.Text ?? "";
                var f = frags[i];
                if (f.Count == 0) continue;   // an empty node nothing touched
                if (f.Count == 1 && f[0].Op == TokenDiff.Op.Equal && f[0].Text == original) continue;

                var parent = leaf.Parent;
                int index = leaf.IndexInParent;
                leaf.RemoveFromParent();
                foreach (var frag in f)
                {
                    IAbstractMarkupData node;
                    if (frag.Op == TokenDiff.Op.Equal)
                    {
                        node = TextLike(leaf, frag.Text);
                    }
                    else
                    {
                        var marker = factory.CreateRevision(frag.Op == TokenDiff.Op.Insert ? ins : del);
                        marker.Add(TextLike(leaf, frag.Text));
                        node = marker;
                    }
                    parent.Insert(index++, node);
                }
            }
        }

        private static void Append(List<Fragment> list, TokenDiff.Op op, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (list.Count > 0 && list[list.Count - 1].Op == op)
            {
                var last = list[list.Count - 1];
                last.Text += text;
                list[list.Count - 1] = last;
            }
            else list.Add(new Fragment { Op = op, Text = text });
        }

        /// <summary>A copy of <paramref name="model"/>, carrying its formatting,
        /// with different text.</summary>
        private static IText TextLike(IText model, string text)
        {
            var clone = (IText)model.Clone();
            clone.Properties.Text = text;
            return clone;
        }

        private static void InsertAt(IAbstractMarkupDataContainer container,
            IAbstractMarkupData before, IAbstractMarkupData node)
        {
            if (before != null && before.Parent == container)
                container.Insert(before.IndexInParent, node);
            else
                container.Add(node);
        }

        // ─── Projection: what Accept All / Reject All would produce ──

        /// <summary>
        /// The target as it would read after accepting (or rejecting) every
        /// revision, with tags rendered so that structure is compared too.
        /// Comments are transparent, as everywhere else here.
        /// </summary>
        internal static string Project(IAbstractMarkupDataContainer container, bool accept)
        {
            var sb = new StringBuilder();
            ProjectInto(container, accept, sb);
            return sb.ToString();
        }

        private static void ProjectInto(IAbstractMarkupDataContainer container, bool accept, StringBuilder sb)
        {
            foreach (var item in container)
            {
                if (item is IText t) sb.Append(t.Properties?.Text);
                else if (item is IRevisionMarker r)
                {
                    var type = r.Properties?.RevisionType;
                    bool keep = type == RevisionType.Insert ? accept
                              : type == RevisionType.Delete ? !accept
                              : true;
                    if (keep) ProjectInto(r, accept, sb);
                }
                else if (item is ICommentMarker c) ProjectInto(c, accept, sb);
                else if (item is ITagPair pair)
                {
                    var id = TagId(pair.StartTagProperties);
                    sb.Append('<').Append(id).Append('>');
                    ProjectInto(pair, accept, sb);
                    sb.Append("</").Append(id).Append('>');
                }
                else if (item is IPlaceholderTag ph) sb.Append('<').Append(TagId(ph.Properties)).Append("/>");
                else if (item is ILockedContent locked) sb.Append("[locked:").Append(PlainText(locked.Content)).Append(']');
                else if (item is IAbstractMarkupDataContainer other) ProjectInto(other, accept, sb);
            }
        }

        /// <summary>
        /// The markup tree of <paramref name="container"/> in one line, for the
        /// diagnostic log: node kinds and text lengths, never the text itself (a
        /// client's words do not belong in a log). "T(12) Ins[T(4)] Del[T(5)]
        /// Cmt[T(30)] Tag[...] Ph".
        /// </summary>
        internal static string Shape(IAbstractMarkupDataContainer container)
        {
            if (container == null) return "(null)";
            var sb = new StringBuilder();
            ShapeInto(container, sb);
            return sb.Length == 0 ? "(empty)" : sb.ToString().TrimEnd();
        }

        private static void ShapeInto(IAbstractMarkupDataContainer container, StringBuilder sb)
        {
            foreach (var item in container)
            {
                if (item is IText t) sb.Append("T(").Append(t.Properties?.Text?.Length ?? 0).Append(") ");
                else if (item is IRevisionMarker r)
                {
                    var type = r.Properties?.RevisionType;
                    sb.Append(type == RevisionType.Insert ? "Ins[" : type == RevisionType.Delete ? "Del[" : "Rev[");
                    ShapeInto(r, sb);
                    sb.Append("] ");
                }
                else if (item is ICommentMarker c) { sb.Append("Cmt["); ShapeInto(c, sb); sb.Append("] "); }
                else if (item is ITagPair pair) { sb.Append("Tag["); ShapeInto(pair, sb); sb.Append("] "); }
                else if (item is IPlaceholderTag) sb.Append("Ph ");
                else if (item is ILockedContent) sb.Append("Locked ");
                else if (item is IAbstractMarkupDataContainer other) { sb.Append(item.GetType().Name).Append('['); ShapeInto(other, sb); sb.Append("] "); }
                else sb.Append(item?.GetType().Name ?? "null").Append(' ');
            }
        }

        private static string PlainText(IAbstractMarkupDataContainer container)
        {
            if (container == null) return "";
            var sb = new StringBuilder();
            foreach (var item in container)
            {
                if (item is IText t) sb.Append(t.Properties?.Text);
                else if (item is IAbstractMarkupDataContainer nested) sb.Append(PlainText(nested));
            }
            return sb.ToString();
        }
    }
}
