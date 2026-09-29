using System;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Scales a code-built dialog's layout to the screen's DPI, once, at the end
    /// of its constructor.
    ///
    /// <para><b>Why WinForms' own scaling is not used.</b> These dialogs set
    /// <c>AutoScaleMode.Dpi</c> in code, and it scaled nothing: the mode takes
    /// effect the moment it is set, while the form has no controls yet, and the
    /// controls added afterwards are never scaled. Studio is DPI-aware, so the
    /// fonts (in points) grew with Windows display scaling while every size and
    /// position (in pixels) stayed put - at 125% the first-run SetupDialog cut
    /// off its own text. Setting <c>AutoScaleDimensions</c> inside
    /// <c>SuspendLayout</c> does make WinForms scale, but then a Label that
    /// inherits the form's Font is scaled twice, and <c>AutoScaleMode.Font</c>
    /// scaled nothing at all (both checked 2026-09-29 on .NET 4.8 with
    /// .dev/autoscale-order.ps1). <c>Control.Scale</c> scales every control
    /// exactly once.</para>
    ///
    /// <para><b>The factor</b> is the screen DC's DPI, as <see cref="UiScale"/>
    /// reads it: the DPI this process draws its fonts at, so layout and text
    /// grow together. It is 96, and this does nothing, in a process that is not
    /// DPI-aware. <c>Control.DeviceDpi</c> is no use: .NET 4.8 reports 96 there
    /// unless the application opted into its high-DPI mode.</para>
    ///
    /// <para>A dialog that sets pixel positions later, in a Resize or Load
    /// handler, must scale those numbers itself - <see cref="Pixels"/>.</para>
    /// </summary>
    internal static class DialogScale
    {
        /// <summary>The screen DPI over 96: 1.25 at 125% Windows scaling.</summary>
        public static float Factor
        {
            get
            {
                try
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        var k = g.DpiX / 96f;
                        return k > 0f && k <= 4f ? k : 1f;
                    }
                }
                catch { return 1f; }
            }
        }

        /// <summary>A pixel distance at 96 DPI, scaled to the screen.</summary>
        public static int Pixels(int at96) => (int)Math.Round(at96 * Factor);

        /// <summary>
        /// Switches WinForms' own scaling off and scales the form and every
        /// control in it by <see cref="Factor"/>. Call once, after all the
        /// controls are added.
        /// </summary>
        public static void Apply(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.None;
            var k = Factor;
            if (Math.Abs(k - 1f) < 0.01f) return;
            form.Scale(new SizeF(k, k));
        }
    }
}
