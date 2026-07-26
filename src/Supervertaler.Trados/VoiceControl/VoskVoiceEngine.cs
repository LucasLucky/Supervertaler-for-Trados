using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Supervertaler.Trados.VoiceControl
{
    /// <summary>
    /// Engine abstraction so alternative recognisers (SAPI, cloud) could be
    /// slotted in later without touching the manager/executor.
    /// </summary>
    internal interface IVoiceEngine : IDisposable
    {
        /// <summary>Raised on a background thread with each recognised phrase.</summary>
        event Action<string> Recognized;
        void Start(List<string> grammarPhrases);
        void UpdateGrammar(List<string> grammarPhrases);
        void Stop();
    }

    /// <summary>
    /// Vosk in grammar mode – the recogniser is constrained to the command
    /// phrases (plus "[unk]" for everything else), which is what makes
    /// commands fast (~30 ms) and near-perfect: it literally cannot
    /// mis-hear a command as anything but another command or [unk].
    /// Same approach as Workbench's ContinuousVoiceListener.
    /// </summary>
    internal sealed class VoskVoiceEngine : IVoiceEngine
    {
        private IntPtr _model;
        private IntPtr _recognizer;
        private WaveInCapture _capture;
        private readonly object _lock = new object();

        public event Action<string> Recognized;

        public void Start(List<string> grammarPhrases)
        {
            if (!VoskNative.Preload(VoiceRuntimeInstaller.LibVoskPath))
                throw new InvalidOperationException("libvosk.dll could not be loaded.");

            VoskNative.vosk_set_log_level(-1); // silence libvosk's stderr chatter

            _model = VoskNative.vosk_model_new(VoskNative.Utf8(VoiceRuntimeInstaller.ModelDir));
            if (_model == IntPtr.Zero)
                throw new InvalidOperationException("The voice model could not be loaded (it may be corrupt – delete the trados/voice/models folder to re-download).");

            _recognizer = VoskNative.vosk_recognizer_new_grm(_model, 16000f, GrammarJson(grammarPhrases));
            if (_recognizer == IntPtr.Zero)
                throw new InvalidOperationException("The voice recogniser could not be created.");

            _capture = new WaveInCapture();
            _capture.DataAvailable += OnAudio;
            _capture.Start();
        }

        /// <summary>Rebuilds the grammar live after the command set changes.</summary>
        public void UpdateGrammar(List<string> grammarPhrases)
        {
            lock (_lock)
            {
                if (_model == IntPtr.Zero) return;
                var fresh = VoskNative.vosk_recognizer_new_grm(_model, 16000f, GrammarJson(grammarPhrases));
                if (fresh == IntPtr.Zero) return;
                var old = _recognizer;
                _recognizer = fresh;
                if (old != IntPtr.Zero) VoskNative.vosk_recognizer_free(old);
            }
        }

        private static byte[] GrammarJson(List<string> phrases)
        {
            // Hand-rolled JSON array – phrases are plain lowercase words, but
            // escape quotes/backslashes defensively.
            var sb = new StringBuilder("[");
            foreach (var p in phrases.Concat(new[] { "[unk]" }))
            {
                if (sb.Length > 1) sb.Append(',');
                sb.Append('"').Append(p.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            sb.Append(']');
            return VoskNative.Utf8(sb.ToString());
        }

        private void OnAudio(byte[] data, int length)
        {
            string resultJson = null;
            lock (_lock)
            {
                if (_recognizer == IntPtr.Zero) return;
                // Returns 1 when an utterance ended (silence after speech)
                if (VoskNative.vosk_recognizer_accept_waveform(_recognizer, data, length) == 1)
                    resultJson = VoskNative.ReadUtf8(VoskNative.vosk_recognizer_result(_recognizer));
            }
            if (resultJson == null) return;

            var text = ExtractText(resultJson);
            if (string.IsNullOrWhiteSpace(text)) return;
            // Strip [unk] tokens; if nothing else remains, it wasn't a command
            text = text.Replace("[unk]", " ").Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            if (text.Length == 0) return;

            try { Recognized?.Invoke(text); } catch { }
        }

        /// <summary>Pulls "text" out of Vosk's {"text" : "..."} result JSON.</summary>
        internal static string ExtractText(string json)
        {
            var idx = json.IndexOf("\"text\"", StringComparison.Ordinal);
            if (idx < 0) return "";
            var colon = json.IndexOf(':', idx);
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        public void Stop()
        {
            try { _capture?.Dispose(); } catch { }
            _capture = null;
            lock (_lock)
            {
                if (_recognizer != IntPtr.Zero) { VoskNative.vosk_recognizer_free(_recognizer); _recognizer = IntPtr.Zero; }
                if (_model != IntPtr.Zero) { VoskNative.vosk_model_free(_model); _model = IntPtr.Zero; }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
