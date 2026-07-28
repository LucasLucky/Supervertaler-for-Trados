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
    /// Deliberately generic so future one-off announcements (e.g. a References
    /// feature launch) reuse this rather than growing a new dialog each time.
    /// </summary>
    internal sealed class AnnouncementDialog : Form
    {
        public AnnouncementDialog(string introText, string messageText, string linkUrl, string linkLabel,
            string closeLabel = "Got it")
        {
            Icon = Supervertaler.Trados.Core.IconHelper.AppIcon;
            // Same DPI-scaling approach as SurveyDialog / UsageStatisticsDialog, so
            // the dialog doesn't squish at >100% Windows display scaling.
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

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    DialogResult = DialogResult.Cancel;
            };

            var lblIntro = new Label
            {
                Text = introText ?? "",
                Location = new Point(20, 16),
                Size = new Size(430, 20),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            var lblMessage = new Label
            {
                Text = messageText ?? "",
                Location = new Point(20, 44),
                Size = new Size(430, 96),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            var lnkRead = new LinkLabel
            {
                Text = string.IsNullOrEmpty(linkLabel) ? "Read more" : linkLabel,
                Location = new Point(20, 148),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                LinkColor = Color.FromArgb(0, 102, 204)
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
                Location = new Point(20, 184),
                Size = new Size(150, 32),
                FlatStyle = FlatStyle.System
            };
            btnClose.Click += (s, e) => DialogResult = DialogResult.OK;

            ClientSize = new Size(470, 236);
            Controls.AddRange(new Control[] { lblIntro, lblMessage, lnkRead, btnClose });

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
