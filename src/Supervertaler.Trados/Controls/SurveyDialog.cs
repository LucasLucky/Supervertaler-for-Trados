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
    /// The question text comes from the Surveys admin dashboard, typed in by
    /// hand for each new question, so its length can't be known or constrained
    /// in advance – a short one-liner today, a two-sentence question next time.
    /// Every label that carries variable-length text therefore auto-sizes to a
    /// fixed width and reports its own measured height, and everything below it
    /// is positioned relative to that (Bottom-chained), with the form's final
    /// height computed from the lowest control rather than hardcoded. A label
    /// that participates in this chain has its Font set explicitly, even where
    /// it would otherwise just inherit the Form's – PreferredSize is measured
    /// using whatever Font is set on the control at that moment, and these
    /// labels are read (.Bottom / .Right) before they are ever added to the
    /// Form, so an inherited "ambient" font would still measure against the
    /// wrong one (Control.DefaultFont) and throw the chained layout off.
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

        private const int EdgeMargin = 20;
        private const int ContentWidth = 430;
        private static readonly Font BodyFont = new Font("Segoe UI", 9F);

        public SurveyDialog(string question, string yesLabel, string noLabel, string kind = "yesno")
        {
            bool isOpen = (kind == "open");

            Icon = Supervertaler.Trados.Core.IconHelper.AppIcon;
            // Let WinForms scale by system DPI so the dialog doesn't squish at
            // >100% Windows display scaling (same approach as UsageStatisticsDialog).
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();

            Text = "Supervertaler for Trados";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = BodyFont;
            KeyPreview = true;

            // Esc closes as an ignore (does not count as an answer).
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Answer = "ignored";
                    DialogResult = DialogResult.Cancel;
                }
            };

            var lblIntro = new Label
            {
                Text = "Sorry to bother you – a quick question about Supervertaler development.",
                Font = BodyFont,
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Location = new Point(EdgeMargin, 16)
            };

            var lblQuestion = new Label
            {
                Text = question ?? "",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Location = new Point(EdgeMargin, lblIntro.Bottom + 10)
            };

            int y = lblQuestion.Bottom + 16;

            var lblComment = new Label
            {
                Text = isOpen ? "Your answer:" : "Anything to add? (optional)",
                Font = BodyFont,
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true
            };

            _txtComment = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };

            _chkDontAsk = new CheckBox
            {
                Text = "Don't ask again",
                Font = BodyFont,
                AutoSize = true,
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            var lblIgnore = new Label
            {
                Text = "Feel free to just ignore this and close it.",
                Font = BodyFont,
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150)
            };

            if (isOpen)
            {
                // Open mode: the big text box is the answer; a Send button submits.
                lblComment.Location = new Point(EdgeMargin, y);
                _txtComment.Location = new Point(EdgeMargin, lblComment.Bottom + 4);
                _txtComment.Size = new Size(ContentWidth, 96);

                var btnSend = new Button
                {
                    Text = "Send",
                    Location = new Point(EdgeMargin, _txtComment.Bottom + 12),
                    Size = new Size(150, 32),
                    FlatStyle = FlatStyle.System
                };
                btnSend.Click += (s, e) =>
                {
                    // Only counts as an answer if they actually wrote something.
                    Answer = string.IsNullOrEmpty((_txtComment.Text ?? "").Trim()) ? "ignored" : "answered";
                    DialogResult = DialogResult.OK;
                };

                _chkDontAsk.Location = new Point(EdgeMargin, btnSend.Bottom + 12);
                lblIgnore.Location = new Point(_chkDontAsk.Right + 10, _chkDontAsk.Top + 2);

                ClientSize = new Size(470, _chkDontAsk.Bottom + 20);
                Controls.AddRange(new Control[]
                {
                    lblIntro, lblQuestion, lblComment, _txtComment, btnSend, _chkDontAsk, lblIgnore
                });
            }
            else
            {
                // Yes/No mode: buttons, then an optional comment box. 200px-wide
                // buttons (vs. the original 150px) give a longer Yes/No label –
                // also admin-typed, also unpredictable in length – more room
                // before it has to wrap; if it wraps anyway, the Button control
                // grows text onto a second line on its own rather than clipping.
                var btnYes = new Button
                {
                    Text = string.IsNullOrEmpty(yesLabel) ? "Yes" : yesLabel,
                    Location = new Point(EdgeMargin, y),
                    Size = new Size(200, 36),
                    FlatStyle = FlatStyle.System
                };
                btnYes.Click += (s, e) => { Answer = "yes"; DialogResult = DialogResult.OK; };

                var btnNo = new Button
                {
                    Text = string.IsNullOrEmpty(noLabel) ? "No" : noLabel,
                    Location = new Point(EdgeMargin + 210, y),
                    Size = new Size(200, 36),
                    FlatStyle = FlatStyle.System
                };
                btnNo.Click += (s, e) => { Answer = "no"; DialogResult = DialogResult.OK; };

                int afterButtons = Math.Max(btnYes.Bottom, btnNo.Bottom) + 16;
                lblComment.Location = new Point(EdgeMargin, afterButtons);
                _txtComment.Location = new Point(EdgeMargin, lblComment.Bottom + 4);
                _txtComment.Size = new Size(ContentWidth, 72);

                _chkDontAsk.Location = new Point(EdgeMargin, _txtComment.Bottom + 12);
                lblIgnore.Location = new Point(_chkDontAsk.Right + 10, _chkDontAsk.Top + 2);

                ClientSize = new Size(470, _chkDontAsk.Bottom + 20);
                Controls.AddRange(new Control[]
                {
                    lblIntro, lblQuestion, btnYes, btnNo, lblComment, _txtComment, _chkDontAsk, lblIgnore
                });
            }

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
