using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Models;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// The Alt+P TermPicker popup: a modal shell around <see cref="TermPickerControl"/>,
    /// which owns the list, the synonym expansion and the whole keyboard grammar.
    /// The same control is hosted by <see cref="TermPickerViewPart"/> as a dockable
    /// pane, so popup and pane can't drift apart.
    ///
    /// Rows with multiple target synonyms open expanded (Left collapses them) and
    /// show ▾/▸ in the # column. Enter, double-click or a term number inserts.
    /// </summary>
    public class TermPickerDialog : Form
    {
        private readonly TermPickerControl _picker;
        private readonly TermLensSettings _settings;

        /// <summary>The target term the user chose, or null if cancelled.</summary>
        public string SelectedTargetTerm { get; private set; }

        // Set when the user pressed 'e'; the editor is opened after this form
        // has closed (see OnFormClosed).
        private TermPickerEditEventArgs _pendingEdit;

        public TermPickerDialog(List<TermPickerMatch> matches, TermLensSettings settings = null)
        {
            Icon = IconHelper.AppIcon;
            // Let WinForms scale this dialog by system DPI so it doesn't squish
            // at >100% Windows display scaling.
            AutoScaleMode = AutoScaleMode.Dpi;
            _settings = settings;

            Text = "TermPicker";
            Size = new Size(580, 400);
            MinimumSize = new Size(400, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            if (_settings != null && _settings.TermPickerWidth > 0 && _settings.TermPickerHeight > 0)
                Size = new Size(_settings.TermPickerWidth, _settings.TermPickerHeight);

            _picker = new TermPickerControl
            {
                Dock = DockStyle.Fill,
                ShowHint = false   // the dialog shows its own hint next to the buttons
            };
            _picker.InsertRequested += (s, e) =>
            {
                SelectedTargetTerm = e.TargetTerm;
                DialogResult = DialogResult.OK;
                Close();
            };
            // 'e' must close this modal popup BEFORE the term editor opens –
            // otherwise the editor appears behind it and looks frozen.
            _picker.EditRequested += (s2, e2) =>
            {
                _pendingEdit = e2;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            _picker.LoadMatches(matches);
            _picker.ApplyColumnWidths(_settings?.TermPickerColumnWidths);

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(8, 6, 8, 6)
            };

            var hintLabel = new Label
            {
                Text = "Enter inserts • ←/→ collapse/expand synonyms • E edits • Esc closes",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Right,
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            var btnInsert = new Button
            {
                Text = "Insert",
                Dock = DockStyle.Right,
                Width = 80
            };
            btnInsert.Click += (s, e) =>
            {
                var text = _picker.SelectedTargetTerm;
                if (string.IsNullOrEmpty(text)) return;
                SelectedTargetTerm = text;
                DialogResult = DialogResult.OK;
                Close();
            };

            bottomPanel.Controls.Add(hintLabel);
            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnInsert);

            Controls.Add(_picker);
            Controls.Add(bottomPanel);

            AcceptButton = null;
            CancelButton = btnCancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _picker.FocusList();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_settings != null)
            {
                _settings.TermPickerWidth = Width;
                _settings.TermPickerHeight = Height;
                _settings.TermPickerColumnWidths = _picker.GetColumnWidths();
                _settings.Save();
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            var edit = _pendingEdit;
            _pendingEdit = null;
            if (edit?.Entry != null)
                TermLensEditorViewPart.HandleEditCurrentTerm(edit.Entry, edit.AllEntries);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Escape closes the popup. The ListView swallows it before the
            // form's CancelButton mechanism gets a look-in, so handle it here.
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.F1)
            {
                HelpSystem.OpenHelp(HelpSystem.Topics.TermPickerDialog);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
