using System;
using System.ComponentModel;
using System.Text;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Whether an edit made through <see cref="IStudioDocument.ProcessSegmentPair"/>
    /// actually landed. Studio's call says nothing when it does not: with Track
    /// Changes and TQA mode both off, the editor protects tracked changes nobody
    /// has accepted or rejected (FrameworkEditControl's revision protection) and
    /// quietly declines an edit that would replace them, from a plugin as from
    /// the keyboard. Found 18 Oct 2026, when update_segments answered ok:true for
    /// four segments that stayed exactly as they were. Michael chose to respect
    /// the protection and say why, rather than lift it and drop changes nobody
    /// has reviewed, and to give every feature that writes a segment the same
    /// check.
    ///
    /// <para>The check reads the segment back from the document and compares
    /// its accepted view - the text as Accept All would leave it, tags included
    /// (<see cref="TrackedTargetMerge.Project"/>) - with the target the edit
    /// built. An edit that changed no text is not checked; nor is a segment that
    /// cannot be read back. One paragraph's pairs are copied per check.</para>
    /// </summary>
    internal static class SegmentWriteCheck
    {
        /// <summary>
        /// <see cref="IStudioDocument.ProcessSegmentPair"/>, then the check: throws
        /// <see cref="SegmentWriteDeclinedException"/> with the reason when Studio
        /// did not apply the edit, so each caller's own failure handling reports
        /// it. UI thread.
        /// </summary>
        /// <param name="comments">Compare the target's comments too: for an edit
        /// that changes a comment and not the text, which the text alone would
        /// show as nothing to check.</param>
        public static void ProcessChecked(IStudioDocument doc, ISegmentPair pair, string undoName,
            Action<ISegmentPair, CancelEventArgs> edit, bool comments = false)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            Func<IAbstractMarkupDataContainer, string> view = comments
                ? (Func<IAbstractMarkupDataContainer, string>)AcceptedWithComments
                : c => TrackedTargetMerge.Project(c, accept: true);
            bool tracked = TrackingSuspension.IsTracking(doc) == true;
            string before = null, after = null, shapeBefore = null, shapeAfter = null;

            doc.ProcessSegmentPair(pair, undoName, (sp, cancel) =>
            {
                before = view(sp.Target);
                shapeBefore = TrackedTargetMerge.Shape(sp.Target);
                edit(sp, cancel);
                after = view(sp.Target);
                shapeAfter = TrackedTargetMerge.Shape(sp.Target);
            });

            if (after == null || after == before) return;
            var live = ReadLivePair(doc, pair)?.Target;
            if (live == null || view(live) == after) return;
            throw new SegmentWriteDeclinedException(Reason(doc, live, shapeBefore, shapeAfter, tracked));
        }

        /// <summary>
        /// Why the edit did not land, or null when <paramref name="live"/> reads as
        /// <paramref name="acceptedAfter"/> (or cannot be compared). Logs the
        /// markup's shape when it did not: kinds and lengths, never the text.
        /// </summary>
        public static string Declined(IStudioDocument doc, ISegment live, string acceptedAfter,
            string shapeBefore, string shapeAfter, bool tracked)
        {
            if (live == null || acceptedAfter == null) return null;
            if (TrackedTargetMerge.Project(live, accept: true) == acceptedAfter) return null;
            return Reason(doc, live, shapeBefore, shapeAfter, tracked);
        }

        /// <summary>The reason for the user, once a check has found that Studio
        /// did not apply an edit; and the log line.</summary>
        private static string Reason(IStudioDocument doc, ISegment live, string shapeBefore, string shapeAfter, bool tracked)
        {
            bool hadRevisions = shapeBefore != null && (shapeBefore.Contains("Ins[") || shapeBefore.Contains("Del["));
            DiagnosticLog.Log("Write", "NOT APPLIED by Studio (tracked write: " + tracked
                + ", tracking now: " + (TrackingSuspension.IsTracking(doc)?.ToString() ?? "unknown")
                + "). Before: " + shapeBefore + " | built: " + shapeAfter
                + " | live after: " + TrackedTargetMerge.Shape(live));
            return hadRevisions && !tracked
                ? "the segment holds tracked changes that have not been accepted or rejected, and Trados Studio "
                  + "protects them while Track Changes is off. Accept or reject them, or switch Track Changes on, "
                  + "and try again. Nothing was written."
                : "Trados Studio did not apply the change: the segment still reads as it did. Nothing was written.";
        }

        /// <summary>
        /// The accepted view with every comment in it - text, author and severity,
        /// where its marker sits: what a comment edit changes. Deleted text and its
        /// comments are left out, as Accept All would, so the copies Studio's own
        /// tracked changes keep do not count.
        /// </summary>
        internal static string AcceptedWithComments(IAbstractMarkupDataContainer container)
        {
            var sb = new StringBuilder();
            WalkAccepted(container, sb);
            return sb.ToString();
        }

        private static void WalkAccepted(IAbstractMarkupDataContainer container, StringBuilder sb)
        {
            if (container == null) return;
            foreach (var item in container)
            {
                if (item is IText t) sb.Append(t.Properties?.Text);
                else if (item is IRevisionMarker r)
                {
                    if (r.Properties?.RevisionType != RevisionType.Delete) WalkAccepted(r, sb);
                }
                else if (item is ICommentMarker m)
                {
                    sb.Append("[c:");
                    var cs = m.Comments;
                    for (int i = 0; cs != null && i < cs.Count; i++)
                    {
                        var c = cs.GetItem(i);
                        if (c != null) sb.Append(c.Text).Append('\u0001').Append(c.Author).Append('\u0001').Append(c.Severity).Append('\u0002');
                    }
                    sb.Append(']');
                    WalkAccepted(m, sb);
                    sb.Append("[/c]");
                }
                else if (item is ITagPair tp) { sb.Append('<'); WalkAccepted(tp, sb); sb.Append('>'); }
                else if (item is IPlaceholderTag) sb.Append("<ph/>");
                else if (item is IAbstractMarkupDataContainer other) WalkAccepted(other, sb);
            }
        }

        /// <summary>The segment pair as the document holds it now, read afresh
        /// (the pair an edit was given is a copy, and keeps the old target); null
        /// when it cannot be found. One paragraph's pairs are copied, not the
        /// document's.</summary>
        public static ISegmentPair ReadLivePair(IStudioDocument doc, ISegmentPair pair)
        {
            try
            {
                var puId = doc?.GetParentParagraphUnit(pair)?.Properties?.ParagraphUnitId;
                var segId = pair?.Properties?.Id.Id;
                if (puId == null || segId == null) return null;
                foreach (var p in doc.GetSegmentPairsFromParagraph(puId.Value))
                    if (p?.Properties != null && p.Properties.Id.Id == segId) return p;
                DiagnosticLog.Log("Write", "could not find the segment again to check the write");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("Write", "could not read the segment back to check the write: " + ex.Message);
            }
            return null;
        }
    }

    /// <summary>Studio did not apply an edit to a segment (see
    /// <see cref="SegmentWriteCheck"/>); the message says why, for the user.</summary>
    internal sealed class SegmentWriteDeclinedException : InvalidOperationException
    {
        public SegmentWriteDeclinedException(string message) : base(message) { }
    }
}
