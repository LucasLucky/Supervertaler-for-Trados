using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// A tiny, one-way in-app notice with a link out to further reading (e.g. a
    /// GitHub Discussions post). Unlike <see cref="SurveyDialog"/> this collects
    /// no answer and has no server round-trip: it is shown at most once per
    /// caller-supplied id (tracked in <see cref="Settings.TermLensSettings.ShownAnnouncementIds"/>),
    /// and closing it by any means – the link, "Got it", Esc, or the window's
    /// close button – dismisses it for good.
    ///
    /// LAYOUT: laid out with a TableLayoutPanel and Margins, with no computed
    /// coordinates and no literal ClientSize, for the same reason as
    /// <see cref="SurveyDialog"/> – users have every combination of resolution,
    /// DPI and system font size, and a layout built from hardcoded pixel
    /// positions can only be verified on the machine it was written on. Rows
    /// auto-size and the Form follows them, so measurement happens after the
    /// DPI auto-scale pass rather than being frozen in beforehand. Message text
    /// is a compile-time constant today, but sizing to it costs nothing and
    /// means a longer notice later cannot silently clip.
    ///
    /// Deliberately generic so future one-off announcements (e.g. a References
    /// feature launch) reuse this rather than growing a new dialog each time.
    /// </summary>
    internal sealed class AnnouncementDialog : Form
    {
        private const int ContentWidth = 430;

        public AnnouncementDialog(string introText, string messageText, string linkUrl, string linkLabel,
            string closeLabel = "Got it")
        {
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

            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    DialogResult = DialogResult.Cancel;
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
                Text = introText ?? "",
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 8)
            };

            var lblMessage = new Label
            {
                Text = messageText ?? "",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 12)
            };

            var lnkRead = new LinkLabel
            {
                Text = string.IsNullOrEmpty(linkLabel) ? "Read more" : linkLabel,
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                LinkColor = Color.FromArgb(0, 102, 204),
                Margin = new Padding(0, 0, 0, 14)
            };
            lnkRead.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(linkUrl)) return;
                try
                {
                    // Same "open externally" pattern as the SuperMemory Overview
                    // report (AiAssistantViewPart.OnOverview): hand off to the
                    // shell rather than the plugin owning a browser dependency.
                    Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
                }
                catch { /* opening the browser is best-effort */ }
            };

            var btnClose = new Button
            {
                Text = string.IsNullOrEmpty(closeLabel) ? "Got it" : closeLabel,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(150, 32),
                MaximumSize = new Size(ContentWidth, 0),
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0)
            };
            btnClose.Click += (s, e) => DialogResult = DialogResult.OK;

            root.Controls.Add(lblIntro);
            root.Controls.Add(lblMessage);
            root.Controls.Add(lnkRead);
            root.Controls.Add(btnClose);
            Controls.Add(root);

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
