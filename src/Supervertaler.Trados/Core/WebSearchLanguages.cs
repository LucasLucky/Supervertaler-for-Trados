using System;
using System.Collections.Generic;
using System.Globalization;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Converts a Trados locale ("nl-BE", "en-GB") into whichever language-code
    /// vocabulary a given web resource expects.
    ///
    /// <para>The standalone SuperLookup app hard-codes six languages and falls
    /// back to English for everything else. That is fine for a personal tool but
    /// wrong here: a Trados user working Estonian→Finnish would silently get
    /// English URLs. So the table below covers all EU official languages plus the
    /// major world ones, and anything outside it falls back to
    /// <see cref="CultureInfo"/> before finally degrading to the raw code.</para>
    ///
    /// <para>Note the ISO 639-2/B vs 639-3 split, which is the whole reason two
    /// three-letter columns exist. ProZ wants the bibliographic code (Dutch =
    /// "dut", German = "ger"), Juremy wants the terminological one (Dutch =
    /// "nld", German = "deu"). They differ for about twenty languages and are
    /// identical for the rest.</para>
    /// </summary>
    public static class WebSearchLanguages
    {
        private class Entry
        {
            public string Bibliographic;   // ISO 639-2/B
            public string Terminological;  // ISO 639-3 (== ISO 639-2/T)
            public string EnglishName;     // lower-case
            public Entry(string b, string t, string n) { Bibliographic = b; Terminological = t; EnglishName = n; }
        }

        // Keyed by ISO 639-1. Where the /B and /T columns are equal the language
        // has only one three-letter code; the twenty-odd rows where they differ
        // are the ones that actually matter for ProZ vs Juremy.
        private static readonly Dictionary<string, Entry> Table =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
        {
            { "ar", new Entry("ara", "ara", "arabic") },
            { "bg", new Entry("bul", "bul", "bulgarian") },
            { "bs", new Entry("bos", "bos", "bosnian") },
            { "ca", new Entry("cat", "cat", "catalan") },
            { "cs", new Entry("cze", "ces", "czech") },
            { "cy", new Entry("wel", "cym", "welsh") },
            { "da", new Entry("dan", "dan", "danish") },
            { "de", new Entry("ger", "deu", "german") },
            { "el", new Entry("gre", "ell", "greek") },
            { "en", new Entry("eng", "eng", "english") },
            { "es", new Entry("spa", "spa", "spanish") },
            { "et", new Entry("est", "est", "estonian") },
            { "eu", new Entry("baq", "eus", "basque") },
            { "fa", new Entry("per", "fas", "persian") },
            { "fi", new Entry("fin", "fin", "finnish") },
            { "fr", new Entry("fre", "fra", "french") },
            { "ga", new Entry("gle", "gle", "irish") },
            { "he", new Entry("heb", "heb", "hebrew") },
            { "hi", new Entry("hin", "hin", "hindi") },
            { "hr", new Entry("hrv", "hrv", "croatian") },
            { "hu", new Entry("hun", "hun", "hungarian") },
            { "hy", new Entry("arm", "hye", "armenian") },
            { "id", new Entry("ind", "ind", "indonesian") },
            { "is", new Entry("ice", "isl", "icelandic") },
            { "it", new Entry("ita", "ita", "italian") },
            { "ja", new Entry("jpn", "jpn", "japanese") },
            { "ka", new Entry("geo", "kat", "georgian") },
            { "ko", new Entry("kor", "kor", "korean") },
            { "lt", new Entry("lit", "lit", "lithuanian") },
            { "lv", new Entry("lav", "lav", "latvian") },
            { "mk", new Entry("mac", "mkd", "macedonian") },
            { "ms", new Entry("may", "msa", "malay") },
            { "mt", new Entry("mlt", "mlt", "maltese") },
            { "nb", new Entry("nob", "nob", "norwegian") },
            { "nl", new Entry("dut", "nld", "dutch") },
            { "nn", new Entry("nno", "nno", "norwegian") },
            { "no", new Entry("nor", "nor", "norwegian") },
            { "pl", new Entry("pol", "pol", "polish") },
            { "pt", new Entry("por", "por", "portuguese") },
            { "ro", new Entry("rum", "ron", "romanian") },
            { "ru", new Entry("rus", "rus", "russian") },
            { "sk", new Entry("slo", "slk", "slovak") },
            { "sl", new Entry("slv", "slv", "slovenian") },
            { "sq", new Entry("alb", "sqi", "albanian") },
            { "sr", new Entry("srp", "srp", "serbian") },
            { "sv", new Entry("swe", "swe", "swedish") },
            { "th", new Entry("tha", "tha", "thai") },
            { "tr", new Entry("tur", "tur", "turkish") },
            { "uk", new Entry("ukr", "ukr", "ukrainian") },
            { "vi", new Entry("vie", "vie", "vietnamese") },
            { "zh", new Entry("chi", "zho", "chinese") },
        };

        /// <summary>
        /// Reduces a Trados locale to its bare ISO 639-1 base: "nl-BE" → "nl",
        /// "en_US" → "en", "Dutch (Belgium)" → "" (display names are not codes).
        /// </summary>
        public static string BaseCode(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return string.Empty;
            var s = locale.Trim();
            var cut = s.IndexOfAny(new[] { '-', '_' });
            if (cut > 0) s = s.Substring(0, cut);
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// Renders <paramref name="locale"/> in the vocabulary <paramref name="format"/>
        /// asks for. Returns the bare two-letter code for
        /// <see cref="LanguageCodeFormat.None"/>, and never returns null.
        /// </summary>
        public static string Convert(string locale, LanguageCodeFormat format)
        {
            var code = BaseCode(locale);
            if (code.Length == 0) return string.Empty;

            Entry entry;
            var known = Table.TryGetValue(code, out entry);

            switch (format)
            {
                case LanguageCodeFormat.Iso3Bibliographic:
                    // Outside the table, CultureInfo's three-letter code is the
                    // terminological one — but /B and /T coincide for every
                    // language not listed above, so it is the right answer there.
                    return known ? entry.Bibliographic : ThreeLetterFromCulture(code);

                case LanguageCodeFormat.Iso639_3:
                    return known ? entry.Terminological : ThreeLetterFromCulture(code);

                case LanguageCodeFormat.FullLower:
                    return known ? entry.EnglishName : EnglishNameFromCulture(code);

                default:
                    return code;
            }
        }

        /// <summary>Convenience wrapper: the ISO 639-1 code, upper-cased.
        /// BabelNet's {sl_upper}/{tl_upper} placeholders want this.</summary>
        public static string UpperIso2(string locale)
        {
            return BaseCode(locale).ToUpperInvariant();
        }

        private static string ThreeLetterFromCulture(string code)
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo(code);
                var three = ci.ThreeLetterISOLanguageName;
                if (!string.IsNullOrEmpty(three) && three != "und") return three.ToLowerInvariant();
            }
            catch (CultureNotFoundException) { }
            // Better to send the site a two-letter code it may reject than a
            // three-letter code for the wrong language.
            return code;
        }

        private static string EnglishNameFromCulture(string code)
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo(code);
                var name = ci.EnglishName;
                if (!string.IsNullOrEmpty(name) && !name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    // EnglishName carries the region for specific cultures
                    // ("Dutch (Belgium)"); the neutral name is what URLs want.
                    var paren = name.IndexOf('(');
                    if (paren > 0) name = name.Substring(0, paren);
                    return name.Trim().ToLowerInvariant();
                }
            }
            catch (CultureNotFoundException) { }
            return code;
        }

        /// <summary>True if we can render this locale in every format – i.e. it
        /// is in the table or .NET knows it. Lets the UI warn about a resource
        /// that will produce a broken URL for the current project.</summary>
        public static bool IsKnown(string locale)
        {
            var code = BaseCode(locale);
            if (code.Length == 0) return false;
            if (Table.ContainsKey(code)) return true;
            try { CultureInfo.GetCultureInfo(code); return true; }
            catch (CultureNotFoundException) { return false; }
        }
    }
}
