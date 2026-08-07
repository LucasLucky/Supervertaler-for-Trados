using System;
using System.Collections.Generic;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Per-endpoint tally of the bytes the bridge has moved this session, so the
    /// cost of an MCP conversation can be measured instead of guessed.
    ///
    /// Why this exists: a chat client re-sends its entire transcript on every
    /// turn, so every byte a tool returns is billed once when it arrives and
    /// again on every subsequent turn. The plugin cannot see the AI's token
    /// counts, but it can see exactly what it emits – and that is the half that
    /// is actually under our control. Bytes/4 is a rough token proxy; it is
    /// deliberately not presented as anything more precise.
    ///
    /// Both directions are counted. Responses dominate (get_segments and
    /// friends), but request bodies matter too: update_segments carries full
    /// target text inbound, which is the case for offering a patch-style edit
    /// instead.
    ///
    /// Scope is the bridge's lifetime – i.e. from when Trados started with the
    /// plugin enabled – not one chat. Callers wanting to measure a single
    /// conversation reset the counters at its start (session_report reset=true).
    ///
    /// Thread-safe: HttpListener may dispatch concurrently.
    /// </summary>
    public static class BridgePayloadLedger
    {
        /// <summary>One endpoint's tally. Key is the bridge path, plus
        /// "?type=<c>x</c>" where several tools share one endpoint (the QA
        /// checks all live on /v1/qa-check).</summary>
        public sealed class Entry
        {
            public string Key;
            public int Calls;
            public long ResponseBytes;
            public long RequestBytes;
            /// <summary>Largest single response – finds the one call that
            /// poisoned a conversation, which an average hides.</summary>
            public long MaxResponseBytes;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static DateTime _sinceUtc = DateTime.UtcNow;

        /// <summary>When the current measurement window opened (bridge start, or the last reset).</summary>
        public static DateTime SinceUtc
        {
            get { lock (Sync) { return _sinceUtc; } }
        }

        /// <summary>Record an inbound request body. Call count is attributed on
        /// the response side, so a request that never gets answered does not
        /// inflate the call total.</summary>
        public static void RecordRequest(string key, long bytes)
        {
            if (string.IsNullOrEmpty(key) || bytes <= 0) return;
            lock (Sync)
            {
                Get(key).RequestBytes += bytes;
            }
        }

        /// <summary>Record an outbound response body.</summary>
        public static void RecordResponse(string key, long bytes)
        {
            if (string.IsNullOrEmpty(key) || bytes < 0) return;
            lock (Sync)
            {
                var e = Get(key);
                e.Calls++;
                e.ResponseBytes += bytes;
                if (bytes > e.MaxResponseBytes) e.MaxResponseBytes = bytes;
            }
        }

        /// <summary>Copy of the tallies, heaviest response payload first.</summary>
        public static List<Entry> Snapshot()
        {
            var copy = new List<Entry>();
            lock (Sync)
            {
                foreach (var e in Entries.Values)
                {
                    copy.Add(new Entry
                    {
                        Key = e.Key,
                        Calls = e.Calls,
                        ResponseBytes = e.ResponseBytes,
                        RequestBytes = e.RequestBytes,
                        MaxResponseBytes = e.MaxResponseBytes
                    });
                }
            }
            copy.Sort((a, b) => b.ResponseBytes.CompareTo(a.ResponseBytes));
            return copy;
        }

        /// <summary>Zero everything and reopen the measurement window.</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                Entries.Clear();
                _sinceUtc = DateTime.UtcNow;
            }
        }

        /// <summary>Caller must hold Sync.</summary>
        private static Entry Get(string key)
        {
            Entry e;
            if (!Entries.TryGetValue(key, out e))
            {
                e = new Entry { Key = key };
                Entries[key] = e;
            }
            return e;
        }
    }
}
