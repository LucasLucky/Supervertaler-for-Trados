using System;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Editor context-menu action: "Search the web".
    /// Takes the selected source or target text and opens it in every enabled
    /// SuperSearch web resource — IATE, Linguee, Reverso and the rest — using the
    /// project's own language pair, so there is nothing to type or configure.
    ///
    /// <para>Ctrl+Alt+L matches the Supervertaler Workbench binding. The
    /// standalone SuperLookup app deliberately took Ctrl+Shift+L instead, so a
    /// user running both on one machine gets no clash.</para>
    /// </summary>
    [Action("Supervertaler_WebSearch", typeof(EditorController),
        Name = "Search the web",
        Description = "Open the selected text in the enabled SuperSearch web resources")]
    [ActionLayout(
        typeof(TranslationStudioDefaultContextMenus.EditorDocumentContextMenuLocation), 11,
        DisplayType.Default, "", true)]
    [Shortcut(Keys.Control | Keys.Alt | Keys.L)]
    public class WebSearchAction : AbstractAction
    {
        protected override void Execute()
        {
            try
            {
                var editorController = SdlTradosStudio.Application.GetController<EditorController>();
                var doc = editorController?.ActiveDocument;
                if (doc == null) return;

                // Studio fills exactly one side of doc.Selection — whichever the
                // caret is in. Unlike SuperSearchAction we do not care which side
                // it came from: every web resource is a source-side lookup, so
                // either selection is simply "the term to look up".
                string selectedText = null;
                try
                {
                    var selection = doc.Selection;
                    if (selection != null)
                    {
                        var sourceSelection = selection.Source?.ToString();
                        var targetSelection = selection.Target?.ToString();
                        selectedText = !string.IsNullOrWhiteSpace(targetSelection)
                            ? targetSelection.Trim()
                            : sourceSelection?.Trim();
                    }
                }
                catch { /* selection API may not be available in all contexts */ }

                // Bring the panel up so the status line and the Web button are
                // visible — the search itself opens a browser window, but the
                // user should be able to see what ran and adjust the resources.
                try
                {
                    if (SuperSearchViewPart.IsHostedInAssistantTab())
                    {
                        AiAssistantViewPart.ActivateSuperSearchTab();
                    }
                    else
                    {
                        var viewPart = SdlTradosStudio.Application.GetController<SuperSearchViewPart>();
                        viewPart?.Activate();
                    }
                }
                catch { /* Activate may not be available in all Trados versions */ }

                var control = SuperSearchViewPart.GetControl();
                if (control == null) return;

                // A null term leaves whatever is already in the Src box, so the
                // shortcut still works as "search again" with no selection.
                control.RunWebSearch(string.IsNullOrWhiteSpace(selectedText) ? null : selectedText);
            }
            catch { /* silently ignore errors */ }
        }
    }
}
