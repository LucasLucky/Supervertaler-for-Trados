using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Supervertaler.Trados
{
    /// <summary>
    /// WH_KEYBOARD_LL hook that dismisses Supervertaler pop-up surfaces on
    /// Escape. Lowest interception level Windows offers: keys arrive here
    /// before they are posted to any thread queue, so whatever Studio's input
    /// pipeline does afterwards cannot starve us (both the IMessageFilter and
    /// a WH_GETMESSAGE hook measurably never saw Escape at all). Installed
    /// from inside the Trados process, so it works when Studio runs elevated -
    /// UIPI only blocks OTHER, lower-integrity processes.
    ///
    /// Scope guard: only acts when the foreground window belongs to this
    /// process AND the dismiss callback reports it actually closed something.
    /// In every other case the key passes through untouched, including to
    /// other applications.
    /// </summary>
    internal sealed class EscapeKeyHookLL : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_ESCAPE = 0x1B;

        private readonly Func<bool> _onEscape;
        private readonly int _ownPid;
        private IntPtr _hookId;
        private LowLevelKeyboardProc _proc;   // held so the GC can't collect it

        public EscapeKeyHookLL(Func<bool> onEscape)
        {
            _onEscape = onEscape ?? throw new ArgumentNullException(nameof(onEscape));
            _ownPid = Process.GetCurrentProcess().Id;
            _proc = HookProc;
            using (var mod = Process.GetCurrentProcess().MainModule)
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                    GetModuleHandle(mod.ModuleName), 0);
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if (info.vkCode == VK_ESCAPE)
                {
                    // Only when Trados itself is the foreground process - never
                    // touch Escape typed into any other application.
                    uint fgPid = 0;
                    try { GetWindowThreadProcessId(GetForegroundWindow(), out fgPid); }
                    catch { }

                    bool ours = fgPid == (uint)_ownPid;
                    bool handled = false;
                    if (ours)
                    {
                        try { handled = _onEscape(); }
                        catch { handled = false; }
                    }
                    if (handled)
                        return (IntPtr)1;   // swallow: the Escape did its job
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _proc = null;
        }
    }
}
