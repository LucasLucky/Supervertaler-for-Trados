using System.Drawing;
using System.Windows.Forms;
using Supervertaler.Trados.Core;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// A TabControl whose tabs are outlined in a darker grey than Windows draws
    /// them. The themed strip outlines its tabs in a grey so light (229) that in
    /// the Supervertaler Assistant panel the tabs ran together (Michael, 17 Oct
    /// 2026).
    ///
    /// <para>Windows still draws the strip - hover, focus and the selected tab
    /// are all its own. Once it has painted, the outline is drawn again over
    /// the very pixels it used: an unselected tab's top and its right edge,
    /// which the next tab shares, the first tab's left edge, and the selected
    /// tab's frame, which stands 2 px outside its rectangle (measured at 125%,
    /// 17 Oct 2026). Owner-drawing the tabs was tried first and does not work:
    /// the themed frame is painted after the owner-drawn tab, and over it.</para>
    ///
    /// <para>Left alone without visual styles and in high contrast, where the
    /// strip is drawn differently and its own colours are the ones to keep.</para>
    ///
    /// <para><see cref="MarkSelected"/> adds a bar across the selected tab's top
    /// edge. SuperSearch has nine tabs or so, and in a strip that long the
    /// selected tab standing 2 px proud was not enough to find it (Michael, 27
    /// Oct 2026).</para>
    /// </summary>
    internal sealed class OutlinedTabControl : TabControl
    {
        private const int WM_PAINT = 0x000F;

        /// <summary>The grey of a standard button's border.</summary>
        private static readonly Color Outline = Color.FromArgb(173, 173, 173);

        /// <summary>How far the selected tab's frame stands outside its rectangle.</summary>
        private const int SelectedInflate = 2;

        /// <summary>Draw a bar in the selection colour across the selected tab's top.</summary>
        public bool MarkSelected { get; set; }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT) DrawOutlines();
        }

        private void DrawOutlines()
        {
            if (!IsHandleCreated || TabCount == 0 || Multiline
                || !Application.RenderWithVisualStyles || SystemInformation.HighContrast)
                return;

            using (var g = Graphics.FromHwnd(Handle))
            using (var pen = new Pen(Outline))
            {
                int selected = SelectedIndex;
                for (int i = 0; i < TabCount; i++)
                {
                    var r = GetTabRect(i);
                    if (i == selected)
                    {
                        int left = r.Left - SelectedInflate, right = r.Right - 1 + SelectedInflate, top = r.Top - SelectedInflate;
                        g.DrawLine(pen, left, top, left, r.Bottom);
                        g.DrawLine(pen, right, top, right, r.Bottom);
                        g.DrawLine(pen, left, top, right, top);
                        if (MarkSelected)
                            g.FillRectangle(SystemBrushes.Highlight,
                                left, top, right - left + 1, UiScale.Pixels(3));
                        continue;
                    }

                    // The selected tab overlaps its neighbours: their tops stop at its frame.
                    int from = i == selected + 1 ? r.Left + SelectedInflate : r.Left;
                    int to = i == selected - 1 ? r.Right - 1 - SelectedInflate : r.Right - 1;
                    g.DrawLine(pen, from, r.Top, to, r.Top);
                    if (i == 0) g.DrawLine(pen, r.Left, r.Top, r.Left, r.Bottom - 1);
                    // The edge next to the selected tab is the selected tab's frame.
                    if (i + 1 != selected) g.DrawLine(pen, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                }
            }
        }
    }
}
