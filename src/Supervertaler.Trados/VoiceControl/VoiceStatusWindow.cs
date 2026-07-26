using System;
using System.Drawing;
using System.Windows.Forms;
using Supervertaler.Trados.Core;

namespace Supervertaler.Trados.VoiceControl
{
    /// <summary>
    /// Tiny always-on-top status strip shown while voice commands are active –
    /// the "simple face" of the feature. Shows the listening state and the
    /// last executed command, plus a stop button and a gear that opens the
    /// Advanced dialog. Positioned bottom-right of the primary screen; not
    /// activated on show, so it never steals focus from the editor.
    /// </summary>
    internal sealed class VoiceStatusWindow : Form
    {
        private readonly Label _dot;
        private readonly Label _text;
        private readonly Timer _flashTimer;

        public event EventHandler StopRequested;
        public event EventHandler AdvancedRequested;

        protected override bool ShowWithoutActivation => true;

        public VoiceStatusWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(32, 33, 36);
            Padding = new Padding(UiScale.Pixels(8), UiScale.Pixels(5), UiScale.Pixels(6), UiScale.Pixels(5));

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _dot = new Label
            {
                Text = "●",
                ForeColor = Color.FromArgb(120, 200, 120),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, UiScale.Pixels(2), UiScale.Pixels(4), 0)
            };

            _text = new Label
            {
                Text = "Listening…",
                ForeColor = Color.WhiteSmoke,
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, UiScale.Pixels(2), UiScale.Pixels(8), 0)
            };

            var gear = MakeButton("⚙", "Advanced voice settings");
            gear.Click += (s, e) => AdvancedRequested?.Invoke(this, EventArgs.Empty);

            var stop = MakeButton("✕", "Stop voice commands");
            stop.Click += (s, e) => StopRequested?.Invoke(this, EventArgs.Empty);

            layout.Controls.Add(_dot);
            layout.Controls.Add(_text);
            layout.Controls.Add(gear);
            layout.Controls.Add(stop);
            Controls.Add(layout);

            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _flashTimer = new Timer { Interval = 2500 };
            _flashTimer.Tick += (s, e) =>
            {
                _flashTimer.Stop();
                SetStatus("Listening…", listening: true);
            };

            Load += (s, e) => PositionBottomRight();
        }

        private Button MakeButton(string glyph, string tip)
        {
            var b = new Button
            {
                Text = glyph,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(48, 49, 52),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(UiScale.Pixels(2), 0, 0, 0),
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            new ToolTip().SetToolTip(b, tip);
            return b;
        }

        private void PositionBottomRight()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - UiScale.Pixels(24), wa.Bottom - Height - UiScale.Pixels(24));
        }

        /// <summary>Thread-safe status update.</summary>
        public void SetStatus(string text, bool listening)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetStatus(text, listening))); return; }
            _text.Text = text;
            _dot.ForeColor = listening ? Color.FromArgb(120, 200, 120) : Color.FromArgb(230, 170, 60);
            PositionBottomRight();
        }

        /// <summary>Shows "heard" feedback briefly, then reverts to Listening.</summary>
        public void FlashCommand(string phrase)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => FlashCommand(phrase))); return; }
            _text.Text = "“" + phrase + "”";
            _dot.ForeColor = Color.FromArgb(110, 168, 254);
            PositionBottomRight();
            _flashTimer.Stop();
            _flashTimer.Start();
        }
    }
}
