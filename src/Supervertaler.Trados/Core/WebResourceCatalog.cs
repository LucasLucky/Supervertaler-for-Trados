using System;
using System.Collections.Generic;
using System.Linq;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The built-in web resources SuperSearch ships with, and the logic that
    /// reconciles a user's saved list against them.
    ///
    /// <para>The set is shared with the standalone SuperLookup app; ids and URL
    /// templates match so a list exported there imports here unchanged.</para>
    /// </summary>
    public static class WebResourceCatalog
    {
        /// <summary>
        /// Bump when a built-in's URL, name, icon or format changes. On a bump,
        /// <see cref="Merge"/> refreshes every built-in's definition from the
        /// current defaults while preserving the user's on/off choices and
        /// ordering. Without this, a site that changes its URL scheme stays
        /// broken forever for existing users.
        /// </summary>
        public const int DefaultsRevision = 1;

        /// <summary>
        /// Built-ins retired since launch. Ids listed here are dropped from a
        /// user's saved list on merge instead of lingering as dead tabs.
        /// </summary>
        private static readonly HashSet<string> RetiredIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // chemindustry.com changed hands: the domain now serves an
                // unrelated Chinese PHP application that returns a stack trace
                // for every request. Not broken — gone. Verified 2026-08-12.
                "chemindustry",
            };

        /// <summary>
        /// Enabled out of the box. Deliberately small: the standalone app enables
        /// everything, which is fine when tabs are the whole UI, but a Trados user
        /// pressing Alt+S does not want forty tabs appearing next to their
        /// results. Everything else ships disabled and is one checkbox away.
        /// </summary>
        private static readonly HashSet<string> DefaultEnabledIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "superterm", "iate", "linguee", "reverso", "proz" };

        // Order matters: it is the tab order and the settings-list order, and it
        // is kept byte-identical to the standalone app's DEFAULT_RESOURCES so a
        // user moving between the two products sees the same list in the same
        // sequence. Verified against an exported superlookup-searches.json.
        private static readonly WebResource[] BuiltIns =
        {
            // ── Core set ────────────────────────────────────────────────────
            R("superterm", "📚", "Beijerterm", "https://beijerterm.com/?q={query}&from={sl}&to={tl}", "iso2"),
            R("iate", "🇪🇺", "IATE", "https://iate.europa.eu/search/byUrl?term={query}&sl={sl}&tl={tl}", "iso2"),
            R("linguee", "📗", "Linguee", "https://www.linguee.com/{sl_full}-{tl_full}/search?source=auto&query={query}", "full_lower"),
            R("proz", "💬", "ProZ.com", "https://www.proz.com/search/?term={query}&from={sl}&to={tl}&results_per_page=25&es=1", "iso3"),
            // ProZ's rebuilt term search, under /next/. Added alongside the
            // classic one rather than replacing it: both work today, they present
            // the same KudoZ and glossary data differently, and which one a
            // translator prefers is taste. Ships disabled so nobody's working
            // setup changes under them — tick it in the Web picker to try it.
            // Same iso3 (ISO 639-2/B) codes as the classic search: dut, eng.
            R("proz_next", "💬", "ProZ.com (next)", "https://www.proz.com/next/search-fast?q={query}&sl={sl}&tl={tl}", "iso3"),
            R("reverso", "🔄", "Reverso", "https://context.reverso.net/translation/{sl_full}-{tl_full}/{query}", "full_lower"),
            R("juremy", "⚖️", "Juremy", "https://juremy.com/search?src={sl}&dst={tl}&q={query}&opts=ia&tool=iws", "iso639_3"),
            R("babelnet", "🌐", "BabelNet", "https://babelnet.org/search?word={query}&lang={sl_upper}&transLang={tl_upper}", "iso2"),
            R("wikipedia", "📖", "Wikipedia", "https://{sl}.wikipedia.org/w/index.php?search={query}&title=Special:Search&fulltext=1", "iso2", "wikipedia"),
            R("wiktionary", "📓", "Wiktionary", "https://{sl}.wiktionary.org/w/index.php?search={query}&title=Special:Search&fulltext=1", "iso2", "wiktionary"),
            R("wikidata", "🔗", "Wikidata", "https://www.wikidata.org/w/index.php?search={query}&title=Special:Search&fulltext=1", null, "wikidata"),
            R("acronymfinder", "🔤", "AcronymFinder", "https://www.acronymfinder.com/~/search/af.aspx?string=exact&Acronym={query}", null),
            R("opus", "🗂️", "OPUS Corpus", "https://opus.nlpl.eu/bin/opuscqp.pl?corpus=DGT;lang={sl};cqp={query};align={tl}", "iso2"),
            R("google", "🔍", "Google", "https://www.google.com/search?q={query}", null),
            R("google_patents", "📜", "Google Patents", "https://patents.google.com/?q=\"{query}\"", null),
            R("github_code", "💻", "GitHub Code", "https://github.com/search?q={query}&type=code", null),

            // ── Bilingual dictionaries ──────────────────────────────────────
            R("glosbe", "📘", "Glosbe", "https://glosbe.com/{sl}/{tl}/{query}", "iso2"),
            R("babla", "🗣️", "bab.la", "https://en.bab.la/dictionary/{sl_full}-{tl_full}/{query}", null),
            R("wordreference", "📕", "WordReference", "https://www.wordreference.com/{sl}{tl}/{query}", "iso2"),
            R("keybot", "🔑", "Keybot", "https://www.keybot.com/{sl_full}-{tl_full}/{query}.htm", null),
            R("sensagent", "📐", "Sensagent", "https://dictionary.sensagent.com/{query}/{sl}-{tl}/", "iso2"),
            R("bing_translator", "🌉", "Bing Translator", "https://www.bing.com/translator/?from={sl}&to={tl}&text={query}", "iso2"),
            // Endpoint moved: /2lingual-google/google-search -> /2lingual-google-search,
            // and qt=1 is now required or the query is ignored. Verified 2026-08-12.
            R("twolingual", "🔀", "2lingual", "https://www.2lingual.com/2lingual-google-search?qt=1&q={query}&lr1=lang_{sl}&lr2=lang_{tl}", "iso2"),

            // ── Dutch ───────────────────────────────────────────────────────
            R("woordenlijst", "🇳🇱", "Woordenlijst (Taalunie)", "https://woordenlijst.org/#/?q={query}", null),
            R("synoniemen", "🔁", "Synoniemen.net", "https://synoniemen.net/index.php?zoekterm={query}", null),
            // /begrippen/{x} is the A-Z index, not a term page, so the old
            // template 404'd on every query. The real search is a GET on the
            // home page, and LETTER is mandatory — without it the site silently
            // returns the front page instead of results. Verified 2026-08-12.
            R("dfbonline", "💶", "Financiële Begrippenlijst", "https://www.dfbonline.nl/?invoer={query}&LETTER=123", null),

            // ── EU / terminology ────────────────────────────────────────────
            R("eurlex", "📜", "EUR-Lex", "https://eur-lex.europa.eu/search.html?text={query}&scope=EURLEX&type=quick", null),
            R("eurotermbank", "🏛️", "EuroTermBank", "https://www.eurotermbank.com/search/{query}", null),
            R("gemet", "🌍", "GEMET Thesaurus", "https://www.eionet.europa.eu/gemet/en/search/?query={query}", null),

            // ── English monolingual / writing ───────────────────────────────
            R("collins", "📙", "Collins", "https://www.collinsdictionary.com/dictionary/english/{query}", null),
            R("thefreedictionary", "📔", "TheFreeDictionary", "https://www.thefreedictionary.com/{query}", null),
            R("merriam_thesaurus", "📗", "Merriam-Webster Thesaurus", "https://www.merriam-webster.com/thesaurus/{query}", null),
            R("thesaurus_com", "🗂️", "Thesaurus.com", "https://www.thesaurus.com/browse/{query}", null),
            // Now HTTPS-only, and the parameter was renamed word -> query.
            // The on-page form posts with a jsessionid, but the plain GET still
            // works and is what a URL template needs. Verified 2026-08-12.
            R("freecollocation", "🔗", "Oxford Collocations", "https://www.freecollocation.com/search?query={query}", null),
            R("skell", "📊", "SkELL Concordance", "https://skell.sketchengine.eu/#result?lang=en&query={query}", null),
            R("etymonline", "🏺", "Etymonline", "https://www.etymonline.com/search?q={query}", null),
            R("wordnik", "🔤", "Wordnik", "https://www.wordnik.com/words/{query}", null),
            R("visuwords", "🕸️", "Visuwords", "https://visuwords.com/{query}", null),
            R("howmanysyllables", "🎵", "Syllable Dictionary", "https://www.howmanysyllables.com/words/{query}", null),

            // ── Medical / technical ─────────────────────────────────────────
            R("ema", "💊", "EMA Medicines", "https://www.ema.europa.eu/en/medicines?search_api_views_fulltext={query}", null),
            R("emc", "💉", "EMC Medicines", "https://www.medicines.org.uk/emc/search?q={query}", null),
            // ChemIndustry removed here — see RetiredIds.

            // ── Media ───────────────────────────────────────────────────────
            R("imdb", "🎬", "IMDb", "https://www.imdb.com/find?q={query}&s=tt", null),
        };

        private static WebResource R(string id, string icon, string name, string url, string fmt, string wiki = null)
        {
            return new WebResource
            {
                Id = id,
                Icon = icon,
                Name = name,
                Url = url,
                Fmt = fmt,
                Wiki = wiki,
                Enabled = DefaultEnabledIds.Contains(id),
            };
        }

        /// <summary>A fresh copy of the built-in set, with default enabled states.</summary>
        public static List<WebResource> Defaults()
        {
            return BuiltIns.Select(r => r.Clone()).ToList();
        }

        /// <summary>
        /// Reconciles the user's saved resource list with the built-ins.
        ///
        /// <list type="bullet">
        /// <item>Retired built-ins are dropped.</item>
        /// <item>On a <see cref="DefaultsRevision"/> bump, each built-in's
        ///       definition is refreshed from the current defaults — but the
        ///       user's <see cref="WebResource.Enabled"/> choice and their
        ///       ordering are preserved.</item>
        /// <item>Custom (non-built-in) resources are left completely alone.</item>
        /// <item>Newly shipped built-ins are appended, disabled unless they are
        ///       in the default-enabled set.</item>
        /// </list>
        ///
        /// <para>Mirrors the standalone app's <c>merge_resources()</c>.</para>
        /// </summary>
        /// <param name="saved">The user's stored list; null or empty yields <see cref="Defaults"/>.</param>
        /// <param name="savedRevision">The <see cref="DefaultsRevision"/> in force when <paramref name="saved"/> was written.</param>
        public static List<WebResource> Merge(IList<WebResource> saved, int savedRevision)
        {
            if (saved == null || saved.Count == 0) return Defaults();

            var defaults = Defaults().ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
            var refresh = savedRevision != DefaultsRevision;

            var merged = new List<WebResource>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var stored in saved)
            {
                if (stored == null || string.IsNullOrWhiteSpace(stored.Id)) continue;
                if (RetiredIds.Contains(stored.Id)) continue;
                seen.Add(stored.Id);

                WebResource builtIn;
                if (refresh && defaults.TryGetValue(stored.Id, out builtIn))
                {
                    var refreshed = builtIn.Clone();
                    refreshed.Enabled = stored.Enabled;   // the user's choice wins
                    merged.Add(refreshed);
                }
                else
                {
                    merged.Add(stored);
                }
            }

            foreach (var builtIn in Defaults())
            {
                if (!seen.Contains(builtIn.Id)) merged.Add(builtIn);
            }

            return merged;
        }
    }
}
