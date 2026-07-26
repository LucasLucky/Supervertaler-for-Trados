using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Supervertaler.Trados.VoiceControl
{
    /// <summary>
    /// Microphone capture via the classic winmm waveIn API – 16 kHz, 16-bit,
    /// mono, exactly what libvosk expects. No NAudio dependency (keeps the
    /// plugin package unchanged) and arch-neutral for the x86/x64 builds.
    ///
    /// Deadlock safety: waveInOpen uses CALLBACK_EVENT, not a function
    /// callback – the driver signals an Event when a buffer completes, and a
    /// worker thread harvests WHDR_DONE buffers, hands the audio to the
    /// consumer and requeues them. No delegates are ever called from the
    /// audio driver's thread.
    /// </summary>
    internal sealed class WaveInCapture : IDisposable
    {
        private const int SampleRate = 16000;
        private const int BufferMillis = 100;   // 10 buffers/second
        private const int BufferCount = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag, nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public ushort nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength, dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags, dwLoops;
            public IntPtr lpNext, reserved;
        }

        private const ushort WAVE_FORMAT_PCM = 1;
        private const uint WAVE_MAPPER = 0xFFFFFFFF;
        private const uint CALLBACK_EVENT = 0x00050000;
        private const uint WHDR_DONE = 0x00000001;

        [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr hWaveIn, uint deviceId, ref WAVEFORMATEX format, IntPtr callbackEvent, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr hWaveIn, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr hWaveIn, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr hWaveIn, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr hWaveIn);
        [DllImport("winmm.dll")] private static extern int waveInStop(IntPtr hWaveIn);
        [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr hWaveIn);
        [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr hWaveIn);

        private IntPtr _hWaveIn;
        private IntPtr[] _headers;      // unmanaged WAVEHDR blocks
        private IntPtr[] _buffers;      // unmanaged audio buffers
        private ManualResetEvent _bufferDone;
        private Thread _worker;
        private volatile bool _running;

        /// <summary>Called on the worker thread with each completed audio chunk.</summary>
        public event Action<byte[], int> DataAvailable;

        /// <summary>Opens the default microphone and starts capturing.</summary>
        public void Start()
        {
            if (_running) return;

            var format = new WAVEFORMATEX
            {
                wFormatTag = WAVE_FORMAT_PCM,
                nChannels = 1,
                nSamplesPerSec = SampleRate,
                wBitsPerSample = 16,
                nBlockAlign = 2,
                nAvgBytesPerSec = SampleRate * 2,
                cbSize = 0
            };

            _bufferDone = new ManualResetEvent(false);
            int hr = waveInOpen(out _hWaveIn, WAVE_MAPPER, ref format,
                _bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
            if (hr != 0)
                throw new InvalidOperationException(
                    "Could not open the microphone (waveInOpen error " + hr + "). " +
                    "Check that a recording device is available in Windows sound settings.");

            int bytesPerBuffer = SampleRate * 2 * BufferMillis / 1000;
            int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
            _headers = new IntPtr[BufferCount];
            _buffers = new IntPtr[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                _buffers[i] = Marshal.AllocHGlobal(bytesPerBuffer);
                var hdr = new WAVEHDR { lpData = _buffers[i], dwBufferLength = (uint)bytesPerBuffer };
                _headers[i] = Marshal.AllocHGlobal(hdrSize);
                Marshal.StructureToPtr(hdr, _headers[i], false);
                waveInPrepareHeader(_hWaveIn, _headers[i], hdrSize);
                waveInAddBuffer(_hWaveIn, _headers[i], hdrSize);
            }

            _running = true;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Supervertaler.VoiceCapture" };
            _worker.Start();
            waveInStart(_hWaveIn);
        }

        private void WorkerLoop()
        {
            int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
            var managed = new byte[SampleRate * 2 * BufferMillis / 1000];

            while (_running)
            {
                _bufferDone.WaitOne(200);
                _bufferDone.Reset();
                if (!_running) break;

                for (int i = 0; i < BufferCount; i++)
                {
                    var hdr = (WAVEHDR)Marshal.PtrToStructure(_headers[i], typeof(WAVEHDR));
                    if ((hdr.dwFlags & WHDR_DONE) == 0) continue;

                    int recorded = (int)hdr.dwBytesRecorded;
                    if (recorded > 0)
                    {
                        if (managed.Length < recorded) managed = new byte[recorded];
                        Marshal.Copy(hdr.lpData, managed, 0, recorded);
                        try { DataAvailable?.Invoke(managed, recorded); }
                        catch { /* consumer errors must not kill capture */ }
                    }

                    // Requeue the buffer (from OUR thread – safe)
                    waveInUnprepareHeader(_hWaveIn, _headers[i], hdrSize);
                    hdr.dwFlags = 0; hdr.dwBytesRecorded = 0;
                    Marshal.StructureToPtr(hdr, _headers[i], false);
                    waveInPrepareHeader(_hWaveIn, _headers[i], hdrSize);
                    if (_running) waveInAddBuffer(_hWaveIn, _headers[i], hdrSize);
                }
            }
        }

        public void Dispose()
        {
            if (!_running && _hWaveIn == IntPtr.Zero) return;
            _running = false;

            try { waveInStop(_hWaveIn); } catch { }
            try { waveInReset(_hWaveIn); } catch { }   // returns all buffers
            try { _bufferDone?.Set(); } catch { }
            try { _worker?.Join(1000); } catch { }

            int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
            if (_headers != null)
            {
                for (int i = 0; i < _headers.Length; i++)
                {
                    if (_headers[i] != IntPtr.Zero)
                    {
                        try { waveInUnprepareHeader(_hWaveIn, _headers[i], hdrSize); } catch { }
                        Marshal.FreeHGlobal(_headers[i]);
                    }
                    if (_buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_buffers[i]);
                }
                _headers = null; _buffers = null;
            }

            try { waveInClose(_hWaveIn); } catch { }
            _hWaveIn = IntPtr.Zero;
            try { _bufferDone?.Dispose(); } catch { }
            _bufferDone = null;
        }
    }
}
