using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;
using Supervertaler.Trados.Controls;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Licensing;
using Supervertaler.Trados.Models;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Editor context menu action: "Smart-add term (AI)".
    ///
    /// The AI counterpart of <see cref="AddTermAction"/>. Instead of pre-filling
    /// the term entry dialog with whatever the translator selected, it sends the
    /// whole source and target segment to the model and asks it to identify the
    /// term pair AND any abbreviation spelled out alongside it – the case this
    /// exists for being text like "Sustainable Finance Disclosure Regulation
    /// (SFDR, Verordening (EU) 2019/2088)", where the term and its abbreviation
    /// are both on screen and were previously typed into the dialog by hand.
    ///
    /// The dialog still opens and the translator still saves it. Nothing is
    /// written without confirmation: an extraction can pick the wrong span or
    /// the wrong abbreviation, and a silent fan-out into every write termbase is
    /// exactly the failure mode that corrupted two termbases in 20.153.
    ///
    /// Falls back to plain <see cref="AddTermAction"/> behaviour – the raw
    /// selection, no abbreviations – whenever the AI is unconfigured, fails,
    /// or returns something that does not survive validation. The action never
    /// leaves the translator with nothing.
    /// </summary>
    [Action("TermLens_SmartAddTerm", typeof(EditorController),
        Name = "Smart-add term (AI)",
        Description = "Let the AI extract the term pair and its abbreviation from this segment, then confirm in the dialog")]
    [ActionLayout(
        typeof(TranslationStudioDefaultContextMenus.EditorDocumentContextMenuLocation), 7,
        DisplayType.Default, "", true)]
    [Shortcut(Keys.Alt | Keys.Shift | Keys.Down)]
    public class SmartAddTermAction : AbstractAction
    {
        protected override void Execute()
        {
            if (!LicenseManager.Instance.HasAssistantAccess)
            {
                LicenseManager.ShowUpgradeMessage();
                return;
            }

            try
            {
                var editorController = SdlTradosStudio.Application.GetController<EditorController>();
                var doc = editorController?.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("No document is open.",
                        "TermLens", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var settings = TermLensSettings.Load();

                if (settings.WriteTermbaseIds == null || settings.WriteTermbaseIds.Count == 0)
                {
                    MessageBox.Show(
                        "No write termbase is configured.\n\n" +
                        "Open TermLens settings (gear icon) and check the “Write” column " +
                        "for the termbases where new terms should be added.",
                        "TermLens — Smart-Add Term",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(settings.TermbasePath) || !File.Exists(settings.TermbasePath))
                {
                    MessageBox.Show(
                        "Database file not found. Please check the TermLens settings.",
                        "TermLens — Smart-Add Term",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Full segment text on both sides – the model needs the whole
                // segment, since the abbreviation usually sits outside whatever
                // the translator happened to select.
                string fullSource = doc.ActiveSegmentPair?.Source != null
                    ? SegmentTagHandler.GetFinalText(doc.ActiveSegmentPair.Source) : "";
                string fullTarget = doc.ActiveSegmentPair?.Target != null
                    ? SegmentTagHandler.GetFinalText(doc.ActiveSegmentPair.Target) : "";

                if (string.IsNullOrWhiteSpace(fullSource) || string.IsNullOrWhiteSpace(fullTarget))
                {
                    MessageBox.Show(
                        "Both source and target text are required.\n\n" +
                        "Make sure the active segment has text on both sides.",
                        "TermLens — Smart-Add Term",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Any selection is passed to the model as a hint only – it steers
                // which term is picked, it does not bound it, so selecting half a
                // term still yields the whole term.
                string srcSelection = null, tgtSelection = null;
                try
                {
                    var selection = doc.Selection;
                    if (selection != null)
                    {
                        try { srcSelection = selection.Source?.ToString(); } catch { }
                        try { tgtSelection = selection.Target?.ToString(); } catch { }
                    }
                }
                catch { /* selection unavailable – the model works from the segment alone */ }

                // Fallback pre-fills, used when extraction is unavailable or fails.
                string fallbackSource = string.IsNullOrWhiteSpace(srcSelection) ? fullSource : srcSelection.Trim();
                string fallbackTarget = string.IsNullOrWhiteSpace(tgtSelection) ? fullTarget : tgtSelection.Trim();

                var writeTermbases = new List<TermbaseInfo>();
                using (var reader = new TermbaseReader(settings.TermbasePath))
                {
                    if (reader.Open())
                    {
                        foreach (var id in settings.WriteTermbaseIds)
                        {
                            var tb = reader.GetTermbaseById(id);
                            if (tb != null) writeTermbases.Add(tb);
                        }
                    }
                }

                if (writeTermbases.Count == 0)
                {
                    MessageBox.Show(
                        "The configured write termbases were not found in the database.\n" +
                        "Please check the TermLens settings.",
                        "TermLens — Smart-Add Term",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var primaryTb = settings.ProjectTermbaseId > 0
                    ? writeTermbases.Find(t => t.Id == settings.ProjectTermbaseId) ?? writeTermbases[0]
                    : writeTermbases[0];

                string projectSourceLang = null, projectTargetLang = null;
                try { projectSourceLang = doc.ActiveFile?.SourceFile?.Language?.DisplayName; } catch { }
                try { projectTargetLang = doc.ActiveFile?.Language?.DisplayName; } catch { }

                // ── AI extraction ──────────────────────────────────────────────
                var extracted = TryExtract(
                    settings, fullSource, fullTarget,
                    projectSourceLang, projectTargetLang,
                    srcSelection, tgtSelection);

                string preSource = fallbackSource;
                string preTarget = fallbackTarget;
                string preSourceAbbr = null;
                string preTargetAbbr = null;

                if (extracted != null && extracted.Found)
                {
                    preSource = extracted.SourceTerm;
                    preTarget = extracted.TargetTerm;
                    preSourceAbbr = extracted.SourceAbbreviation;
                    preTargetAbbr = extracted.TargetAbbreviation;
                }

                // Pre-fills are passed in PROJECT direction; the dialog swaps them
                // per its own termbase's declared direction (see its add-mode ctor).
                using (var dlg = new TermEntryEditorDialog(
                    preSource, preTarget, settings.TermbasePath, primaryTb, projectSourceLang,
                    preSourceAbbr, preTargetAbbr))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        if (dlg.SavedEntry != null)
                            TermLensEditorViewPart.NotifyTermInserted(
                                new List<Models.TermEntry> { dlg.SavedEntry });
                        else
                            TermLensEditorViewPart.NotifyTermAdded();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}",
                    "TermLens", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Runs the extraction call behind the shared busy dialog. Returns null on
        /// any failure – an unconfigured provider, a network error, an unparseable
        /// or unverifiable reply – so the caller falls back to the plain selection.
        /// A failure here must never block adding a term by hand.
        /// </summary>
        private static SmartTermExtractor.Result TryExtract(
            TermLensSettings settings,
            string fullSource, string fullTarget,
            string sourceLang, string targetLang,
            string srcSelection, string tgtSelection)
        {
            try
            {
                var aiSettings = settings?.AiSettings;
                if (aiSettings == null) return null;

                var provider = aiSettings.SelectedProvider ?? LlmModels.ProviderOpenAi;
                string apiKey;
                string baseUrl = null;
                string model = aiSettings.GetSelectedModel();

                if (provider == LlmModels.ProviderOllama)
                {
                    apiKey = "ollama";
                    baseUrl = aiSettings.OllamaEndpoint ?? "http://localhost:11434";
                }
                else if (provider == LlmModels.ProviderCustomOpenAi)
                {
                    var profile = aiSettings.GetActiveCustomProfile();
                    if (profile == null) return null;
                    apiKey = profile.ApiKey;
                    baseUrl = profile.Endpoint;
                    model = profile.Model;
                }
                else
                {
                    apiKey = LlmClient.ResolveApiKey(provider, aiSettings.ApiKeys);
                }

                if (string.IsNullOrEmpty(apiKey)) return null;

                var userPrompt = SmartTermExtractor.BuildUserPrompt(
                    fullSource, fullTarget, sourceLang, targetLang, srcSelection, tgtSelection);

                string reply;
                using (var client = new LlmClient(provider, model, apiKey, baseUrl))
                using (var busy = new AutoPromptBusyForm(
                    () => client.SendPromptAsync(
                        userPrompt,
                        SmartTermExtractor.SystemPrompt,
                        maxTokens: 400,
                        suppressLog: true)))
                {
                    busy.ShowDialog();
                    reply = busy.Result;
                }

                var result = SmartTermExtractor.Parse(reply, fullSource, fullTarget);
                if (!result.Found && !string.IsNullOrEmpty(result.Note))
                    DiagnosticLog.Log("SmartAddTerm", "Extraction not used: " + result.Note);
                else if (!string.IsNullOrEmpty(result.Note))
                    DiagnosticLog.Log("SmartAddTerm", result.Note);

                return result;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("SmartAddTerm", "Extraction failed: " + ex.Message);
                return null;
            }
        }
    }
}
