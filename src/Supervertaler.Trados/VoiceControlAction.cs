using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;
using Supervertaler.Trados.Licensing;
using Supervertaler.Trados.VoiceControl;

namespace Supervertaler.Trados
{
    /// <summary>
    /// The one-button face of voice commands: Ctrl+Alt+V (also in the editor
    /// context menu) toggles listening. First activation downloads the voice
    /// runtime automatically; after that the default command set is ready
    /// with zero configuration. Everything deeper lives behind the gear on
    /// the status strip (VoiceSettingsDialog).
    /// </summary>
    [Action("Supervertaler_VoiceControl", typeof(EditorController),
        Name = "Supervertaler: Toggle voice commands",
        Description = "Start/stop hands-free voice commands (confirm, next segment, insert term…)")]
    [ActionLayout(
        typeof(TranslationStudioDefaultContextMenus.EditorDocumentContextMenuLocation), 6,
        DisplayType.Default, "", false)]
    [Shortcut(Keys.Control | Keys.Alt | Keys.V)]
    public class VoiceControlAction : AbstractAction
    {
        protected override void Execute()
        {
            if (!LicenseManager.Instance.HasTier1Access)
            {
                LicenseManager.ShowLicenseRequiredMessage();
                return;
            }

            VoiceControlManager.Instance.Toggle();
        }
    }
}
