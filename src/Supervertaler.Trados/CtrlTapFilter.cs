using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Supervertaler.Trados
{
    /// <summary>
    /// IMessageFilter that detects a Ctrl "tap" – pressing and releasing the
    /// Ctrl key without any other key pressed in between.  This mimics memoQ's
    /// behaviour where a quick Ctrl tap opens the term picker.
    ///
    /// The filter watches WM_KEYDOWN / WM_KEYUP for VK_CONTROL (and VK_LCONTROL /
    /// VK_RCONTROL).  If Ctrl goes down and then up without any intervening
    /// WM_KEYDOWN for a non-modifier key, the callback fires.
    ///
    /// A maximum hold duration (default 400 ms) prevents long Ctrl holds from
    /// triggering.  This avoids false positives when the user holds Ctrl to
    /// reach for another key (Ctrl+C, Ctrl+V, etc.) but is slow.
    /// </summary>
    internal sealed class CtrlTapFilter : IMessageFilter
    {
        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_KEYUP      = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP   = 0x0105;

        private const int VK_CONTROL  = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_SHIFT    = 0x10;
        private const int VK_LSHIFT   = 0xA0;
        private const int VK_RSHIFT   = 0xA1;
        private const int VK_MENU     = 0x12;  // Alt
        private const int VK_LMENU    = 0xA4;
        private const int VK_RMENU    = 0xA5;
        private const int VK_LWIN     = 0x5B;
        private const int VK_RWIN     = 0x5C;
        private const int VK_ESCAPE   = 0x1B;

        private readonly Action _onCtrlTap;
        private readonly Func<bool> _onEscape;
        private readonly int _maxHoldMs;

        private bool _ctrlDown;
        private bool _otherKeyPressed;
        private DateTime _ctrlDownTime;

        /// <param name="onEscape">Optional. Called on a plain Escape key-down;
        /// return true to CONSUME the keypress (used to close the TermLens
        /// popup, which is WS_EX_NOACTIVATE and therefore never receives keys
        /// itself). Return false to let Escape reach Studio untouched - which
        /// is what happens whenever no Supervertaler popup is open, so
        /// Studio's own Escape behaviour is unaffected.</param>
        public CtrlTapFilter(Action onCtrlTap, int maxHoldMs = 400, Func<bool> onEscape = null)
        {
            _onCtrlTap = onCtrlTap ?? throw new ArgumentNullException(nameof(onCtrlTap));
            _onEscape = onEscape;
            _maxHoldMs = maxHoldMs;
        }

        public bool PreFilterMessage(ref Message m)
        {
            int msg = m.Msg;
            // m.WParam is IntPtr (64-bit under x64). Direct (int) cast throws
            // OverflowException whenever WPARAM has any high bits set — common
            // with system key events on x64. Read as Int64 first, then mask to
            // the low byte (the virtual-key code) before narrowing.
            int vk  = (int)(m.WParam.ToInt64() & 0xFF);

            // Plain Escape (WM_KEYDOWN only - Alt+Escape is a Windows window
            // switcher and must not be touched): give the owner a chance to
            // close an open popup. Consumed only when it actually closed one.
            if (msg == WM_KEYDOWN && vk == VK_ESCAPE && _onEscape != null)
            {
                if (_onEscape()) return true;
            }

            // --- Key down ---
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                if (IsCtrlKey(vk))
                {
                    if (!_ctrlDown)
                    {
                        _ctrlDown = true;
                        _otherKeyPressed = false;
                        _ctrlDownTime = DateTime.UtcNow;
                    }
                }
                else if (!IsModifierKey(vk))
                {
                    // A non-modifier key was pressed while Ctrl is held –
                    // this is a combo (Ctrl+C, Ctrl+V, etc.), not a tap.
                    _otherKeyPressed = true;
                }
            }

            // --- Key up ---
            if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                if (IsCtrlKey(vk) && _ctrlDown)
                {
                    _ctrlDown = false;
                    double held = (DateTime.UtcNow - _ctrlDownTime).TotalMilliseconds;

                    // Voice keystroke commands synthesise Ctrl-modified chords
                    // via SendKeys ("zoom in" → Ctrl+Alt+PgUp). When Studio's
                    // accelerator handling consumes the middle key before this
                    // filter sees it, the synthetic Ctrl down/up pair looks
                    // exactly like a user tap and would pop the TermLens popup.
                    // Ignore taps within a short window of a synthetic send.
                    bool syntheticWindow =
                        (DateTime.UtcNow - VoiceControl.VoiceCommandExecutor.LastSyntheticKeystrokeUtc)
                            .TotalMilliseconds < 750;

                    if (!_otherKeyPressed && held <= _maxHoldMs && !syntheticWindow)
                    {
                        // Pure Ctrl tap detected – fire callback
                        _onCtrlTap();
                    }

                    _otherKeyPressed = false;
                }
            }

            // Never consume the message – let it propagate normally
            return false;
        }

        private static bool IsCtrlKey(int vk)
            => vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;

        private static bool IsModifierKey(int vk)
            => vk == VK_CONTROL  || vk == VK_LCONTROL || vk == VK_RCONTROL
            || vk == VK_SHIFT    || vk == VK_LSHIFT   || vk == VK_RSHIFT
            || vk == VK_MENU     || vk == VK_LMENU    || vk == VK_RMENU
            || vk == VK_LWIN     || vk == VK_RWIN;
    }
}
