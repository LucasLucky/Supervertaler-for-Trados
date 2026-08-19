using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// Edits one memory-bank Markdown file as plain text.
    ///
    /// <para>Plain text on purpose. These files go straight into the AI's
    /// context, so a save that reformats or "tidies" one degrades every
    /// subsequent translation quietly. Nothing here parses the markdown; what
    /// you typed is what gets written.</para>
    ///
    /// <para>Not <see cref="PromptEditorDialog"/>, which carries Name,
    /// Description and Category fields. A bank file has none of those, and a
    /// Name box would imply it renames the file.</para>
    ///
    /// <para><b>Two hazards this exists to handle.</b> These files are also
    /// written by Obsidian and the Python Supervertaler assistant, and nothing
    /// locks them — so the file can change between opening this dialog and
    /// saving it. And the bank files do not agree on line endings: some are
    /// CRLF, <c>_shared/brief.md</c> is LF-only. Writing back with whatever
    /// WinForms produced would rewrite every line of such a file, turning a
    /// one-word edit into a whole-file diff in the user's sync and version
    /// history.</para>
    /// </summary>
    internal class BankFileEditorDialog : Form
    {
        private readonly string _filePath;
        private TextBox _txt;

        /// <summary>What the file looked like when we opened it, so a
        /// concurrent write can be detected rather than silently clobbered.</summary>
        private DateTime _openedWriteTimeUtc;

        /// <summary>The file's own newline, preserved on save.</summary>
        private string _newline = Environment.NewLine;

        /// <summary>Whether the file ended with a newline. Adding or removing
        /// one shows up as a change in every diff tool.</summary>
        private bool _trailingNewline;

        public BankFileEditorDialog(string filePath, string bankName, bool readIntoPrompts)
        {
            _filePath = filePath;

            Icon = Core.IconHelper.AppIcon;
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Edit " + Path.GetFileName(filePath ?? "");
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 620);
            MinimumSize = new Size(420, 300);
            BackColor = Color.White;

            var header = new Label
            {
                Text = readIntoPrompts
                    ? "Memory bank \"" + bankName + "\" — read into the AI's context."
                    : "Memory bank \"" + bankName + "\", reference folder — never read into a prompt.",
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(12, 6, 12, 0),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 110, 110)
            };

            _txt = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                AcceptsTab = true,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(252, 252, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.White };

            var btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.None,   // set only after a successful write
                Width = 90,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnSave.Click += OnSave;

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnCancel);
            buttons.Resize += (s, e) =>
            {
                btnCancel.Location = new Point(buttons.Width - btnCancel.Width - 12, 10);
                btnSave.Location = new Point(btnCancel.Left - btnSave.Width - 8, 10);
            };

            // Fill first, then the docked edges, so the text box gets what is left.
            var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 0), BackColor = Color.White };
            pad.Controls.Add(_txt);

            Controls.Add(pad);
            Controls.Add(buttons);
            Controls.Add(header);

            AcceptButton = null;      // Enter inserts a newline; this is a text editor
            CancelButton = btnCancel;

            Load += (s, e) => LoadFile();
        }

        private void LoadFile()
        {
            try
            {
                var raw = File.ReadAllText(_filePath);
                _openedWriteTimeUtc = File.GetLastWriteTimeUtc(_filePath);

                // Whichever ending dominates is the file's convention. Counting
                // rather than sniffing the first one: a file edited by two tools
                // can be mixed, and the majority is the safer thing to restore.
                var crlf = CountOccurrences(raw, "\r\n");
                var lf = CountOccurrences(raw, "\n");
                _newline = (crlf > 0 && crlf >= lf - crlf) ? "\r\n" : "\n";
                _trailingNewline = raw.EndsWith("\n");

                // The TextBox needs CRLF to show line breaks at all.
                _txt.Text = raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
                _txt.Select(0, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open this file:\n\n" + ex.Message,
                    "Supervertaler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private void OnSave(object sender, EventArgs e)
        {
            try
            {
                // Somebody else may have written this file while the dialog was
                // open - Obsidian, or the Python assistant. Saying so
                // beats silently winning.
                var now = File.Exists(_filePath) ? File.GetLastWriteTimeUtc(_filePath) : _openedWriteTimeUtc;
                if (now != _openedWriteTimeUtc)
                {
                    var answer = MessageBox.Show(this,
                        "Something else wrote to this file while you had it open.\n\n"
                        + "That would be Obsidian or another editor, or the Supervertaler "
                        + "assistant. Saving now replaces what they wrote with the text in "
                        + "this window.\n\n"
                        + "Choose No to go back \u2014 your text stays in the editor, so you can "
                        + "copy it somewhere safe and compare before deciding.\n\n"
                        + "Save anyway?",
                        "File changed on disk",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    if (answer != DialogResult.Yes) return;
                }

                var text = _txt.Text.Replace("\r\n", "\n");
                if (_trailingNewline && !text.EndsWith("\n")) text += "\n";
                if (!_trailingNewline && text.EndsWith("\n")) text = text.TrimEnd('\n');
                if (_newline != "\n") text = text.Replace("\n", _newline);

                // No BOM: these files are shared with tools that do not write one.
                File.WriteAllText(_filePath, text, new UTF8Encoding(false));

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save this file:\n\n" + ex.Message,
                    "Supervertaler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
