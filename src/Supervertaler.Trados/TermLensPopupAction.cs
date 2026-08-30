using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Supervertaler.Trados.Licensing;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Keyboard action: opens the floating TermLens popup – a borderless
    /// version of the docked TermLens panel for the active segment, designed
    /// for keyboard-only term selection.
    ///
    /// Alt+L — two keys, because this is used constantly and a three-key
    /// chord is too slow for it. It replaces the old Ctrl tap, which could not
    /// be told apart from any other program's Ctrl-modified shortcut and so
    /// opened the popup unbidden (see TermLensEditorViewPart.Initialize).
    ///
    /// Alt+L sits beside Alt+P for TermPicker, the sibling feature. Note that
    /// Alt is the ribbon's KeyTip prefix, so an Alt+letter can collide with a
    /// ribbon command that never appears in Studio's keyboard settings; the
    /// keys already used this way (Alt+P, Alt+Q, Alt+S, Alt+T, Alt+W, Alt+0-9,
    /// Alt+Up, Alt+Down) are known good.
    ///
    /// Existing users keep whatever they have bound — Studio stores per-user
    /// shortcuts — so this default only affects fresh installs.
    /// </summary>
    [Shortcut(Keys.Alt | Keys.L)]
    [Action("TermLens_TermLensPopup", typeof(EditorController),
        Name = "TermLens: Show TermLens popup",
        Description = "Open a floating TermLens popup; cycle matches with arrow keys; Enter inserts")]
    public class TermLensPopupAction : AbstractAction
    {
        protected override void Execute()
        {
            if (!LicenseManager.Instance.HasTier1Access)
            {
                LicenseManager.ShowLicenseRequiredMessage();
                return;
            }

            TermLensEditorViewPart.HandleTermLensPopup();
        }
    }
}
