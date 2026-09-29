using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// One-time anonymous-usage-statistics notice. Shown once after install or
    /// update under the v2 framing: informational, default-on, opt-out.
    ///
    ///   - Yes button / Enter / Esc / X-close all return DialogResult.Yes
    ///     (user keeps stats on - the default).
    ///   - Only an explicit click on "Turn it off" returns DialogResult.No.
    ///
    /// Copy is deliberately written as a personal note from the developer
    /// rather than a corporate disclaimer, since the data collected is
    /// genuinely anonymous and minimal.
    /// </summary>
    internal sealed class UsageStatisticsDialog : Form
    {
        private const string HelpUrl =
            "https://docs.supervertaler.com/trados/settings/usage-statistics/";

        internal const string BodyText =
            "Supervertaler for Trados sends one anonymous ping at startup so I can see " +
            "how many people use the plugin, and on what setup. No personal data, no " +
            "translation content, no termbase info – just a random ID made on your " +
            "computer, the plugin, Windows and Trados versions, your system locale, the " +
            "processor type, whether Windows runs in a virtual machine (as with Parallels " +
            "on a Mac), and your Windows display scaling, Windows text size and " +
            "Supervertaler UI scale.\n\n" +
            "If you'd rather not, switch it off below or any time in Settings.\n\n" +
            "– Michael";

        public UsageStatisticsDialog()
        {
            Icon = Supervertaler.Trados.Core.IconHelper.AppIcon;
            // Let WinForms scale this dialog by system DPI so it doesn't squish
            // at >100% Windows display scaling. Cheap fallback; for surfaces
            // with their own UiScale-driven layout, set AutoScaleMode = None
            // instead and let UiScale own scaling.
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();

            Text = "Supervertaler for Trados";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            HelpButton = true;
            ClientSize = new Size(460, 285);
            Font = new Font("Segoe UI", 9F);

            HelpButtonClicked += (s, e) =>
            {
                e.Cancel = true; // prevent the cursor from changing to ?
                try { Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true }); }
                catch { }
            };

            var lblTitle = new Label
            {
                Text = "Anonymous usage statistics",
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            var lblBody = new Label
            {
                // Every field UsagePing sends, in words. This said "just plugin
                // version, OS, Trados version, and system locale" while the ping
                // also carried the VM, architecture and three scaling fields; a
                // field added to the ping must be added here, to the help page
                // and to supervertaler.com/privacy (section 6).
                Text = BodyText,
                Location = new Point(20, 46),
                Size = new Size(420, 175),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            var lnkLearnMore = new LinkLabel
            {
                Text = "Learn more about what is collected",
                Location = new Point(20, 229),
                AutoSize = true,
                LinkColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.FromArgb(37, 99, 235)
            };
            lnkLearnMore.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true }); }
                catch { }
            };

            var btnYes = new Button
            {
                Text = "Keep it on",
                DialogResult = DialogResult.Yes,
                Location = new Point(190, 245),
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.System
            };

            var btnNo = new Button
            {
                Text = "Turn it off",
                DialogResult = DialogResult.No,
                Location = new Point(320, 245),
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.System
            };

            // Both Enter (AcceptButton) and Esc (CancelButton) are wired to
            // "Keep it on" so any non-explicit close defaults to keeping stats
            // enabled. The X-close button isn't routed through CancelButton -
            // it returns DialogResult.Cancel - and the calling code treats
            // anything that isn't an explicit DialogResult.No as "keep on".
            AcceptButton = btnYes;
            CancelButton = btnYes;

            Controls.AddRange(new Control[] { lblTitle, lblBody, lnkLearnMore, btnYes, btnNo });

            // Size the body to its text once the dialog has its real font and
            // DPI, and move everything below it. The positions above are for
            // 96 DPI, and nothing scales them: at 150% Windows scaling the font
            // grew while the label did not, and the last lines - including how
            // to switch statistics off - were cut off, as the help page's own
            // screenshot shows. Measured from the label as it is, so it is
            // right whether or not WinForms has scaled the layout first.
            Load += (s, e) =>
            {
                var u = lblBody.Font.Height / 15f;   // about 1 at 96 DPI
                var gap = (int)Math.Round(8 * u);
                lblBody.Top = Math.Max(lblBody.Top, lblTitle.Bottom + gap);
                // The label's own measure: it knows whether it draws with GDI
                // or GDI+ (the host decides), which wrap differently.
                var need = lblBody.GetPreferredSize(new Size(lblBody.Width, 0)).Height;
                lblBody.Height = need + gap;
                lnkLearnMore.Top = lblBody.Bottom + gap;
                btnYes.Top = btnNo.Top = lnkLearnMore.Top + (int)Math.Round(16 * u);
                ClientSize = new Size(ClientSize.Width, btnYes.Bottom + gap);
            };

            ResumeLayout(false);
        }
    }
}
