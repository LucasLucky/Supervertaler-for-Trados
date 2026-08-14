using System;
using System.Collections.Generic;
using System.Linq;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Turns a <see cref="WebResource"/> template plus a query and a language
    /// pair into the URL to open. Ported from the standalone SuperLookup app's
    /// <c>build_url()</c> so all three products produce byte-identical URLs for
    /// the same search.
    /// </summary>
    public static class WebSearchUrlBuilder
    {
        /// <summary>Linguee's id, special-cased in <see cref="Build"/>.</summary>
        private const string LingueeId = "linguee";

        /// <summary>
        /// Builds the URL for one resource.
        /// </summary>
        /// <param name="resource">The resource whose <see cref="WebResource.Url"/> is the template.</param>
        /// <param name="query">The search text, unescaped.</param>
        /// <param name="sourceLocale">Trados source locale, e.g. "en-GB".</param>
        /// <param name="targetLocale">Trados target locale, e.g. "nl-NL".</param>
        /// <returns>The resolved URL, or null if the resource has no template.</returns>
        public static string Build(WebResource resource, string query, string sourceLocale, string targetLocale)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.Url)) return null;

            var q = Uri.EscapeDataString(query ?? string.Empty);
            var format = resource.Format;

            // {sl}/{tl} stay empty when the resource declares no language format –
            // matching the standalone app, where a template that names {sl} but
            // sets fmt=null is treated as a template that does not really use it.
            var sl = format == LanguageCodeFormat.None
                ? string.Empty
                : WebSearchLanguages.Convert(sourceLocale, format);
            var tl = format == LanguageCodeFormat.None
                ? string.Empty
                : WebSearchLanguages.Convert(targetLocale, format);

            var slFull = WebSearchLanguages.Convert(sourceLocale, LanguageCodeFormat.FullLower);
            var tlFull = WebSearchLanguages.Convert(targetLocale, LanguageCodeFormat.FullLower);
            var slUpper = WebSearchLanguages.UpperIso2(sourceLocale);
            var tlUpper = WebSearchLanguages.UpperIso2(targetLocale);

            if (string.Equals(resource.Id, LingueeId, StringComparison.OrdinalIgnoreCase))
                OrderForLinguee(ref slFull, ref tlFull);

            var url = resource.Url;
            url = url.Replace("{query}", q);
            url = url.Replace("{sl_full}", slFull);
            url = url.Replace("{tl_full}", tlFull);
            url = url.Replace("{sl_upper}", slUpper);
            url = url.Replace("{tl_upper}", tlUpper);
            // {sl}/{tl} last: replacing them first would corrupt the longer
            // {sl_full} / {sl_upper} tokens, which share their prefix.
            url = url.Replace("{sl}", sl);
            url = url.Replace("{tl}", tl);
            return url;
        }

        /// <summary>
        /// Builds URLs for every enabled resource, in list order. Resources whose
        /// template fails to resolve are skipped rather than yielding a broken tab.
        /// </summary>
        public static List<WebSearchTarget> BuildAll(
            IEnumerable<WebResource> resources, string query, string sourceLocale, string targetLocale)
        {
            var targets = new List<WebSearchTarget>();
            if (resources == null) return targets;

            foreach (var resource in resources.Where(r => r != null && r.Enabled))
            {
                var url = Build(resource, query, sourceLocale, targetLocale);
                if (string.IsNullOrWhiteSpace(url)) continue;
                targets.Add(new WebSearchTarget { Resource = resource, Url = url });
            }
            return targets;
        }

        /// <summary>
        /// Linguee only serves language pairs in a fixed order: English first if
        /// the pair involves English, otherwise alphabetical. Asking for
        /// "dutch-english" 404s where "english-dutch" works.
        /// </summary>
        private static void OrderForLinguee(ref string slFull, ref string tlFull)
        {
            const string english = "english";
            if (slFull == english || tlFull == english)
            {
                if (slFull != english) Swap(ref slFull, ref tlFull);
            }
            else if (string.CompareOrdinal(slFull, tlFull) > 0)
            {
                Swap(ref slFull, ref tlFull);
            }
        }

        private static void Swap(ref string a, ref string b)
        {
            var t = a; a = b; b = t;
        }
    }

    /// <summary>A resource paired with the URL a particular search resolved to.</summary>
    public class WebSearchTarget
    {
        public WebResource Resource { get; set; }
        public string Url { get; set; }
    }
}
