using System;
using System.Runtime.InteropServices;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Pulls one of our windows in front of Trados.
    ///
    /// <para>A plain <c>Form.Activate()</c> loses the foreground race against a
    /// heavy host: Windows accepts <c>SetForegroundWindow</c> and then quietly
    /// declines to actually raise the window. This is the escalation chain from
    /// the standalone SuperLookup app, itself ported from the Supervertaler
    /// Workbench where it was hard-won over the v1.10.x series — the same code
    /// path, for the same reason, against the same host.</para>
    ///
    /// <list type="number">
    /// <item>A synthetic Alt chord satisfies <c>SetForegroundWindow</c>'s
    /// documented "Alt key pressed" exception, so the OS grants the call even
    /// with no other claim on the foreground. Alt+F24 rather than a bare Alt tap,
    /// because F24 is inert and nothing binds it — a lone Alt would open a menu
    /// bar.</item>
    /// <item><c>AttachThreadInput</c> shares the foreground thread's input queue.
    /// Usually a no-op here, since our form and Trados share one UI thread — but
    /// the WebView2 browser is a separate process, and it is that process which
    /// takes the foreground during initialisation.</item>
    /// <item><c>BringWindowToTop</c> → <c>SetForegroundWindow</c> →
    /// <c>SwitchToThisWindow</c>, the last a deprecated-but-effective hammer.</item>
    /// </list>
    /// </summary>
    public static class ForegroundWindow
    {
        private const int VK_MENU = 0x12;
        private const int VK_F24 = 0x87;
        private const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, int flags, IntPtr extraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(int attachTo, int attachFrom, bool attach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);

        private const int GWL_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

        /// <summary>
        /// Makes <paramref name="owner"/> the owner of <paramref name="child"/>.
        ///
        /// <para>This is the structural half of keeping the web window visible.
        /// Forcing the foreground is a race we can lose — WebView2 loads pages in
        /// a separate browser process which grabs focus whenever it finishes,
        /// which can be long after we raised the window. An <i>owned</i> window,
        /// by contrast, is kept above its owner by the window manager itself, so
        /// it can never end up behind Trados no matter who wins the focus race.
        /// </para>
        ///
        /// <para>Trados' main window is WPF-hosted, not a WinForms Form, so
        /// Form.Owner cannot express this — hence GWL_HWNDPARENT directly.</para>
        /// </summary>
        public static bool SetOwner(IntPtr child, IntPtr owner)
        {
            if (child == IntPtr.Zero || owner == IntPtr.Zero) return false;
            // A window cannot own itself; Windows rejects the call and the child
            // silently keeps no owner at all.
            if (child == owner)
            {
                DiagnosticLog.Log("Foreground",
                    "Refusing to make a window its own owner — host handle was misdetected");
                return false;
            }
            try
            {
                if (IntPtr.Size == 8)
                    SetWindowLongPtr64(child, GWL_HWNDPARENT, owner);
                else
                    SetWindowLong32(child, GWL_HWNDPARENT, owner.ToInt32());
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("Foreground", $"Could not set window owner: {ex.Message}");
                return false;
            }
        }

        private static IntPtr _hostMainWindow;

        /// <summary>
        /// Records the Trados main window. Must be called at plugin start-up,
        /// before any of our own windows exist.
        ///
        /// <para><c>Process.MainWindowHandle</c> is not "the main window" — it is
        /// the first top-level visible window the OS enumerates for the process.
        /// Once one of our forms is open, that can be <i>ours</i>, and asking a
        /// window to own itself silently leaves it with no owner at all. Sampling
        /// once at start-up, when only Trados' own windows exist, avoids the
        /// question entirely.</para>
        /// </summary>
        public static void CaptureHostMainWindow()
        {
            try
            {
                _hostMainWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                DiagnosticLog.Log("Foreground",
                    _hostMainWindow == IntPtr.Zero
                        ? "Host main window not found at start-up"
                        : $"Host main window captured: 0x{_hostMainWindow.ToInt64():X}");
            }
            catch (Exception ex)
            {
                _hostMainWindow = IntPtr.Zero;
                DiagnosticLog.Log("Foreground", $"Could not capture host main window: {ex.Message}");
            }
        }

        /// <summary>The Trados main window, or IntPtr.Zero if it is not known.</summary>
        public static IntPtr HostMainWindow()
        {
            // Late fallback for the case where start-up capture never ran. Still
            // better than nothing: SetOwner rejects a self-owning handle.
            if (_hostMainWindow == IntPtr.Zero) CaptureHostMainWindow();
            return _hostMainWindow;
        }

        /// <summary>
        /// Raises <paramref name="hwnd"/> to the foreground. Best-effort: returns
        /// false if the interop path failed, never throws.
        /// </summary>
        public static bool Force(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            try
            {
                keybd_event(VK_MENU, 0, 0, IntPtr.Zero);              // Alt down
                keybd_event(VK_F24, 0, 0, IntPtr.Zero);               // F24 down — makes it a chord
                keybd_event(VK_F24, 0, KEYEVENTF_KEYUP, IntPtr.Zero); // F24 up
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, IntPtr.Zero);// Alt up

                var foreground = GetForegroundWindow();
                var foregroundThread = foreground != IntPtr.Zero
                    ? GetWindowThreadProcessId(foreground, IntPtr.Zero)
                    : 0;
                var ourThread = GetCurrentThreadId();

                var attached = false;
                if (foregroundThread != 0 && foregroundThread != ourThread)
                    attached = AttachThreadInput(foregroundThread, ourThread, true);

                try
                {
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                    SwitchToThisWindow(hwnd, true);
                }
                finally
                {
                    if (attached) AttachThreadInput(foregroundThread, ourThread, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("Foreground", $"Could not raise window: {ex.Message}");
                return false;
            }
        }
    }
}
