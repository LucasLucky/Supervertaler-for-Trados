using System;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// A tiny, easily-ignorable dev-survey dialog (issue #43). Two modes:
    ///
    ///   "yesno" — the question with Yes/No buttons + an optional comment box.
    ///   "open"  — no buttons; a large free-text box IS the answer, with a Send
    ///             button (for questions that aren't answerable Yes/No).
    ///
    /// LAYOUT: the question text is typed into the Surveys admin dashboard by
    /// hand for each new question, so its length is unknown at build time – a
    /// one-liner today, three wrapped lines tomorrow. Two earlier attempts at
    /// this failed: fixed Size clipped long text, and hand-computing positions
    /// from AutoSize .Bottom values overlapped controls, because measured sizes
    /// and hardcoded pixel offsets are scaled differently by the DPI auto-scale
    /// pass that runs *after* the constructor.
    ///
    /// So there are no computed coordinates here at all. A TableLayoutPanel with
    /// AutoSize rows stacks the controls and measures them itself, after scaling,
    /// and the Form's AutoSize follows the panel. Nothing in this file should
    /// ever go back to assigning Location or a literal ClientSize.
    ///
    /// Closing without answering is fine — the copy says so, and an unanswered
    /// close leaves Answer = "ignored".
    ///
    /// Read after ShowDialog():
    ///   Answer        — "yes", "no" (yesno mode), "answered" (open mode), or "ignored"
    ///   Comment       — free text (trimmed); the answer itself in open mode
    ///   DontAskAgain  — the user ticked "Don't ask again"
    /// </summary>
    internal sealed class SurveyDialog : Form
    {
        public string Answer { get; private set; } = "ignored";
        public string Comment => (_txtComment.Text ?? "").Trim();
        public bool DontAskAgain => _chkDontAsk.Checked;

        private readonly TextBox _txtComment;
        private readonly CheckBox _chkDontAsk;

        /// <summary>Width available to content inside the panel's padding.</summary>
        private const int ContentWidth = 430;

        public SurveyDialog(string question, string yesLabel, string noLabel, string kind = "yesno")
        {
            bool isOpen = (kind == "open");

            Icon = Supervertaler.Trados.Core.IconHelper.AppIcon;
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();

            Text = "Supervertaler for Trados";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            // The form sizes itself to whatever the panel ends up needing.
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Answer = "ignored";
                    DialogResult = DialogResult.Cancel;
                }
            };

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth));

            var lblIntro = new Label
            {
                Text = "Sorry to bother you – a quick question about Supervertaler development.",
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 10)
            };

            var lblQuestion = new Label
            {
                Text = question ?? "",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 14)
            };

            var lblComment = new Label
            {
                Text = isOpen ? "Your answer:" : "Anything to add? (optional)",
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _txtComment = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                Width = ContentWidth,
                Height = isOpen ? 96 : 72,
                Margin = new Padding(0, 0, 0, 12)
            };

            _chkDontAsk = new CheckBox
            {
                Text = "Don't ask again",
                AutoSize = true,
                ForeColor = Color.FromArgb(90, 90, 90),
                Margin = new Padding(0, 0, 12, 0),
                Anchor = AnchorStyles.Left
            };

            var lblIgnore = new Label
            {
                Text = "Feel free to just ignore this and close it.",
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150),
                Margin = new Padding(0, 3, 0, 0),
                Anchor = AnchorStyles.Left
            };

            // Bottom row: checkbox + hint side by side, in their own auto-sized
            // flow so neither can overlap the other however long the text is.
            var footer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            footer.Controls.Add(_chkDontAsk);
            footer.Controls.Add(lblIgnore);

            root.Controls.Add(lblIntro);
            root.Controls.Add(lblQuestion);

            if (isOpen)
            {
                var btnSend = new Button
                {
                    Text = "Send",
                    Size = new Size(150, 32),
                    FlatStyle = FlatStyle.System,
                    Margin = new Padding(0, 0, 0, 12)
                };
                btnSend.Click += (s, e) =>
                {
                    // Only counts as an answer if they actually wrote something.
                    Answer = string.IsNullOrEmpty((_txtComment.Text ?? "").Trim()) ? "ignored" : "answered";
                    DialogResult = DialogResult.OK;
                };

                root.Controls.Add(lblComment);
                root.Controls.Add(_txtComment);
                root.Controls.Add(btnSend);
            }
            else
            {
                // Yes/No labels are admin-typed too, so the buttons auto-size to
                // their text rather than being pinned to a guessed width.
                var buttons = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = true,
                    MaximumSize = new Size(ContentWidth, 0),
                    Margin = new Padding(0, 0, 0, 14),
                    Padding = new Padding(0)
                };

                var btnYes = new Button
                {
                    Text = string.IsNullOrEmpty(yesLabel) ? "Yes" : yesLabel,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(120, 32),
                    MaximumSize = new Size(ContentWidth, 0),
                    FlatStyle = FlatStyle.System,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnYes.Click += (s, e) => { Answer = "yes"; DialogResult = DialogResult.OK; };

                var btnNo = new Button
                {
                    Text = string.IsNullOrEmpty(noLabel) ? "No" : noLabel,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(120, 32),
                    MaximumSize = new Size(ContentWidth, 0),
                    FlatStyle = FlatStyle.System,
                    Margin = new Padding(0)
                };
                btnNo.Click += (s, e) => { Answer = "no"; DialogResult = DialogResult.OK; };

                buttons.Controls.Add(btnYes);
                buttons.Controls.Add(btnNo);

                root.Controls.Add(buttons);
                root.Controls.Add(lblComment);
                root.Controls.Add(_txtComment);
            }

            root.Controls.Add(footer);
            Controls.Add(root);

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
