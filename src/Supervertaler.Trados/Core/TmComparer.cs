using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Sdl.LanguagePlatform.TranslationMemory;
using Sdl.LanguagePlatform.TranslationMemoryApi;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Enumerates a translation memory and diffs it against the open document.
    ///
    /// Why this exists: concordance search answers "have I seen this phrase
    /// before?", one query at a time. It cannot answer the question a translator
    /// actually has before delivery — "across this whole file, where does my
    /// translation differ from the client's reference TM?" — because that is a
    /// join over every segment, not a lookup. Field report: a term coined in
    /// good faith already had an established rendering in the client's own TM,
    /// and no amount of concordance searching would have surfaced it, because
    /// you only search for what you already suspect.
    ///
    /// The join runs HERE rather than in the AI's context. Shipping a whole TM
    /// over the bridge so the model can diff it would cost a fortune in tokens
    /// and scale terribly — the reference TM in that report held 1,490 units and
    /// a master TM is far larger. Enumerating once into a lookup and returning
    /// only the deviations turns the most valuable pre-delivery check into one
    /// cheap call.
    /// </summary>
    public static class TmComparer
    {
        /// <summary>How many TUs to pull per SDK round trip.</summary>
        private const int BatchSize = 200;

        /// <summary>
        /// Collapses ordinary whitespace runs to a single space and trims.
        ///
        /// Deliberately leaves U+00A0 alone. Collapsing it would fold a
        /// non-breaking space into a plain one and hide exactly the difference
        /// the nbsp QA check exists to find — a target that lost its
        /// non-breaking spaces would compare as identical to the TM.
        /// </summary>
        public static string NormaliseWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (var c in text)
            {
                // U+00A0 is intentionally NOT treated as whitespace here.
                bool isPlainSpace = c == ' ' || c == '\t' || c == '\r' || c == '\n';
                if (isPlainSpace) { pendingSpace = sb.Length > 0; continue; }
                if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>One source→target pair as stored in the TM.</summary>
        public sealed class TmPair
        {
            public string Source;
            public string Target;
        }

        /// <summary>
        /// Walks every translation unit in a TM entry (a file <c>.sdltm</c> path
        /// or a GroupShare <c>sdltm.http…</c> URI), calling <paramref name="onPair"/>
        /// for each. Stops early on <paramref name="maxUnits"/> or
        /// <paramref name="deadline"/> and reports that via the return value, so
        /// a huge master TM degrades into a partial answer instead of hanging
        /// past the caller's HTTP timeout.
        /// </summary>
        /// <returns>true when the whole TM was read; false when cut short.</returns>
        public static bool Enumerate(string tmEntry, Action<TmPair> onPair,
            int maxUnits, TimeSpan deadline, out int unitsRead, out string error)
        {
            unitsRead = 0;
            error = null;
            var sw = Stopwatch.StartNew();
            var complete = true;

            try
            {
                var directions = new List<ITranslationMemoryLanguageDirection>();
                if (ServerTmClient.IsServerTmUri(tmEntry))
                {
                    if (!ServerTmClient.TryParseServerTmUri(tmEntry, out var sref))
                    {
                        error = "could not parse the GroupShare TM URI";
                        return false;
                    }
                    foreach (var ld in ServerTmClient.OpenLanguageDirections(sref))
                        if (ld != null) directions.Add(ld);
                }
                else
                {
                    var tm = new FileBasedTranslationMemory(tmEntry);
                    if (tm.LanguageDirection != null) directions.Add(tm.LanguageDirection);
                }

                if (directions.Count == 0)
                {
                    error = "the TM has no readable language direction";
                    return false;
                }

                foreach (var ld in directions)
                {
                    var iterator = new RegularIterator(BatchSize);
                    while (true)
                    {
                        if (unitsRead >= maxUnits || sw.Elapsed > deadline)
                        {
                            complete = false;
                            break;
                        }

                        var batch = ld.GetTranslationUnits(ref iterator);
                        if (batch == null || batch.Length == 0) break;

                        foreach (var tu in batch)
                        {
                            if (tu?.SourceSegment == null || tu.TargetSegment == null) continue;
                            unitsRead++;
                            onPair(new TmPair
                            {
                                Source = tu.SourceSegment.ToPlain(),
                                Target = tu.TargetSegment.ToPlain()
                            });
                        }
                    }
                    if (!complete) break;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            return complete;
        }

        /// <summary>
        /// Builds a source→targets lookup from a TM. The key is whitespace-
        /// normalised and case-insensitive so trivial differences still pair up;
        /// the stored targets keep their exact characters so the comparison
        /// itself stays faithful.
        /// </summary>
        public static Dictionary<string, List<string>> BuildIndex(string tmEntry,
            int maxUnits, TimeSpan deadline, out int unitsRead, out bool complete, out string error)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            complete = Enumerate(tmEntry, pair =>
            {
                var key = NormaliseWhitespace(pair.Source);
                if (key.Length == 0) return;
                List<string> targets;
                if (!index.TryGetValue(key, out targets))
                {
                    targets = new List<string>();
                    index[key] = targets;
                }
                var t = NormaliseWhitespace(pair.Target);
                if (t.Length > 0 && !targets.Contains(t, StringComparer.Ordinal))
                    targets.Add(t);
            }, maxUnits, deadline, out unitsRead, out error);
            return index;
        }
    }
}
