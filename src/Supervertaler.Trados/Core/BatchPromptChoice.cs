using System;
using System.Collections.Generic;
using System.Linq;
using Supervertaler.Core;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// What a stored batch-prompt choice means, in one place: the Batch
    /// Operations dropdown, the run, Preview prompt, Translate active segment and
    /// the Prompt Manager's "active" tick all go through <see cref="Effective"/>.
    ///
    /// <para>The choice is stored as a prompt's relative path (AiSettings, and
    /// per project in ProjectSettings), and "" meant two things at once: never
    /// chosen, and "(None)" chosen on purpose. Both ran on the built-in
    /// instructions alone, so nearly every Proofread skipped the Default
    /// Proofreading Prompt - its default-to-OK framing, its citation discipline
    /// and its rule against proposing terminology without evidence - and the
    /// dropdown said "(None – default)" as though that were the default prompt.
    /// Seen in the live test of 16 Oct 2026, where such a run raised a
    /// speculative terminology finding of exactly the kind that prompt
    /// suppresses. Translate had the same gap with the Default Translation
    /// Prompt, which Michael chose to close the same way.</para>
    ///
    /// <para>Now a choice never made means the mode's shipped default prompt,
    /// and the deliberate choice of the built-in instructions alone is stored as
    /// <see cref="BuiltInOnly"/>.</para>
    /// </summary>
    internal static class BatchPromptChoice
    {
        /// <summary>
        /// Stored when the translator chooses "(Built-in instructions only)". A
        /// '*' cannot appear in a file name, so this can never name a prompt; a
        /// build that predates it finds no such prompt and sends none, which is
        /// what it means.
        /// </summary>
        public const string BuiltInOnly = "*built-in*";

        /// <summary>The shipped prompts (core PromptLibrary's default
        /// definitions), used when no prompt was ever chosen for the mode.</summary>
        public const string DefaultTranslationPromptName = "Default Translation Prompt";
        public const string DefaultProofreadingPromptName = "Default Proofreading Prompt";

        /// <summary>The mode's shipped default prompt's relative path in
        /// <paramref name="prompts"/>, or null when the library has none: the
        /// shipped copy (IsDefault), not a clone that kept its name.</summary>
        public static string DefaultPath(IEnumerable<PromptTemplate> prompts, bool proofread)
        {
            var name = proofread ? DefaultProofreadingPromptName : DefaultTranslationPromptName;
            var category = proofread ? "Proofread" : "Translate";
            return prompts?
                .FirstOrDefault(p => p != null && p.IsDefault
                    && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                    && Controls.BatchTranslateControl.CategoryBelongsTo(p.Category, category))?
                .RelativePath;
        }

        /// <summary>
        /// The library prompt a run in this mode uses for <paramref name="stored"/>,
        /// or null for the built-in instructions alone.
        /// </summary>
        public static string Effective(string stored, bool proofread, IEnumerable<PromptTemplate> prompts)
        {
            if (stored == BuiltInOnly) return null;
            if (!string.IsNullOrEmpty(stored)) return stored;
            return DefaultPath(prompts, proofread);
        }
    }
}
