using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Supervertaler.Trados.Models;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Searches the user's terminology for SuperSearch, across all three kinds
    /// of termbase the plugin can read:
    ///
    ///   * Supervertaler termbases  – the shared SQLite database (all of them,
    ///     with inactive ones flagged rather than hidden);
    ///   * MultiTerm <c>.sdltb</c>  – the Trados project's own termbases;
    ///   * Trados <c>.ttb</c>       – the Studio 2026 format, read through the
    ///     same <see cref="ITermbaseReader"/> abstraction.
    ///
    /// Why it lives inside SuperSearch rather than in a panel of its own:
    /// "where does this phrase appear?" and "what have I called this term?" are
    /// the same question at different granularities, and answering them in two
    /// places means searching twice. Results reuse <see cref="SearchResult"/>
    /// with <see cref="ResultKind.TermbaseEntry"/>, so the existing grid,
    /// filtering and export paths need no special cases beyond disabling
    /// replace (a term is not a document location).
    ///
    /// Matching goes through <see cref="XliffSearcher.QueryMatches"/> – the same
    /// predicate the file and TM searches use – so the Aa / .* / Word options
    /// behave identically no matter which scope is selected.
    /// </summary>
    public static class TermbaseSearcher
    {
        /// <summary>Cap per termbase, so a 90k-term database can't flood the grid.</summary>
        private const int MaxHitsPerTermbase = 500;

        /// <summary>Describes one searchable termbase for the caller's UI.</summary>
        public class TermbaseSource
        {
            /// <summary>Display name, shown in the File/TM column.</summary>
            public string Name;
            /// <summary>"Supervertaler", "MultiTerm" or "TTB" – shown in Status.</summary>
            public string Kind;
            /// <summary>File path for .sdltb/.ttb; the shared DB path for Supervertaler.</summary>
            public string Path;
            /// <summary>Supervertaler termbase id; -1 for file-based termbases.</summary>
            public long SupervertalerId = -1;
            public string SourceIndexName;
            public string TargetIndexName;
            /// <summary>False when the termbase's Read tick is off (Supervertaler)
            /// or it is disabled in Trados Project Settings.</summary>
            public bool Active = true;
        }

        /// <summary>
        /// Lists every termbase SuperSearch can search: the Supervertaler ones
        /// from the shared database, plus the open project's MultiTerm/.ttb
        /// termbases (<paramref name="projectTermbases"/>, from
        /// MultiTermProjectDetector, resolved on the UI thread by the caller).
        /// </summary>
        public static List<TermbaseSource> Discover(
            List<MultiTermTermbaseConfig> projectTermbases)
        {
            var sources = new List<TermbaseSource>();

            // ─── Supervertaler termbases (shared SQLite DB) ───────────────
            try
            {
                var settings = TermLensSettings.Load();
                var dbPath = settings?.TermbasePath;
                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    var disabled = new HashSet<long>(settings.DisabledTermbaseIds ?? new List<long>());
                    using (var reader = new TermbaseReader(dbPath))
                    {
                        if (reader.Open())
                        {
                            foreach (var tb in reader.GetTermbases() ?? new List<TermbaseInfo>())
                            {
                                sources.Add(new TermbaseSource
                                {
                                    Name = tb.Name,
                                    Kind = "Supervertaler",
                                    Path = dbPath,
                                    SupervertalerId = tb.Id,
                                    Active = !disabled.Contains(tb.Id),
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("SuperSearch", "Termbase discovery (Supervertaler) failed: " + ex.Message);
            }

            // ─── The project's MultiTerm (.sdltb) / Trados (.ttb) termbases ──
            foreach (var cfg in projectTermbases ?? new List<MultiTermTermbaseConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.FilePath)) continue;
                var isTtb = cfg.FilePath.EndsWith(".ttb", StringComparison.OrdinalIgnoreCase);
                sources.Add(new TermbaseSource
                {
                    Name = cfg.TermbaseName ?? System.IO.Path.GetFileNameWithoutExtension(cfg.FilePath),
                    Kind = isTtb ? "TTB" : "MultiTerm",
                    Path = cfg.FilePath,
                    SourceIndexName = cfg.SourceIndexName,
                    TargetIndexName = cfg.TargetIndexName,
                    Active = cfg.TradosEnabled,
                });
            }

            return sources;
        }

        /// <summary>
        /// Searches the given termbases and returns matching term pairs as
        /// <see cref="SearchResult"/>s. <paramref name="scope"/> selects which
        /// side of the pair the query is applied to, mirroring file/TM search.
        /// </summary>
        public static List<SearchResult> Search(
            IEnumerable<TermbaseSource> sources,
            string query,
            SearchScope scope,
            bool caseSensitive,
            bool useRegex,
            bool wholeWord,
            Action<int, int> progress,
            CancellationToken ct)
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrEmpty(query)) return results;

            var list = (sources ?? Enumerable.Empty<TermbaseSource>()).ToList();
            int done = 0;

            foreach (var src in list)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var pairs = src.SupervertalerId >= 0
                        ? LoadSupervertalerTerms(src)
                        : LoadFileTermbaseTerms(src);

                    foreach (var pair in pairs)
                    {
                        ct.ThrowIfCancellationRequested();
                        var srcText = pair.Item1 ?? "";
                        var tgtText = pair.Item2 ?? "";

                        bool hit;
                        switch (scope)
                        {
                            case SearchScope.SourceOnly:
                                hit = XliffSearcher.QueryMatches(srcText, query, caseSensitive, useRegex, wholeWord);
                                break;
                            case SearchScope.TargetOnly:
                                hit = XliffSearcher.QueryMatches(tgtText, query, caseSensitive, useRegex, wholeWord);
                                break;
                            default:
                                hit = XliffSearcher.QueryMatches(srcText, query, caseSensitive, useRegex, wholeWord)
                                   || XliffSearcher.QueryMatches(tgtText, query, caseSensitive, useRegex, wholeWord);
                                break;
                        }
                        if (!hit) continue;

                        results.Add(new SearchResult
                        {
                            Kind = ResultKind.TermbaseEntry,
                            FileName = src.Name,
                            FilePath = src.Path,
                            SourceText = srcText,
                            TargetText = tgtText,
                            // The Status column carries provenance for a term:
                            // which kind of termbase it came from, and whether
                            // that termbase is currently switched off.
                            Status = src.Active ? src.Kind : src.Kind + " (inactive)",
                        });

                        if (results.Count % MaxHitsPerTermbase == 0) break;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    DiagnosticLog.Log("SuperSearch",
                        $"Termbase search failed for '{src.Name}': {ex.Message}");
                }

                done++;
                progress?.Invoke(done, list.Count);
            }

            return results;
        }

        /// <summary>Source→target pairs from one Supervertaler termbase.</summary>
        private static List<Tuple<string, string>> LoadSupervertalerTerms(TermbaseSource src)
        {
            var pairs = new List<Tuple<string, string>>();
            using (var reader = new TermbaseReader(src.Path))
            {
                if (!reader.Open()) return pairs;
                // LoadAllTerms keys on the (lower-cased) source term and holds
                // every entry; filter to this termbase so each row is
                // attributed to the termbase it actually came from.
                var index = reader.LoadAllTerms();
                foreach (var kv in index ?? new Dictionary<string, List<TermEntry>>())
                {
                    foreach (var e in kv.Value ?? new List<TermEntry>())
                    {
                        if (e == null || e.TermbaseId != src.SupervertalerId) continue;
                        pairs.Add(Tuple.Create(e.SourceTerm ?? "", e.TargetTerm ?? ""));
                    }
                }
            }
            return pairs;
        }

        /// <summary>Source→target pairs from a .sdltb or .ttb termbase.</summary>
        private static List<Tuple<string, string>> LoadFileTermbaseTerms(TermbaseSource src)
        {
            var pairs = new List<Tuple<string, string>>();
            using (var reader = TermbaseReaderFactory.Create(src.Path))
            {
                if (!reader.Open()) return pairs;
                var index = reader.LoadAllTerms(
                    src.SourceIndexName, src.TargetIndexName, -1, src.Name);
                foreach (var kv in index ?? new Dictionary<string, List<TermEntry>>())
                {
                    foreach (var e in kv.Value ?? new List<TermEntry>())
                    {
                        if (e == null) continue;
                        pairs.Add(Tuple.Create(e.SourceTerm ?? "", e.TargetTerm ?? ""));
                    }
                }
            }
            return pairs;
        }
    }
}
