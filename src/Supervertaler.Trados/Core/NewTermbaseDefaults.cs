using System;
using System.Collections.Generic;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Applies the intended default to a termbase that has just been created:
    /// <b>not</b> sent to the AI until the user says so.
    ///
    /// Why this is needed at all. <see cref="AiSettings.IsTermbaseAiEnabled"/> reads
    /// the list as opt-OUT for Supervertaler termbases — enabled unless the id is in
    /// <see cref="AiSettings.DisabledAiTermbaseIds"/> — and a brand-new id is in no
    /// list at all. So every termbase created since the one-shot migration has been
    /// AI-enabled from birth, which is the opposite of the documented intent
    /// ("Both default to NOT sending") and the opposite of what a user expects:
    /// creating a container is not consent to send its contents to a model. A large
    /// or messy termbase would have started feeding every prompt the moment it
    /// existed. Issue #62.
    ///
    /// The settings grid did not correct it either — it ticks the AI box as
    /// <c>!disabled.Contains(id)</c>, so a new termbase rendered as ticked and
    /// looked like a deliberate choice.
    ///
    /// This is called by every path that creates a termbase. It is NOT applied
    /// retroactively: the opt-out list cannot distinguish "the user ticked this on"
    /// from "this was never recorded", so sweeping the unlisted ones into it would
    /// silently switch off termbases somebody had chosen deliberately.
    /// </summary>
    internal static class NewTermbaseDefaults
    {
        /// <summary>
        /// Records <paramref name="termbaseId"/> as not-sent-to-AI, in a settings
        /// object the caller is going to save itself (the settings dialog owns its
        /// copy and writes it on OK). Returns true if it changed anything.
        /// </summary>
        public static bool ApplyTo(TermLensSettings settings, long termbaseId)
        {
            if (settings == null || termbaseId < 0) return false;
            if (settings.AiSettings == null) settings.AiSettings = new AiSettings();
            if (settings.AiSettings.DisabledAiTermbaseIds == null)
                settings.AiSettings.DisabledAiTermbaseIds = new List<long>();

            if (settings.AiSettings.DisabledAiTermbaseIds.Contains(termbaseId)) return false;
            settings.AiSettings.DisabledAiTermbaseIds.Add(termbaseId);
            return true;
        }

        /// <summary>
        /// Load-modify-save for callers with no settings object of their own.
        /// Best-effort: a termbase that cannot record its default is a nuisance,
        /// not a reason to fail the creation that just succeeded.
        /// </summary>
        public static void Apply(long termbaseId)
        {
            try
            {
                var settings = TermLensSettings.Load();
                if (ApplyTo(settings, termbaseId)) settings.Save();
            }
            catch { }
        }
    }
}
