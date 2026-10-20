using System;
using System.Globalization;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Labels a segment Supervertaler has written the way Trados Studio labels one
    /// the translator edited by hand: origin "interactive", the origin it had
    /// before kept as its previous origin (<c>sdl:prev-origin</c>, which is
    /// <see cref="ITranslationOrigin.OriginBeforeAdaptation"/>), and who changed it
    /// and when. Michael chose this for every write, AI translations included,
    /// over labelling those as machine translation, and asked for no setting
    /// (20 Oct 2026).
    ///
    /// <para>Why: until 201 a return package said that a segment Supervertaler
    /// had rewritten was still the unedited TM match or MT output it started as -
    /// in one 81-segment job, 50 tm, 30 nmt and 1 mt, not one interactive - so an
    /// agency filtering on unedited matches, or judging post-editing effort, read
    /// the work as untouched.</para>
    ///
    /// <para>Studio's own rule, read from its editor (the same in 2022, 2024 and
    /// 2026): on a change to a target that is not already interactive, the old
    /// origin is cloned into prev-origin when it was tm, auto-propagated,
    /// document-match, nmt, mt, source, auto-aligned or unknown; the match percent
    /// and the context-match flags stay; the system name goes; and
    /// last_modified_by and modified_on are stamped. A target that is already
    /// interactive keeps its chain, so a second rewrite never nests another
    /// prev-origin. <see cref="IStudioDocument.ChangeTranslationOriginOnSegment"/>
    /// runs that same rule, without the stamp, which is why it is used here
    /// rather than a copy of it.</para>
    ///
    /// <para>Studio applies that rule to a ProcessSegmentPair write itself: its
    /// editor sees the target change once the callback returns and labels the
    /// segment, as it demotes it to Draft. In the live test (Studio 2026, 20 Oct
    /// 2026) it labelled every write, and the relabel here never had to run. The
    /// label was being lost afterwards, by Supervertaler: the pair a writer holds
    /// is a copy taken before the write, still holding the old origin, and
    /// update_segments, find_and_replace and Apply put that copy's whole property
    /// set back to set a status or a lock - the old origin with it. So the live
    /// origin is carried onto the caller's pair, and the relabel stays for a write
    /// Studio's editor does not see.</para>
    /// </summary>
    internal static class SegmentOrigin
    {
        /// <summary>The origin type Studio gives a manual edit
        /// (DefaultTranslationOrigin.Interactive).</summary>
        internal const string Interactive = "interactive";

        /// <summary>
        /// After a write that changed the text of <paramref name="pair"/>'s target:
        /// labels the live segment if Studio has not, and sets the live origin on
        /// <paramref name="pair"/>, so that a later
        /// <c>UpdateSegmentPairProperties(pair, pair.Properties)</c> keeps it.
        /// Never throws - the text is written whatever happens here - and says in
        /// the diagnostic log when the label did not take. UI thread.
        /// </summary>
        /// <param name="levelBefore">The status before the write, for the
        /// created_by stamp Studio adds to a segment translated for the first
        /// time.</param>
        internal static void MarkEdited(IStudioDocument doc, ISegmentPair pair, ConfirmationLevel levelBefore)
        {
            try
            {
                var live = SegmentWriteCheck.ReadLivePair(doc, pair);
                if (live?.Properties == null) return;   // ReadLivePair logged why

                if (!IsInteractive(live.Properties.TranslationOrigin))
                {
                    doc.ChangeTranslationOriginOnSegment(pair, Interactive);
                    live = SegmentWriteCheck.ReadLivePair(doc, pair);
                    var props = live?.Properties?.Clone() as ISegmentPairProperties;
                    if (props?.TranslationOrigin != null)
                    {
                        Stamp(props.TranslationOrigin, levelBefore, props.ConfirmationLevel,
                            StudioUserName(), DateTime.UtcNow);
                        doc.UpdateSegmentPairProperties(live, props);
                        live = SegmentWriteCheck.ReadLivePair(doc, pair);
                    }
                    DiagnosticLog.LogIfChanged("Origin", "Studio had not labelled the written segment; labelled it");
                }

                var origin = live?.Properties?.TranslationOrigin;
                if (!IsInteractive(origin))
                {
                    DiagnosticLog.Log("Origin", "the written segment still reads origin '"
                        + (origin?.OriginType ?? "(none)") + "', not interactive");
                    return;
                }
                if (pair.Properties != null)
                    pair.Properties.TranslationOrigin = (ITranslationOrigin)origin.Clone();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("Origin", "could not label the written segment: " + ex.Message);
            }
        }

        internal static bool IsInteractive(ITranslationOrigin origin) =>
            origin != null && origin.OriginType == Interactive;

        /// <summary>
        /// Studio's stamp for an interactive change (its SetTranslationMetaData):
        /// who and when, and also who created the translation when the segment
        /// leaves Unspecified - it had none until now.
        /// </summary>
        internal static void Stamp(ITranslationOrigin origin, ConfirmationLevel levelBefore,
            ConfirmationLevel levelAfter, string user, DateTime utcNow)
        {
            if (origin == null) return;
            var when = FormatDate(utcNow);
            if (levelBefore != levelAfter && levelBefore == ConfirmationLevel.Unspecified)
            {
                origin.SetMetaData("created_by", user);
                origin.SetMetaData("created_on", when);
            }
            origin.SetMetaData("last_modified_by", user);
            origin.SetMetaData("modified_on", when);
        }

        /// <summary>As Studio writes a date into segment metadata: UTC, the
        /// invariant culture's general format ("10/20/2026 18:22:06").</summary>
        internal static string FormatDate(DateTime utc) => utc.ToString(DateTimeFormatInfo.InvariantInfo);

        /// <summary>
        /// The name Studio stamps unless the translator has set a "user name for
        /// file-based TMs" in its options: DOMAIN\user. That setting is not
        /// reached: the way to it runs through Sdl.Desktop.Platform, which Studio
        /// does not let a plugin reference - a build that did failed to load at
        /// all ("Assembly reference is not public", 20 Oct 2026).
        /// </summary>
        internal static string StudioUserName() =>
            (Environment.UserDomainName + "\\" + Environment.UserName).Trim();
    }
}
