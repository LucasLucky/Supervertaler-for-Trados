using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Data.Sqlite;

namespace Supervertaler.Trados.Core
{
    /// <summary>Interchange formats a Supervertaler termbase can be written out as.</summary>
    public enum TermbaseExportFormat
    {
        /// <summary>MultiTerm XML (MTF). What MultiTerm and Glossary Converter import.</summary>
        MultiTermXml,

        /// <summary>TBX-Basic (ISO 30042). Portable beyond Trados.</summary>
        Tbx
    }

    /// <summary>Outcome of one export run.</summary>
    public sealed class TermbaseExportResult
    {
        public int Concepts { get; set; }
        public int Terms { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Writes a Supervertaler termbase out in a format Trados can ingest, closing the
    /// half of the round trip that never existed: <c>.sdltb</c>/<c>.ttb</c> could be
    /// read in (<see cref="TermbaseImporter"/>) but nothing could be written back out
    /// (issue #60).
    ///
    /// WHY NOT WRITE <c>.sdltb</c> / <c>.ttb</c> DIRECTLY. A <c>.sdltb</c> is a JET/MDB
    /// database needing DDL through ACE/JET against an opaque internal schema — the same
    /// component family whose DLL-versioning behaviour drove this project onto
    /// Microsoft.Data.Sqlite in the first place. A <c>.ttb</c> is SQLite and therefore
    /// tempting, but our knowledge of it is derived rather than specified:
    /// <see cref="TtbReader"/> reads three of its seven tables, and authoring one Studio
    /// accepts would mean synthesising the field definitions, the <c>mtSystem</c>
    /// definition XML and the FTS5 index with no spec and no guarantee the schema is
    /// stable. A file Studio half-accepts is worse than a file it rejects.
    ///
    /// So both formats here are documented interchange formats that Trados already
    /// imports, at the cost of one conversion step the user performs themselves.
    ///
    /// The MultiTerm XML shape mirrors, in its verbose form, exactly what
    /// <see cref="MultiTermConceptXml"/> parses on the way in — conceptGrp /
    /// languageGrp / termGrp / descripGrp — so the two directions agree by construction.
    ///
    /// NOTE: neither output has been validated against a real MultiTerm or Glossary
    /// Converter import. Treat that as required before relying on it.
    /// </summary>
    public static class TermbaseExporter
    {
        /// <summary>
        /// One exported entry: a source/target pair with its synonyms and metadata.
        /// A Supervertaler row is bilingual, so one row becomes one concept.
        /// </summary>
        private sealed class Row
        {
            public long Id;
            public string Source = "";
            public string Target = "";
            public string Definition = "";
            public string Domain = "";
            public string Notes = "";
            public string Context = "";
            public string PartOfSpeech = "";
            public string Url = "";
            public string Client = "";
            public string Project = "";
            public bool Forbidden;
            public readonly List<string> SourceSynonyms = new List<string>();
            public readonly List<string> TargetSynonyms = new List<string>();
        }

        /// <summary>
        /// Writes every term in <paramref name="termbaseId"/> to <paramref name="outPath"/>.
        /// Locales are written as given; pass the termbase's own declared languages.
        /// </summary>
        public static TermbaseExportResult Export(
            string dbPath, long termbaseId, string outPath, TermbaseExportFormat format,
            string sourceLocale, string targetLocale, string termbaseName)
        {
            var result = new TermbaseExportResult();
            var rows = LoadRows(dbPath, termbaseId, result);

            var srcLoc = LanguageUtils.CanonicalLocale(sourceLocale) ?? sourceLocale ?? "";
            var tgtLoc = LanguageUtils.CanonicalLocale(targetLocale) ?? targetLocale ?? "";
            if (string.IsNullOrWhiteSpace(srcLoc) || string.IsNullOrWhiteSpace(tgtLoc))
                result.Warnings.Add(
                    "This termbase does not declare both languages, so the exported file may not " +
                    "import cleanly. Set them on Settings → Termbases first.");

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false),
                NewLineChars = "\r\n"
            };

            using (var writer = XmlWriter.Create(outPath, settings))
            {
                if (format == TermbaseExportFormat.MultiTermXml)
                    WriteMultiTermXml(writer, rows, srcLoc, tgtLoc, result);
                else
                    WriteTbx(writer, rows, srcLoc, tgtLoc, termbaseName, result);
            }

            return result;
        }

        /// <summary>Suggested file extension for a format.</summary>
        public static string ExtensionFor(TermbaseExportFormat format) =>
            format == TermbaseExportFormat.Tbx ? ".tbx" : ".xml";

        // ─── MultiTerm XML (MTF) ────────────────────────────────────

        private static void WriteMultiTermXml(
            XmlWriter w, List<Row> rows, string srcLoc, string tgtLoc, TermbaseExportResult result)
        {
            // MultiTerm stores locales upper-case ("EN-GB", "NL-NL") – see
            // ImportLanguage.Locale. Match that so a round trip is symmetric.
            var srcMt = (srcLoc ?? "").ToUpperInvariant();
            var tgtMt = (tgtLoc ?? "").ToUpperInvariant();

            w.WriteStartDocument();
            w.WriteStartElement("mtf");

            int conceptId = 0;
            foreach (var row in rows)
            {
                conceptId++;
                w.WriteStartElement("conceptGrp");
                w.WriteElementString("concept", conceptId.ToString());

                // Concept-level descriptive fields.
                WriteMtDescrip(w, "Subject", row.Domain);
                WriteMtDescrip(w, "Note", row.Notes);
                WriteMtDescrip(w, "Client", row.Client);
                WriteMtDescrip(w, "Project", row.Project);

                WriteMtLanguage(w, srcMt, row.Source, row.SourceSynonyms, row, isSourceSide: true);
                WriteMtLanguage(w, tgtMt, row.Target, row.TargetSynonyms, row, isSourceSide: false);

                w.WriteEndElement(); // conceptGrp
                result.Concepts++;
            }

            w.WriteEndElement(); // mtf
            w.WriteEndDocument();
        }

        private static void WriteMtLanguage(
            XmlWriter w, string locale, string mainTerm, List<string> synonyms, Row row, bool isSourceSide)
        {
            w.WriteStartElement("languageGrp");
            w.WriteStartElement("language");
            // 'type' is the INDEX NAME, not a second copy of the locale: MultiTerm
            // matches its language indexes by that name, and MultiTermConceptXml
            // documents the pair as <l lang="EN" type="English"/> on the way in.
            // Writing the locale here produced indexes called "EN"/"NL"; verified
            // against a MultiTerm XML file that imports cleanly, which uses
            // type="English" lang="EN" (issue #60).
            w.WriteAttributeString("type", MultiTermLanguageName(locale));
            w.WriteAttributeString("lang", locale);
            w.WriteEndElement();

            // Definition and context describe the concept but MultiTerm carries them
            // per language; put them on the source side, which is where a reader
            // looking for the authoritative description will go.
            if (isSourceSide)
            {
                WriteMtDescrip(w, "Definition", row.Definition);
                WriteMtDescrip(w, "Context", row.Context);
            }

            WriteMtTerm(w, mainTerm, row, isMain: true);
            foreach (var syn in synonyms)
                WriteMtTerm(w, syn, row, isMain: false);

            w.WriteEndElement(); // languageGrp
        }

        private static void WriteMtTerm(XmlWriter w, string text, Row row, bool isMain)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            w.WriteStartElement("termGrp");
            w.WriteElementString("term", text);
            if (isMain)
            {
                WriteMtDescrip(w, "Part of Speech", row.PartOfSpeech);
                WriteMtDescrip(w, "Source", row.Url);
                // "Status" is the field TermbaseImporter maps back onto the forbidden
                // flag, and "forbidden" is one of the values it recognises.
                if (row.Forbidden) WriteMtDescrip(w, "Status", "forbidden");
            }
            w.WriteEndElement(); // termGrp
        }

        /// <summary>
        /// The human-readable language name MultiTerm uses as an index name:
        /// "EN" → English, "NL" → Dutch, "EN-GB" → English (United Kingdom).
        /// Falls back to the locale itself for anything Windows doesn't know, which
        /// is no worse than what we wrote before and never throws.
        /// </summary>
        private static string MultiTermLanguageName(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return locale ?? "";
            try
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(locale.Trim());
                if (!string.IsNullOrWhiteSpace(culture?.EnglishName) &&
                    !culture.EnglishName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                    return culture.EnglishName;
            }
            catch { /* not a locale Windows recognises */ }
            return locale;
        }

        private static void WriteMtDescrip(XmlWriter w, string type, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            w.WriteStartElement("descripGrp");
            w.WriteStartElement("descrip");
            w.WriteAttributeString("type", type);
            w.WriteString(value);
            w.WriteEndElement();
            w.WriteEndElement();
        }

        // ─── TBX-Basic (ISO 30042) ──────────────────────────────────

        private static void WriteTbx(
            XmlWriter w, List<Row> rows, string srcLoc, string tgtLoc,
            string termbaseName, TermbaseExportResult result)
        {
            w.WriteStartDocument();
            w.WriteStartElement("martif");
            w.WriteAttributeString("type", "TBX-Basic");
            w.WriteAttributeString("xml", "lang", null, string.IsNullOrWhiteSpace(srcLoc) ? "en" : srcLoc);

            w.WriteStartElement("martifHeader");
            w.WriteStartElement("fileDesc");
            w.WriteStartElement("sourceDesc");
            w.WriteElementString("p",
                "Exported from Supervertaler termbase: " + (termbaseName ?? "(unnamed)"));
            w.WriteEndElement(); // sourceDesc
            w.WriteEndElement(); // fileDesc
            w.WriteEndElement(); // martifHeader

            w.WriteStartElement("text");
            w.WriteStartElement("body");

            int id = 0;
            foreach (var row in rows)
            {
                id++;
                w.WriteStartElement("termEntry");
                w.WriteAttributeString("id", "c" + id);

                WriteTbxDescrip(w, "subjectField", row.Domain);
                WriteTbxDescrip(w, "definition", row.Definition);
                if (!string.IsNullOrWhiteSpace(row.Notes)) w.WriteElementString("note", row.Notes);

                WriteTbxLangSet(w, srcLoc, row.Source, row.SourceSynonyms, row, withContext: true);
                WriteTbxLangSet(w, tgtLoc, row.Target, row.TargetSynonyms, row, withContext: false);

                w.WriteEndElement(); // termEntry
                result.Concepts++;
            }

            w.WriteEndElement(); // body
            w.WriteEndElement(); // text
            w.WriteEndElement(); // martif
            w.WriteEndDocument();
        }

        private static void WriteTbxLangSet(
            XmlWriter w, string locale, string mainTerm, List<string> synonyms, Row row, bool withContext)
        {
            w.WriteStartElement("langSet");
            w.WriteAttributeString("xml", "lang", null, string.IsNullOrWhiteSpace(locale) ? "und" : locale);

            WriteTbxTig(w, mainTerm, row, isMain: true, withContext: withContext);
            foreach (var syn in synonyms)
                WriteTbxTig(w, syn, row, isMain: false, withContext: false);

            w.WriteEndElement(); // langSet
        }

        private static void WriteTbxTig(XmlWriter w, string text, Row row, bool isMain, bool withContext)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            w.WriteStartElement("tig");
            w.WriteElementString("term", text);

            if (isMain && !string.IsNullOrWhiteSpace(row.PartOfSpeech))
            {
                w.WriteStartElement("termNote");
                w.WriteAttributeString("type", "partOfSpeech");
                w.WriteString(row.PartOfSpeech);
                w.WriteEndElement();
            }

            // TBX-Basic spells the "do not use" state as a picklist value, not a boolean.
            w.WriteStartElement("termNote");
            w.WriteAttributeString("type", "administrativeStatus");
            w.WriteString(row.Forbidden && isMain ? "deprecatedTerm-admn-sts" : "preferredTerm-admn-sts");
            w.WriteEndElement();

            if (isMain && withContext) WriteTbxDescrip(w, "context", row.Context);

            w.WriteEndElement(); // tig
        }

        private static void WriteTbxDescrip(XmlWriter w, string type, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            w.WriteStartElement("descrip");
            w.WriteAttributeString("type", type);
            w.WriteString(value);
            w.WriteEndElement();
        }

        // ─── Reading the termbase ───────────────────────────────────

        private static List<Row> LoadRows(string dbPath, long termbaseId, TermbaseExportResult result)
        {
            var rows = new List<Row>();
            var byId = new Dictionary<long, Row>();

            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();

                // Deliberately wider than ExportTsv's column list, which drops
                // definition, context, part of speech and url – MultiTerm and TBX
                // both have homes for those, so there is no reason to lose them here.
                using (var cmd = new SqliteCommand(@"
                    SELECT id, source_term, target_term,
                           COALESCE(definition, ''), COALESCE(domain, ''), COALESCE(notes, ''),
                           COALESCE(context, ''), COALESCE(part_of_speech, ''), COALESCE(url, ''),
                           COALESCE(client, ''), COALESCE(project, ''), COALESCE(forbidden, 0)
                    FROM termbase_terms
                    WHERE CAST(termbase_id AS INTEGER) = @tbId
                    ORDER BY source_term ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@tbId", termbaseId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var row = new Row
                            {
                                Id = r.GetInt64(0),
                                Source = r.IsDBNull(1) ? "" : r.GetString(1),
                                Target = r.IsDBNull(2) ? "" : r.GetString(2),
                                Definition = r.GetString(3),
                                Domain = r.GetString(4),
                                Notes = r.GetString(5),
                                Context = r.GetString(6),
                                PartOfSpeech = r.GetString(7),
                                Url = r.GetString(8),
                                Client = r.GetString(9),
                                Project = r.GetString(10),
                                Forbidden = r.GetInt64(11) != 0
                            };
                            if (string.IsNullOrWhiteSpace(row.Source) || string.IsNullOrWhiteSpace(row.Target))
                                continue;
                            rows.Add(row);
                            byId[row.Id] = row;
                            result.Terms++;
                        }
                    }
                }

                using (var cmd = new SqliteCommand(@"
                    SELECT s.term_id, s.synonym_text, s.language
                    FROM termbase_synonyms s
                    INNER JOIN termbase_terms t ON s.term_id = t.id
                    WHERE CAST(t.termbase_id AS INTEGER) = @tbId
                    ORDER BY s.term_id, s.language, s.display_order ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@tbId", termbaseId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Row row;
                            if (!byId.TryGetValue(r.GetInt64(0), out row)) continue;
                            var text = r.IsDBNull(1) ? "" : r.GetString(1);
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            var lang = r.IsDBNull(2) ? "" : r.GetString(2);
                            if (lang == "source") row.SourceSynonyms.Add(text);
                            else if (lang == "target") row.TargetSynonyms.Add(text);
                        }
                    }
                }
            }

            return rows;
        }
    }
}
