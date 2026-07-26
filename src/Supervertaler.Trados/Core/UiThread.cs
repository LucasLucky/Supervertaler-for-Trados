using System;
using System.Threading;
using System.Windows.Forms;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Reliable UI-thread marshalling for code that runs on background threads
    /// (the Supervertaler bridge's HttpListener threads, in particular).
    ///
    /// WHY THIS EXISTS – the WinForms trap that caused
    /// "This method/property must be called on the UI thread" from MCP tools:
    /// <c>Control.InvokeRequired</c> returns <b>false</b> when the control has
    /// no window handle yet, because there is no handle whose owning thread it
    /// could compare against (it walks the parent chain and gives up). The
    /// bridge delegates guarded their Studio API calls with
    /// <c>_control.Value.InvokeRequired</c>, where _control is the AI Assistant
    /// panel – lazily created, and its handle only created once Trados actually
    /// shows the pane. A user who works in TermLens and never opens the
    /// Assistant pane therefore got InvokeRequired == false on a bridge thread,
    /// the call went straight through on that thread, and Studio's API threw.
    /// (add_comment surfaced it first because AddCommentOnSegment enforces the
    /// UI-thread check strictly; several read paths got away with it.)
    ///
    /// <see cref="Install"/> is called once from AppInitializer.Execute(), which
    /// Trados runs on the UI thread at startup. It records that thread and
    /// creates a hidden, handle-forced control to marshal through – so
    /// marshalling works from the first bridge request, whether or not any
    /// panel has ever been opened.
    /// </summary>
    internal static class UiThread
    {
        private static int _uiThreadId = -1;
        private static Control _marshaller;

        /// <summary>
        /// Call once on the UI thread (AppInitializer). Idempotent and never
        /// throws – if it fails, <see cref="Invoke{T}"/> falls back to the
        /// caller's own marshalling target.
        /// </summary>
        public static void Install()
        {
            try
            {
                if (_marshaller != null) return;
                _uiThreadId = Thread.CurrentThread.ManagedThreadId;

                // A parentless control creates its own window handle on first
                // access to .Handle – that handle belongs to this (UI) thread,
                // which is exactly what Invoke needs. Never shown.
                var ctrl = new Control();
                var forceHandleCreation = ctrl.Handle;
                GC.KeepAlive(forceHandleCreation);
                _marshaller = ctrl;
            }
            catch
            {
                _marshaller = null;
            }
        }

        /// <summary>True when the calling thread is NOT the Studio UI thread.</summary>
        public static bool InvokeRequired =>
            _uiThreadId >= 0 && Thread.CurrentThread.ManagedThreadId != _uiThreadId;

        /// <summary>Whether a usable marshalling target exists.</summary>
        public static bool IsAvailable
        {
            get
            {
                var m = _marshaller;
                return m != null && !m.IsDisposed && m.IsHandleCreated;
            }
        }

        /// <summary>
        /// Runs <paramref name="func"/> on the UI thread and returns its result.
        /// Executes inline when already on the UI thread, or when no marshaller
        /// is available (in which case the caller is no worse off than before).
        /// </summary>
        public static T Invoke<T>(Func<T> func)
        {
            if (func == null) return default(T);
            if (!InvokeRequired || !IsAvailable) return func();
            try
            {
                return (T)_marshaller.Invoke(func);
            }
            catch (InvalidOperationException)
            {
                // Marshaller lost its handle (shutdown) – best effort inline.
                return func();
            }
        }

        /// <summary>Runs <paramref name="action"/> on the UI thread (blocking).</summary>
        public static void Invoke(Action action)
        {
            if (action == null) return;
            if (!InvokeRequired || !IsAvailable) { action(); return; }
            try { _marshaller.Invoke(action); }
            catch (InvalidOperationException) { action(); }
        }
    }
}
