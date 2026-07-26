using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Supervertaler.Trados.Controls;

namespace Supervertaler.Trados.VoiceControl
{
    /// <summary>
    /// The glue: owns the engine, executor, command set and status display,
    /// and exposes the single Toggle() the action/header button call. First
    /// activation downloads the voice runtime (libvosk + small English
    /// model) with progress shown; every later activation is instant.
    ///
    /// Status display strategy: when the TermLens panel exists, its header
    /// 🎤 button IS the indicator (integrated, covers nothing – heard
    /// commands flash in the panel's status label). The floating strip is
    /// only a fallback for sessions where TermLens isn't open, and is
    /// draggable with its position remembered.
    /// </summary>
    internal sealed class VoiceControlManager
    {
        private static VoiceControlManager _instance;
        public static VoiceControlManager Instance =>
            _instance ?? (_instance = new VoiceControlManager());

        private IVoiceEngine _engine;
        private VoiceStatusWindow _statusWindow;   // fallback display only
        private TermLensControl _hostControl;      // preferred display + marshal target
        private VoiceCommandExecutor _executor;
        private List<VoiceCommand> _commands;
        private volatile bool _running;
        private volatile bool _starting;

        public bool IsRunning => _running;

        public void Toggle()
        {
            if (_running || _starting) Stop();
            else Start();
        }

        /// <summary>Starts listening (downloads the runtime first if needed).</summary>
        public void Start()
        {
            if (_running || _starting) return;
            _starting = true;

            _commands = VoiceCommandSet.Load();
            _executor = new VoiceCommandExecutor();
            _executor.LoadCommands(_commands);
            _executor.CommandExecuted += (phrase, desc) => FlashCommand(phrase);

            // Preferred: the TermLens header hosts the indicator. Fallback:
            // the floating strip (draggable, position remembered).
            _hostControl = TermLensEditorViewPart.TryGetVoiceHost();
            if (_hostControl == null)
            {
                _statusWindow = new VoiceStatusWindow();
                _statusWindow.StopRequested += (s, e) => Stop();
                _statusWindow.AdvancedRequested += (s, e) => ShowAdvancedDialog();
                _statusWindow.Show();
            }
            SetStatus(VoiceRuntimeInstaller.IsInstalled ? "Starting…" : "Setting up (one-time)…", state: 1);

            // Runtime install + model load are seconds-slow – background thread.
            var worker = new Thread(() =>
            {
                try
                {
                    VoiceRuntimeInstaller.EnsureInstalled(msg => SetStatus(msg, state: 1));

                    var engine = new VoskVoiceEngine();
                    engine.Recognized += OnRecognized;
                    engine.Start(VoiceCommandSet.GrammarPhrases(_commands));
                    _engine = engine;

                    _running = true;
                    SetStatus("Listening…", state: 2);
                }
                catch (Exception ex)
                {
                    var marshal = MarshalControl();
                    if (marshal != null && !marshal.IsDisposed)
                    {
                        marshal.BeginInvoke((Action)(() =>
                        {
                            Stop();
                            MessageBox.Show(
                                "Voice commands could not start:\n\n" + ex.Message,
                                "Supervertaler – Voice commands",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                }
                finally
                {
                    _starting = false;
                }
            })
            { IsBackground = true, Name = "Supervertaler.VoiceStart" };
            worker.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _engine?.Dispose(); } catch { }
            _engine = null;

            var host = _hostControl;
            _hostControl = null;
            if (host != null && !host.IsDisposed)
                host.SetVoiceState(0);

            var window = _statusWindow;
            _statusWindow = null;
            if (window != null && !window.IsDisposed)
            {
                if (window.InvokeRequired) window.BeginInvoke((Action)(() => window.Close()));
                else window.Close();
            }
        }

        private Control MarshalControl()
        {
            var host = _hostControl;
            if (host != null && !host.IsDisposed) return host;
            var window = _statusWindow;
            if (window != null && !window.IsDisposed) return window;
            return null;
        }

        private void SetStatus(string text, int state)
        {
            var host = _hostControl;
            if (host != null && !host.IsDisposed)
            {
                host.SetVoiceState(state, text);
                return;
            }
            var window = _statusWindow;
            if (window != null && !window.IsDisposed)
                window.SetStatus(text, listening: state == 2);
        }

        private void FlashCommand(string phrase)
        {
            var host = _hostControl;
            if (host != null && !host.IsDisposed)
            {
                host.FlashVoiceCommand(phrase);
                return;
            }
            var window = _statusWindow;
            if (window != null && !window.IsDisposed)
                window.FlashCommand(phrase);
        }

        private void OnRecognized(string text)
        {
            // Engine thread → UI thread
            var marshal = MarshalControl();
            if (marshal == null) return;
            try
            {
                marshal.BeginInvoke((Action)(() => _executor?.Execute(text)));
            }
            catch { /* host torn down mid-recognition */ }
        }

        /// <summary>Reloads commands (after the Advanced dialog saves) into the live engine.</summary>
        public void ReloadCommands()
        {
            _commands = VoiceCommandSet.Load();
            _executor?.LoadCommands(_commands);
            _engine?.UpdateGrammar(VoiceCommandSet.GrammarPhrases(_commands));
        }

        /// <summary>Opens the Advanced command editor (gear / header right-click).</summary>
        public void ShowAdvancedDialog()
        {
            using (var dlg = new VoiceSettingsDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    ReloadCommands();
            }
        }
    }
}
