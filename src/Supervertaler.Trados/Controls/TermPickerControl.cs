using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi.Interfaces;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Models;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// The TermPicker list itself: every matched term for the current segment as
    /// a flat, sortable, keyboard-navigable list, with target synonyms as
    /// expandable child rows.
    ///
    /// Hosted by two surfaces so they can never drift apart:
    ///   • <see cref="TermPickerDialog"/> – the Alt+P popup (modal, at cursor)
    ///   • <see cref="TermPickerViewPart"/> – the dockable pane, refreshed on
    ///     every segment change
    /// The keyboard grammar (arrows to navigate with wrap-around, Right/Left to
    /// expand/collapse, digits to jump, Enter to insert) lives here, so both
    /// surfaces behave identically.
    /// </summary>
    public class TermPickerControl : UserControl, IUIControl
    {
        private readonly ListView _listView;
        private readonly Label _hintLabel;
        private List<TermPickerMatch> _matches = new List<TermPickerMatch>();

        // Parent indices (1-based) currently expanded
        private readonly HashSet<int> _expandedParents = new HashSet<int>();

        private static readonly Color HighPriorityBg = ColorTranslator.FromHtml("#FFE5F0");
        private static readonly Color RegularBg = ColorTranslator.FromHtml("#D6EBFF");
        private static readonly Color NonTranslatableBg = ColorTranslator.FromHtml("#FFF3D0");
        private static readonly Color SubItemBg = Color.FromArgb(245, 245, 250);

        /// <summary>Raised when the user chooses a term (Enter, double-click, digit).</summary>
        public event EventHandler<TermPickerInsertEventArgs> InsertRequested;

        /// <summary>
        /// Raised when the user presses 'e' to edit the selected term. Only the
        /// modal popup subscribes (so it can close before the editor opens); when
        /// nobody subscribes, the control opens the editor itself.
        /// </summary>
        public event EventHandler<TermPickerEditEventArgs> EditRequested;

        /// <summary>The target text of the currently selected row, or null.</summary>
        public string SelectedTargetTerm
        {
            get
            {
                if (_listView.SelectedItems.Count == 0) return null;
                var tag = _listView.SelectedItems[0].Tag as RowTag;
                return tag?.TargetTerm;
            }
        }

        public TermPickerControl()
        {
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 9.5f)
            };
            _listView.Columns.Add("#", 48, HorizontalAlignment.Right);
            _listView.Columns.Add("Source", 160, HorizontalAlignment.Left);
            _listView.Columns.Add("Target", 210, HorizontalAlignment.Left);
            _listView.Columns.Add("Termbase", 130, HorizontalAlignment.Left);

            _listView.DoubleClick += (s, e) => RaiseInsert();
            _listView.KeyDown += OnListViewKeyDown;

            _hintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = "Enter inserts • ←/→ collapse/expand synonyms • E edits",
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };

            Controls.Add(_listView);
            Controls.Add(_hintLabel);
        }

        /// <summary>Hides the hint strip (the dialog shows its own with buttons).</summary>
        public bool ShowHint
        {
            get => _hintLabel.Visible;
            set => _hintLabel.Visible = value;
        }

        /// <summary>Gives the list keyboard focus.</summary>
        public void FocusList()
        {
            try { _listView.Focus(); } catch { }
        }

        /// <summary>Current column widths, for the host to persist.</summary>
        public List<int> GetColumnWidths()
        {
            var widths = new List<int>();
            for (int i = 0; i < _listView.Columns.Count; i++)
                widths.Add(_listView.Columns[i].Width);
            return widths;
        }

        /// <summary>Applies persisted column widths (ignored when the count doesn't match).</summary>
        public void ApplyColumnWidths(List<int> widths)
        {
            if (widths == null || widths.Count != _listView.Columns.Count) return;
            for (int i = 0; i < _listView.Columns.Count; i++)
                if (widths[i] > 0) _listView.Columns[i].Width = widths[i];
        }

        /// <summary>
        /// (Re)loads the list for a segment's matches. Synonym groups open
        /// expanded so the full picture is visible at a glance. Safe to call
        /// repeatedly – the docked pane calls it on every segment change.
        /// </summary>
        public void LoadMatches(List<TermPickerMatch> matches)
        {
            _matches = matches ?? new List<TermPickerMatch>();
            _expandedParents.Clear();
            foreach (var match in _matches)
                if (match.GetAllTargets().Count > 1)
                    _expandedParents.Add(match.Index);

            // Column headers follow the termbase's language names when known
            if (_matches.Count > 0 && _matches[0].PrimaryEntry != null)
            {
                var src = _matches[0].PrimaryEntry.SourceLang;
                var tgt = _matches[0].PrimaryEntry.TargetLang;
                if (!string.IsNullOrEmpty(src)) _listView.Columns[1].Text = src;
                if (!string.IsNullOrEmpty(tgt)) _listView.Columns[2].Text = tgt;
            }

            _listView.BeginUpdate();
            try
            {
                PopulateMainRows();
                if (_listView.Items.Count > 0)
                {
                    _listView.Items[0].Selected = true;
                    _listView.Items[0].Focused = true;
                }
            }
            finally { _listView.EndUpdate(); }
        }

        private void PopulateMainRows()
        {
            _listView.Items.Clear();

            foreach (var match in _matches)
            {
                var allTargets = match.GetAllTargets();
                bool hasExpansion = allTargets.Count > 1;

                string indexDisplay = match.Index.ToString();
                if (hasExpansion)
                    indexDisplay += " ▸"; // ▸

                // Adapt casing to the segment occurrence so the picker shows and
                // inserts the same text as the TermLens chips
                var adaptedTarget = TermCaseAdapter.Adapt(match.MatchedSourceText,
                    match.SourceText, match.PrimaryEntry.TargetTerm ?? "");

                var item = new ListViewItem(indexDisplay);
                item.SubItems.Add(match.SourceText);
                item.SubItems.Add(adaptedTarget);
                item.SubItems.Add(match.PrimaryEntry.TermbaseName ?? "");
                item.Tag = new RowTag
                {
                    IsSubItem = false,
                    ParentIndex = match.Index,
                    TargetTerm = adaptedTarget,
                    Match = match
                };

                if (match.PrimaryEntry.IsNonTranslatable) item.BackColor = NonTranslatableBg;
                else if (match.IsProjectTermbase) item.BackColor = HighPriorityBg;
                else item.BackColor = RegularBg;

                _listView.Items.Add(item);

                if (_expandedParents.Contains(match.Index))
                    AddSubItems(match);
            }
        }

        private void AddSubItems(TermPickerMatch match)
        {
            int parentPos = -1;
            for (int i = 0; i < _listView.Items.Count; i++)
            {
                var tag = _listView.Items[i].Tag as RowTag;
                if (tag != null && !tag.IsSubItem && tag.ParentIndex == match.Index)
                {
                    parentPos = i;
                    break;
                }
            }
            if (parentPos < 0) return;

            _listView.Items[parentPos].Text = match.Index + " ▾"; // ▾

            var allTargets = match.GetAllTargets();
            int insertPos = parentPos + 1;

            for (int t = 1; t < allTargets.Count; t++)
            {
                var option = allTargets[t];
                var adaptedOption = TermCaseAdapter.Adapt(match.MatchedSourceText,
                    match.SourceText, option.TargetTerm);

                var subItem = new ListViewItem("");
                subItem.SubItems.Add("    └ " + match.SourceText);
                subItem.SubItems.Add(adaptedOption);
                subItem.SubItems.Add(option.TermbaseName ?? "");
                subItem.Tag = new RowTag
                {
                    IsSubItem = true,
                    ParentIndex = match.Index,
                    TargetTerm = adaptedOption,
                    Match = match
                };
                subItem.BackColor = SubItemBg;
                subItem.ForeColor = Color.FromArgb(60, 60, 60);

                _listView.Items.Insert(insertPos, subItem);
                insertPos++;
            }
        }

        private void RemoveSubItems(int parentIndex)
        {
            for (int i = _listView.Items.Count - 1; i >= 0; i--)
            {
                var tag = _listView.Items[i].Tag as RowTag;
                if (tag != null && tag.IsSubItem && tag.ParentIndex == parentIndex)
                    _listView.Items.RemoveAt(i);
            }

            for (int i = 0; i < _listView.Items.Count; i++)
            {
                var tag = _listView.Items[i].Tag as RowTag;
                if (tag != null && !tag.IsSubItem && tag.ParentIndex == parentIndex)
                {
                    _listView.Items[i].Text = parentIndex + " ▸"; // ▸
                    break;
                }
            }
        }

        private TermPickerMatch FindMatch(int index)
        {
            foreach (var m in _matches)
                if (m.Index == index) return m;
            return null;
        }

        private void ToggleExpansion(int parentIndex)
        {
            var match = FindMatch(parentIndex);
            if (match == null) return;
            if (match.GetAllTargets().Count <= 1) return;

            if (_expandedParents.Contains(parentIndex))
            {
                _expandedParents.Remove(parentIndex);
                RemoveSubItems(parentIndex);
            }
            else
            {
                _expandedParents.Add(parentIndex);
                AddSubItems(match);
            }
        }

        /// <summary>
        /// Opens the term editor for the selected row, matching the TermLens
        /// popup's 'e' key. MultiTerm entries are read-only, so they are
        /// skipped. Hosts that are modal (the Alt+P popup) close themselves
        /// via <see cref="EditRequested"/> before the editor opens.
        /// </summary>
        public void EditSelected()
        {
            if (_listView.SelectedItems.Count == 0) return;
            var tag = _listView.SelectedItems[0].Tag as RowTag;
            var match = tag?.Match;
            var entry = match?.PrimaryEntry;
            if (entry == null || entry.IsMultiTerm) return;

            var handler = EditRequested;
            if (handler != null)
            {
                handler(this, new TermPickerEditEventArgs
                {
                    Entry = entry,
                    AllEntries = match.AllEntries
                });
                return;
            }

            TermLensEditorViewPart.HandleEditCurrentTerm(entry, match.AllEntries);
        }

        private void RaiseInsert()
        {
            var text = SelectedTargetTerm;
            if (string.IsNullOrEmpty(text)) return;
            InsertRequested?.Invoke(this, new TermPickerInsertEventArgs { TargetTerm = text });
        }

        private void OnListViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                RaiseInsert();
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (_listView.Items.Count > 0 && _listView.SelectedIndices.Count > 0
                    && _listView.SelectedIndices[0] == _listView.Items.Count - 1)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    _listView.Items[0].Selected = true;
                    _listView.Items[0].Focused = true;
                    _listView.EnsureVisible(0);
                }
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_listView.Items.Count > 0 && _listView.SelectedIndices.Count > 0
                    && _listView.SelectedIndices[0] == 0)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    int last = _listView.Items.Count - 1;
                    _listView.Items[last].Selected = true;
                    _listView.Items[last].Focused = true;
                    _listView.EnsureVisible(last);
                }
            }
            else if (e.KeyCode == Keys.Right)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                if (_listView.SelectedItems.Count > 0)
                {
                    var tag = _listView.SelectedItems[0].Tag as RowTag;
                    if (tag != null && !tag.IsSubItem && !_expandedParents.Contains(tag.ParentIndex))
                        ToggleExpansion(tag.ParentIndex);
                }
            }
            else if (e.KeyCode == Keys.Left)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                if (_listView.SelectedItems.Count > 0)
                {
                    var tag = _listView.SelectedItems[0].Tag as RowTag;
                    if (tag != null && _expandedParents.Contains(tag.ParentIndex))
                    {
                        int parentIdx = tag.ParentIndex;
                        ToggleExpansion(parentIdx);
                        for (int i = 0; i < _listView.Items.Count; i++)
                        {
                            var ptag = _listView.Items[i].Tag as RowTag;
                            if (ptag != null && !ptag.IsSubItem && ptag.ParentIndex == parentIdx)
                            {
                                _listView.Items[i].Selected = true;
                                _listView.Items[i].Focused = true;
                                _listView.EnsureVisible(i);
                                break;
                            }
                        }
                    }
                }
            }
            else if (e.KeyCode == Keys.E && !e.Alt && !e.Control && !e.Shift)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                EditSelected();
            }
            else if ((e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9
                      || e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
                     && !e.Alt && !e.Control)
            {
                int digit = e.KeyCode >= Keys.NumPad0
                    ? e.KeyCode - Keys.NumPad0
                    : e.KeyCode - Keys.D0;
                e.Handled = true; e.SuppressKeyPress = true;
                SelectByNumber(digit);
            }
        }

        /// <summary>
        /// Jumps to term N (0 = 10). With nine or fewer matches the number is
        /// unambiguous, so it inserts immediately; with more, it only selects,
        /// leaving room for a second digit.
        /// </summary>
        public void SelectByNumber(int digit)
        {
            int targetIndex = digit == 0 ? 10 : digit;

            for (int i = 0; i < _listView.Items.Count; i++)
            {
                var tag = _listView.Items[i].Tag as RowTag;
                if (tag != null && !tag.IsSubItem && tag.ParentIndex == targetIndex)
                {
                    _listView.Items[i].Selected = true;
                    _listView.Items[i].Focused = true;
                    _listView.EnsureVisible(i);

                    if (_matches.Count <= 9)
                        RaiseInsert();
                    return;
                }
            }
        }

        private class RowTag
        {
            public bool IsSubItem { get; set; }
            public int ParentIndex { get; set; }
            public string TargetTerm { get; set; }
            /// <summary>The match this row belongs to – lets 'e' open the term editor.</summary>
            public TermPickerMatch Match { get; set; }
        }
    }

    public class TermPickerInsertEventArgs : EventArgs
    {
        public string TargetTerm { get; set; }
    }

    public class TermPickerEditEventArgs : EventArgs
    {
        public TermEntry Entry { get; set; }
        public List<TermEntry> AllEntries { get; set; }
    }
}
