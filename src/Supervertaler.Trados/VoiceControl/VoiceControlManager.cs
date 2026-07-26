using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Supervertaler.Trados.VoiceControl
{
    /// <summary>
    /// The glue: owns the engine, executor, command set and status window,
    /// and exposes the single Toggle() the ribbon action calls. First
    /// activation downloads the voice runtime (libvosk + small English
    /// model) with progress in the status strip; every later activation is
    /// instant. All recognition callbacks are marshalled to the UI thread
    /// via the status window.
    /// </summary>
    internal sealed class VoiceControlManager
    {
        private static VoiceControlManager _instance;
        public static VoiceControlManager Instance =>
            _instance ?? (_instance = new VoiceControlManager());

        private IVoiceEngine _engine;
        private VoiceStatusWindow _statusWindow;
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
            _executor.CommandExecuted += (phrase, desc) => _statusWindow?.FlashCommand(phrase);

            _statusWindow = new VoiceStatusWindow();
            _statusWindow.StopRequested += (s, e) => Stop();
            _statusWindow.AdvancedRequested += (s, e) => ShowAdvancedDialog();
            _statusWindow.Show();
            _statusWindow.SetStatus(VoiceRuntimeInstaller.IsInstalled ? "Starting…" : "Setting up (one-time)…", listening: false);

            // Runtime install + model load are seconds-slow – background thread.
            var worker = new Thread(() =>
            {
                try
                {
                    VoiceRuntimeInstaller.EnsureInstalled(msg => _statusWindow?.SetStatus(msg, listening: false));

                    var engine = new VoskVoiceEngine();
                    engine.Recognized += OnRecognized;
                    engine.Start(VoiceCommandSet.GrammarPhrases(_commands));
                    _engine = engine;

                    _running = true;
                    _statusWindow?.SetStatus("Listening…", listening: true);
                }
                catch (Exception ex)
                {
                    var window = _statusWindow;
                    if (window != null && !window.IsDisposed)
                    {
                        window.BeginInvoke((Action)(() =>
                        {
                            Stop();
                            MessageBox.Show(
                                "Voice commands could not start:\n\n" + ex.Message,
                                "Supervertaler — Voice commands",
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

            var window = _statusWindow;
            _statusWindow = null;
            if (window != null && !window.IsDisposed)
            {
                if (window.InvokeRequired) window.BeginInvoke((Action)(() => window.Close()));
                else window.Close();
            }
        }

        private void OnRecognized(string text)
        {
            // Engine thread → UI thread
            var window = _statusWindow;
            if (window == null || window.IsDisposed) return;
            try
            {
                window.BeginInvoke((Action)(() => _executor?.Execute(text)));
            }
            catch { /* window torn down mid-recognition */ }
        }

        /// <summary>Reloads commands (after the Advanced dialog saves) into the live engine.</summary>
        public void ReloadCommands()
        {
            _commands = VoiceCommandSet.Load();
            _executor?.LoadCommands(_commands);
            _engine?.UpdateGrammar(VoiceCommandSet.GrammarPhrases(_commands));
        }

        private void ShowAdvancedDialog()
        {
            using (var dlg = new VoiceSettingsDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    ReloadCommands();
            }
        }
    }
}
