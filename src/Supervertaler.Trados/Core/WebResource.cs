using System;
using System.Runtime.Serialization;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Which language-code vocabulary a web resource expects in its URL.
    /// Sites are maddeningly inconsistent: IATE wants "nl", ProZ wants the
    /// bibliographic "dut", Juremy wants the terminological "nld", and Linguee
    /// wants "dutch". <see cref="WebSearchLanguages"/> does the conversion.
    /// </summary>
    public enum LanguageCodeFormat
    {
        /// <summary>No language codes in the URL – the template only uses {query}.</summary>
        None = 0,
        /// <summary>ISO 639-1 two-letter: nl, en, de.</summary>
        Iso2,
        /// <summary>ISO 639-2/B (bibliographic) three-letter: dut, eng, ger. Used by ProZ.</summary>
        Iso3Bibliographic,
        /// <summary>ISO 639-3 / 639-2/T (terminological): nld, eng, deu. Used by Juremy.</summary>
        Iso639_3,
        /// <summary>Lower-cased English language name: dutch, english, german. Used by Linguee, Reverso.</summary>
        FullLower,
    }

    /// <summary>
    /// One searchable web resource – a name, an icon, and a URL template with
    /// placeholders that <see cref="WebSearchUrlBuilder"/> fills in.
    ///
    /// <para>The on-disk shape is deliberately identical to the standalone
    /// SuperLookup app's <c>superlookup-searches.json</c> so a resource list can
    /// be exported from one product and imported into the other unchanged. That
    /// is why <see cref="Fmt"/> is a raw string rather than the enum: the wire
    /// format is the contract, and <see cref="Format"/> is the typed view of
    /// it.</para>
    /// </summary>
    [DataContract]
    public class WebResource
    {
        /// <summary>Stable identifier. Built-ins use fixed ids ("iate", "linguee");
        /// user-added ones get a slug of their name via <see cref="MakeId"/>.</summary>
        [DataMember(Name = "id")]
        public string Id { get; set; }

        /// <summary>Display name, shown on the tab and in the settings list.</summary>
        [DataMember(Name = "name")]
        public string Name { get; set; }

        /// <summary>Single emoji shown next to the name. Cosmetic; may be empty.</summary>
        [DataMember(Name = "icon")]
        public string Icon { get; set; }

        /// <summary>
        /// URL template. Recognised placeholders: <c>{query}</c>, <c>{sl}</c>,
        /// <c>{tl}</c>, <c>{sl_full}</c>, <c>{tl_full}</c>, <c>{sl_upper}</c>,
        /// <c>{tl_upper}</c>.
        /// </summary>
        [DataMember(Name = "url")]
        public string Url { get; set; }

        /// <summary>
        /// Wire form of <see cref="Format"/>: null, "iso2", "iso3", "iso639_3"
        /// or "full_lower". Kept as a string so unknown future values survive a
        /// load/save round-trip instead of being silently flattened to None.
        /// </summary>
        [DataMember(Name = "fmt")]
        public string Fmt { get; set; }

        /// <summary>Whether this resource participates in searches.</summary>
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Marks the MediaWiki-family resources ("wikipedia", "wiktionary",
        /// "wikidata"). Carried for schema compatibility with the standalone app,
        /// which renders these through the MediaWiki search API rather than as a
        /// page. Unused here – we render them as ordinary pages.
        /// </summary>
        [DataMember(Name = "wiki", EmitDefaultValue = false)]
        public string Wiki { get; set; }

        /// <summary>Typed view of <see cref="Fmt"/>. Unrecognised values read as
        /// <see cref="LanguageCodeFormat.None"/>, which degrades to "no language
        /// codes" rather than throwing on a hand-edited config.</summary>
        public LanguageCodeFormat Format
        {
            get { return ParseFormat(Fmt); }
            set { Fmt = FormatToString(value); }
        }

        public static LanguageCodeFormat ParseFormat(string fmt)
        {
            if (string.IsNullOrWhiteSpace(fmt)) return LanguageCodeFormat.None;
            switch (fmt.Trim().ToLowerInvariant())
            {
                case "iso2": return LanguageCodeFormat.Iso2;
                case "iso3": return LanguageCodeFormat.Iso3Bibliographic;
                case "iso639_3": return LanguageCodeFormat.Iso639_3;
                case "full_lower": return LanguageCodeFormat.FullLower;
                default: return LanguageCodeFormat.None;
            }
        }

        public static string FormatToString(LanguageCodeFormat format)
        {
            switch (format)
            {
                case LanguageCodeFormat.Iso2: return "iso2";
                case LanguageCodeFormat.Iso3Bibliographic: return "iso3";
                case LanguageCodeFormat.Iso639_3: return "iso639_3";
                case LanguageCodeFormat.FullLower: return "full_lower";
                default: return null;
            }
        }

        /// <summary>
        /// Slugifies a display name into an id: lower-case, runs of non-alphanumerics
        /// collapsed to a single hyphen, trimmed. Mirrors the standalone app's
        /// <c>_slug()</c> so the same custom resource gets the same id in both.
        /// </summary>
        public static string MakeId(string name)
        {
            var sb = new System.Text.StringBuilder();
            bool lastWasSeparator = false;
            foreach (var ch in (name ?? string.Empty).ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator && sb.Length > 0)
                {
                    sb.Append('-');
                    lastWasSeparator = true;
                }
            }
            var slug = sb.ToString().Trim('-');
            return slug.Length > 0 ? slug : "search";
        }

        public WebResource Clone()
        {
            return new WebResource
            {
                Id = Id,
                Name = Name,
                Icon = Icon,
                Url = Url,
                Fmt = Fmt,
                Enabled = Enabled,
                Wiki = Wiki,
            };
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Icon) ? (Name ?? Id) : Icon + " " + (Name ?? Id);
        }
    }
}
