using System;
using System.Collections.Generic;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.Desktop.IntegrationApi.Interfaces;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Supervertaler.Trados.Controls;
using Supervertaler.Trados.Models;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Dockable TermPicker pane – the list view of the current segment's
    /// terminology, for users who prefer a flat sortable list as their
    /// permanent terminology display rather than TermLens's in-context chips.
    ///
    /// TermLens and TermPicker are sibling representations of the same data, and
    /// each is now available in both placements:
    ///
    ///                     docked pane        popup at cursor
    ///   TermLens          TermLens panel     Ctrl tap
    ///   TermPicker        THIS pane          Alt+P
    ///
    /// Alt+P deliberately still opens the popup even when this pane is visible
    /// (mirroring TermLens, where Ctrl-tap works regardless of the docked panel).
    ///
    /// Not pinned by default: Trados always registers the ViewPart, but nobody's
    /// existing layout changes on update – the user opens it from the View tab.
    ///
    /// Refresh is driven by TermLensEditorViewPart (which already recomputes
    /// matches on every segment change) calling <see cref="RefreshIfOpen"/>, so
    /// the pane is always exactly in step with the TermLens panel instead of
    /// duplicating the editor event wiring.
    /// </summary>
    [ViewPart(
        Id = "TermPickerViewPart",
        Name = "TermPicker",
        Description = "Matched terms for the current segment as a list",
        Icon = "TermLensIcon"
    )]
    [ViewPartLayout(typeof(EditorController), Dock = DockType.Right, Pinned = false)]
    public class TermPickerViewPart : AbstractViewPartController
    {
        private static TermPickerControl _control;
        private static TermPickerViewPart _instance;

        protected override IUIControl GetContentControl()
        {
            EnsureControl();
            return _control;
        }

        protected override void Initialize()
        {
            _instance = this;
            EnsureControl();
            // Populate immediately: the pane is usually shown while a document
            // is already open, so waiting for the next segment change would
            // leave it blank and looking broken.
            RefreshIfOpen();
        }

        private static void EnsureControl()
        {
            if (_control != null && !_control.IsDisposed) return;

            _control = new TermPickerControl();
            _control.ShowHint = true;

            // Insert through the same path the popup and the TermLens chips use,
            // so casing adaptation and the "TermLens" origin label are identical.
            _control.InsertRequested += (s, e) =>
                TermLensEditorViewPart.InsertTargetText(e.TargetTerm);

            try
            {
                var settings = TermLensSettings.Load();
                _control.ApplyColumnWidths(settings.TermPickerPaneColumnWidths);
            }
            catch { /* widths are cosmetic */ }
        }

        /// <summary>
        /// Reloads the pane from the current segment's matches. Called by
        /// TermLensEditorViewPart whenever the segment display is rebuilt.
        /// No-op when the pane has never been opened, so users who don't use it
        /// pay nothing.
        /// </summary>
        public static void RefreshIfOpen()
        {
            var ctrl = _control;
            if (ctrl == null || ctrl.IsDisposed || !ctrl.IsHandleCreated) return;

            try
            {
                var matches = TermLensEditorViewPart.GetCurrentSegmentMatches()
                              ?? new List<TermPickerMatch>();
                if (ctrl.InvokeRequired)
                    ctrl.BeginInvoke((Action)(() => ctrl.LoadMatches(matches)));
                else
                    ctrl.LoadMatches(matches);
            }
            catch
            {
                // Document may be mid-transition – the next segment change retries
            }
        }

        /// <summary>Persists the pane's column widths (called on shutdown).</summary>
        public static void SaveState()
        {
            var ctrl = _control;
            if (ctrl == null || ctrl.IsDisposed) return;
            try
            {
                var settings = TermLensSettings.Load();
                settings.TermPickerPaneColumnWidths = ctrl.GetColumnWidths();
                settings.Save();
            }
            catch { }
        }
    }
}
