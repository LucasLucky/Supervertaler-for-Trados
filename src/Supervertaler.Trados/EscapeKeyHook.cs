using System;
using System.Runtime.InteropServices;

namespace Supervertaler.Trados
{
    /// <summary>
    /// WH_GETMESSAGE thread hook that watches for the Escape key and dismisses
    /// whichever Supervertaler pop-up surface is currently showing.
    ///
    /// Why a hook and not an IMessageFilter: measured in Studio 2026, the
    /// WinForms filter chain sees modifier keys (that is how the Ctrl-tap
    /// works) but NEVER sees WM_KEYDOWN for Escape - the editor's own
    /// preprocessing consumes dialog-navigation keys before the filter runs.
    /// A GetMessage hook fires when the message is pulled from the thread
    /// queue, before any of that, which is also how ChatInputTextBox kills
    /// Enter. Eating the message (rewriting it to WM_NULL) prevents Studio
    /// from also acting on the Escape that closed our pop-up.
    ///
    /// The callback decides whether anything was dismissed; when it returns
    /// false the message passes through untouched, so Studio's own Escape
    /// behaviour is unaffected whenever no Supervertaler surface is open.
    /// </summary>
    internal sealed class EscapeKeyHook : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            public int pt_x;
            public int pt_y;
        }

        private const int WH_GETMESSAGE = 3;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_NULL = 0x0000;
        private const int VK_ESCAPE = 0x1B;
        private const int HC_ACTION = 0;
        private const int PM_REMOVE = 0x0001;

        private readonly Func<bool> _onEscape;
        private IntPtr _hookId;
        private HookProc _hookDelegate;   // held so the GC can't collect it

        /// <summary>Install on the UI thread. <paramref name="onEscape"/> runs
        /// for every Escape key-down pulled from this thread's queue; return
        /// true to eat the keypress (something was dismissed).</summary>
        public EscapeKeyHook(Func<bool> onEscape)
        {
            _onEscape = onEscape ?? throw new ArgumentNullException(nameof(onEscape));
            _hookDelegate = GetMsgHookProc;
            _hookId = SetWindowsHookEx(WH_GETMESSAGE, _hookDelegate, IntPtr.Zero, GetCurrentThreadId());
        }

        private IntPtr GetMsgHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION && (int)wParam == PM_REMOVE)
            {
                var msg = (MSG)Marshal.PtrToStructure(lParam, typeof(MSG));

                if (msg.message == WM_KEYDOWN && (msg.wParam.ToInt64() & 0xFF) == VK_ESCAPE)
                {
                    bool handled;
                    try { handled = _onEscape(); }
                    catch { handled = false; }   // never let an error break the pump

                    if (handled)
                    {
                        msg.message = WM_NULL;
                        Marshal.StructureToPtr(msg, lParam, false);
                    }
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
            _hookDelegate = null;
        }
    }
}
