using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The queue every startup notice goes through: the update-available dialog,
    /// the usage-statistics opt-in, the dev survey and one-off announcements.
    ///
    /// Each of those used to fire its own <c>Task.Run</c> →
    /// <c>ctrl.BeginInvoke(… ShowDialog())</c>, which produced two complaints
    /// (issue #54):
    ///
    /// <list type="number">
    /// <item><description><b>They opened behind Studio.</b> <c>ShowDialog()</c>
    /// with no argument makes WinForms pick the calling thread's <i>active</i>
    /// window as owner — during Studio startup, frequently nothing at all. An
    /// ownerless window has no z-order relationship to Studio's main window, so
    /// Studio ends up on top of it; and since all of these set
    /// <c>ShowInTaskbar = false</c>, there is then no taskbar button to get back
    /// to them. An <b>owned</b> window can never fall behind its owner, which is
    /// the whole fix.</description></item>
    /// <item><description><b>They stacked.</b> <c>ShowDialog</c> pumps a nested
    /// message loop, so the other queued <c>BeginInvoke</c> callbacks ran
    /// <i>inside</i> the first dialog's loop and piled modals on top of each
    /// other — the "they pop up together" the user described, which is also why
    /// a survey appeared next to an update notice when there was no update.
    /// </description></item>
    /// </list>
    ///
    /// So: one at a time, in the order queued, each owned by a real window and
    /// pulled to the front.
    ///
    /// Note what this does <b>not</b> gate on. The old code waited on
    /// <c>TermLensControl.IsHandleCreated</c> and gave up after 15 s. That is
    /// fine while the pane is pinned, but from 20.173 TermLens initialises even
    /// when its pane is never shown (issue #56), and a pane that is never shown
    /// never creates a handle — which would have silently swallowed every notice
    /// for exactly those users. The handle is now a preference, not a
    /// requirement: the UI <see cref="SynchronizationContext"/> captured during
    /// Initialize is the fallback.
    /// </summary>
    internal static class StartupNotices
    {
        private sealed class Pending
        {
            public Control Anchor;
            public string Name;
            public Action<IWin32Window> Show;
        }

        private static readonly object Sync = new object();
        private static readonly Queue<Pending> Waiting = new Queue<Pending>();
        private static bool _showing;
        private static SynchronizationContext _ui;

        /// <summary>
        /// Remembers the UI thread's synchronization context. Call from a member
        /// that Trados runs on the UI thread (ViewPart <c>Initialize</c>); the
        /// context is how a notice reaches the screen when its anchor control has
        /// no window handle.
        /// </summary>
        public static void CaptureUiContext()
        {
            if (_ui == null) _ui = SynchronizationContext.Current;
        }

        /// <summary>
        /// Queues a notice. <paramref name="show"/> is handed the owner window to
        /// pass to <see cref="ShowOwned"/>, and runs on the UI thread with no
        /// other notice on screen.
        /// </summary>
        public static void Enqueue(Control anchor, string name, Action<IWin32Window> show)
        {
            if (show == null) return;

            lock (Sync)
            {
                Waiting.Enqueue(new Pending { Anchor = anchor, Name = name, Show = show });
            }
            Pump();
        }

        /// <summary>
        /// Shows a modal owned by <paramref name="owner"/> and pulls it in front.
        /// Being owned is what keeps it above Studio; the brief TopMost flip is
        /// what gets it in front of whatever else the user has open, since
        /// Windows will not hand the foreground to a process that does not
        /// already have it. Dropping TopMost immediately afterwards leaves the
        /// dialog at the top of the normal z-order rather than permanently above
        /// every other application.
        /// </summary>
        public static DialogResult ShowOwned(Form dlg, IWin32Window owner)
        {
            if (dlg == null) return DialogResult.None;

            dlg.Shown += (s, e) =>
            {
                try
                {
                    dlg.TopMost = true;
                    dlg.BringToFront();
                    dlg.Activate();
                    dlg.TopMost = false;
                }
                catch { }
            };

            return owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
        }

        // ─── plumbing ───────────────────────────────────────────────

        private static void Pump()
        {
            Pending item;
            lock (Sync)
            {
                if (_showing || Waiting.Count == 0) return;
                item = Waiting.Dequeue();
                _showing = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    await WaitForUiAsync(item.Anchor).ConfigureAwait(false);
                    if (!TryPost(item.Anchor, () => Run(item)))
                        Finish();   // nowhere to show it – don't wedge the queue
                }
                catch
                {
                    Finish();
                }
            });
        }

        private static void Run(Pending item)
        {
            try
            {
                item.Show(OwnerFor(item.Anchor));
            }
            catch (Exception ex)
            {
                try { DiagnosticLog.WriteAlways("StartupNotices", (item.Name ?? "notice") + " threw: " + ex.Message); }
                catch { }
            }
            finally
            {
                Finish();
            }
        }

        private static void Finish()
        {
            lock (Sync) { _showing = false; }
            Pump();
        }

        /// <summary>
        /// Waits up to 15 s for the anchor's window handle — the same allowance
        /// each notice used to give it, and a decent proxy for "Studio has
        /// finished laying itself out". Where this differs from the old code is
        /// what happens when the handle never arrives, because the pane was
        /// never shown: that used to abandon the notice, and now simply falls
        /// through to the captured UI context.
        /// </summary>
        private static async Task WaitForUiAsync(Control anchor)
        {
            for (int i = 0; i < 30; i++)
            {
                if (anchor == null || anchor.IsHandleCreated) return;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        private static bool TryPost(Control anchor, Action action)
        {
            try
            {
                if (anchor != null && anchor.IsHandleCreated)
                {
                    anchor.BeginInvoke(action);
                    return true;
                }
            }
            catch { }

            try
            {
                if (_ui != null)
                {
                    _ui.Post(_ => action(), null);
                    return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// The best owner available: the anchor's own top-level form, else
        /// Studio's main window. Never the "active window" WinForms would guess.
        /// </summary>
        private static IWin32Window OwnerFor(Control anchor)
        {
            try
            {
                var form = anchor?.FindForm();
                if (form != null && form.IsHandleCreated) return form;
            }
            catch { }

            try
            {
                var handle = Process.GetCurrentProcess().MainWindowHandle;
                if (handle != IntPtr.Zero) return new HandleWindow(handle);
            }
            catch { }

            return null;
        }

        private sealed class HandleWindow : IWin32Window
        {
            public HandleWindow(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }
    }
}
