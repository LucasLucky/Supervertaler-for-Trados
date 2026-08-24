using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using V = DocumentFormat.OpenXml.Vml;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// An image pulled out of a DOCX, together with the thing that ties it to
    /// the text.
    ///
    /// <para><see cref="Anchor"/> is the point of this class. A folder of image
    /// files has already lost it: you cannot recover "page 3, between 'Mount the
    /// bracket' and 'Tighten the screws'" from <c>IMG_2094.png</c>. A label
    /// exists only when the document names its figures; the anchor exists
    /// always, which is what lets this feature work on documents whose images
    /// have no names at all. See <c>docs/design/reference-images.md</c>.</para>
    /// </summary>
    public class ExtractedImage
    {
        /// <summary>1-based position in document order.</summary>
        public int Ordinal { get; set; }

        /// <summary>Index of the paragraph carrying the image, for ordering
        /// and for tying back to the document.</summary>
        public int ParagraphIndex { get; set; }

        /// <summary>"FIG. 3", "Table 2", "Plate IV" - or null when the document
        /// does not name it. Null is the normal case outside patents and papers.</summary>
        public string Label { get; set; }

        /// <summary>Text of the paragraph that reads as this image's caption,
        /// or null. Distinct from <see cref="Label"/>: a caption may be a whole
        /// sentence, and is itself usually a segment being translated.</summary>
        public string Caption { get; set; }

        /// <summary>The source text the image sits among. Always populated when
        /// the document has any text near the image.</summary>
        public string Anchor { get; set; }

        /// <summary>The image part's name inside the package, e.g.
        /// "/word/media/image3.png". Stable within one file.</summary>
        public string PartName { get; set; }

        public string ContentType { get; set; }

        /// <summary>Extension implied by the content type, including the dot.</summary>
        public string Extension { get; set; }

        public long SizeBytes { get; set; }

        /// <summary>The bytes, only when extraction was asked for them.
        /// Off by default: Studio 2024 is a 32-bit process and a patent's
        /// drawings can be tens of megabytes.</summary>
        public byte[] Data { get; set; }

        /// <summary>How <see cref="Label"/> was arrived at - useful when a
        /// label looks wrong and someone has to work out why.</summary>
        public string LabelSource { get; set; }

        public override string ToString()
        {
            return "#" + Ordinal + " " + (Label ?? "(unlabelled)")
                 + " [" + PartName + "]";
        }
    }

    /// <summary>
    /// Reads a DOCX and returns each image with its label, caption and anchor.
    ///
    /// <para>A port of Supervertaler Workbench's <c>modules/image_extractor.py</c>,
    /// deliberately rather than a reimplementation: that module's detection
    /// rules carry a bug history worth inheriting (see <see cref="DetectLabel"/>),
    /// and a user with one folder of drawings should not get different answers
    /// from the two products.</para>
    ///
    /// <para>No AI, no network, no vision pass. This is step 2 of issue #69 and
    /// stands on its own.</para>
    /// </summary>
    public static class DocxImageExtractor
    {
        // Word's built-in Caption style, plus the synonyms real documents use.
        private static readonly HashSet<string> CaptionStyleIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "caption", "figurecaption", "figurelegend",
                "tablecaption", "tablelegend",
            };

        // Ordered by specificity: patent FIG. first, then academic Figure, then
        // the rest. Each captures the whole label verbatim so the document's own
        // spelling and capitalisation survive.
        private static readonly Regex[] LabelPatterns =
        {
            // Patent style: FIG. 7, FIGS. 6, FIG.7, FIG 7
            new Regex(@"\b(FIGS?\.?\s*\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Academic / generic
            new Regex(@"\b(Figures?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Fig\.?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Tables?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Diagrams?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Charts?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Photo(?:graph)?s?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Schemes?\s+\d+[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Plates take Roman numerals, per older scientific practice.
            new Regex(@"\b(Plates?\s+(?:\d+|[IVXLCM]+))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(Exhibits?\s+[A-Za-z]?\d*[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        /// <summary>How many paragraphs either side of the image make up the anchor.</summary>
        private const int AnchorWindow = 2;

        /// <summary>
        /// Every image in <paramref name="docxPath"/>, in document order.
        /// Returns an empty list rather than throwing when the file cannot be
        /// read as a DOCX - callers are UI paths and a malformed file is not
        /// exceptional.
        /// </summary>
        /// <param name="includeImageData">Load the bytes as well. Off by
        /// default: Studio 2024 is 32-bit and drawings can be large.</param>
        public static List<ExtractedImage> Extract(string docxPath, bool includeImageData = false)
        {
            var results = new List<ExtractedImage>();
            if (string.IsNullOrWhiteSpace(docxPath) || !File.Exists(docxPath))
                return results;

            try
            {
                using (var doc = WordprocessingDocument.Open(docxPath, false))
                {
                    var main = doc.MainDocumentPart;
                    var body = main?.Document?.Body;
                    if (body == null) return results;

                    var paragraphs = body.Descendants<Paragraph>().ToList();

                    // Precompute per paragraph: its own text, its own image
                    // relationship ids, and whether it is Caption-styled.
                    var texts = new string[paragraphs.Count];
                    var rids = new List<string>[paragraphs.Count];
                    var isCaption = new bool[paragraphs.Count];

                    for (int i = 0; i < paragraphs.Count; i++)
                    {
                        texts[i] = DirectText(paragraphs[i]);
                        rids[i] = DirectImageRelationshipIds(paragraphs[i]);
                        isCaption[i] = IsCaptionStyled(paragraphs[i]);
                    }

                    int ordinal = 0;
                    for (int i = 0; i < paragraphs.Count; i++)
                    {
                        if (rids[i].Count == 0) continue;

                        string labelSource;
                        var label = DetectLabel(texts, rids, isCaption, i, out labelSource);
                        var caption = DetectCaption(texts, isCaption, i);
                        var anchor = CollectAnchor(texts, i);

                        foreach (var rid in rids[i])
                        {
                            var part = ResolveImagePart(main, rid);
                            if (part == null) continue;

                            ordinal++;
                            var img = new ExtractedImage
                            {
                                Ordinal = ordinal,
                                ParagraphIndex = i,
                                Label = label,
                                LabelSource = labelSource,
                                Caption = caption,
                                Anchor = anchor,
                                PartName = part.Uri == null ? "" : part.Uri.ToString(),
                                ContentType = part.ContentType,
                                Extension = ExtensionFor(part.ContentType),
                            };

                            try
                            {
                                using (var s = part.GetStream(FileMode.Open, FileAccess.Read))
                                {
                                    img.SizeBytes = s.Length;
                                    if (includeImageData)
                                    {
                                        using (var ms = new MemoryStream())
                                        {
                                            s.CopyTo(ms);
                                            img.Data = ms.ToArray();
                                        }
                                    }
                                }
                            }
                            catch { /* a part we cannot read still has its anchor */ }

                            results.Add(img);
                        }
                    }
                }
            }
            catch
            {
                // Not a readable DOCX. An empty list says "no images found",
                // which is what the caller can act on.
                return results;
            }

            return results;
        }

        // ── Label detection ──────────────────────────────────────────────

        /// <summary>
        /// Find a label for the image in paragraph <paramref name="idx"/>.
        ///
        /// <para><b>Position dominates style</b>, and the previous paragraph is
        /// only a candidate when the paragraph before THAT has no image of its
        /// own. Patent layouts run [image][caption][image][caption]..., so the
        /// paragraph immediately before an image is usually the PREVIOUS image's
        /// caption. Workbench learned this the hard way (its v1.10.191): an
        /// earlier version promoted any Caption-styled paragraph above any
        /// pattern match, and so labelled
        /// <code>
        ///   p[N-1]  "FIG. 16"   [Caption-styled, belongs to image N-2]
        ///   p[N]    &lt;image of FIG. 17&gt;
        ///   p[N+1]  "FIG. 17"   [not Caption-styled]
        /// </code>
        /// as FIG. 16. Do not "simplify" this back.</para>
        /// </summary>
        private static string DetectLabel(
            string[] texts, List<string>[] rids, bool[] isCaption, int idx,
            out string labelSource)
        {
            labelSource = null;

            var order = new List<int> { idx };
            if (idx + 1 < texts.Length) order.Add(idx + 1);

            // Previous paragraph, but only if it is not the previous image's caption.
            var prevIsOurs = idx > 0 && (idx < 2 || rids[idx - 2].Count == 0);
            if (prevIsOurs) order.Add(idx - 1);

            foreach (var i in order)
            {
                var text = texts[i];
                if (string.IsNullOrEmpty(text)) continue;

                var matched = MatchLabel(text);
                if (matched != null)
                {
                    labelSource = "pattern@" + Offset(i, idx);
                    return matched;
                }

                if (isCaption[i])
                {
                    // A Caption-styled paragraph with no recognisable label
                    // pattern: take its leading clause as the label.
                    var leading = Regex.Split(text, @"[.,:;\n]")[0].Trim();
                    if (leading.Length > 0)
                    {
                        labelSource = "caption-style@" + Offset(i, idx);
                        return leading.Length > 80 ? leading.Substring(0, 80).Trim() : leading;
                    }
                }
            }

            return null;
        }

        private static string Offset(int i, int idx)
        {
            if (i == idx) return "same";
            return i > idx ? "next" : "prev";
        }

        /// <summary>First label pattern that matches, captured verbatim.</summary>
        private static string MatchLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var pat in LabelPatterns)
            {
                var m = pat.Match(text);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        /// <summary>
        /// The caption paragraph's full text, as opposed to the short label.
        /// Same position order, but only Caption-styled paragraphs count -
        /// body text that merely mentions "figure 3" is a citation, not a caption.
        /// </summary>
        private static string DetectCaption(string[] texts, bool[] isCaption, int idx)
        {
            if (isCaption[idx] && !string.IsNullOrWhiteSpace(texts[idx])) return texts[idx];
            if (idx + 1 < texts.Length && isCaption[idx + 1]
                && !string.IsNullOrWhiteSpace(texts[idx + 1])) return texts[idx + 1];
            return null;
        }

        /// <summary>
        /// The text the image sits among: up to <see cref="AnchorWindow"/>
        /// paragraphs either side. Empty paragraphs are skipped but still count
        /// against the window, so the anchor cannot drift far from the image.
        /// </summary>
        private static string CollectAnchor(string[] texts, int idx)
        {
            var start = Math.Max(0, idx - AnchorWindow);
            var end = Math.Min(texts.Length - 1, idx + AnchorWindow);

            var parts = new List<string>();
            for (int i = start; i <= end; i++)
            {
                var t = (texts[i] ?? "").Trim();
                if (t.Length > 0) parts.Add(t);
            }
            return string.Join("\n", parts);
        }

        // ── DOCX reading ────────────────────────────────────────────────

        /// <summary>
        /// Text belonging to this paragraph itself. Descends into runs but stops
        /// at a nested w:p - a text box inside this paragraph holds its own
        /// paragraphs, and their text is not this paragraph's.
        /// </summary>
        private static string DirectText(OpenXmlElement p)
        {
            var sb = new StringBuilder();
            AppendDirectText(p, sb, true);
            return sb.ToString().Trim();
        }

        private static void AppendDirectText(OpenXmlElement el, StringBuilder sb, bool isRoot)
        {
            foreach (var child in el.ChildElements)
            {
                // A nested w:p is a text box's own paragraph. Its text belongs
                // to it, not to us, and it gets its own turn in the outer loop.
                if (child is Paragraph) continue;

                if (child is Text t) { sb.Append(t.Text); continue; }
                if (child is TabChar) { sb.Append('\t'); continue; }
                if (child is Break) { sb.Append(' '); continue; }

                AppendDirectText(child, sb, false);
            }
        }

        /// <summary>
        /// Image relationship ids referenced by this paragraph: DrawingML
        /// <c>a:blip</c> (r:embed or r:link) and legacy VML <c>v:imagedata</c>
        /// (r:id). Nested w:p is skipped for the same reason as the text.
        /// </summary>
        private static List<string> DirectImageRelationshipIds(OpenXmlElement p)
        {
            var ids = new List<string>();
            CollectRelationshipIds(p, ids, true);
            return ids;
        }

        private static void CollectRelationshipIds(OpenXmlElement el, List<string> ids, bool isRoot)
        {
            foreach (var child in el.ChildElements)
            {
                if (!isRoot && child is Paragraph) continue;

                if (child is A.Blip blip)
                {
                    var rid = blip.Embed?.Value ?? blip.Link?.Value;
                    if (!string.IsNullOrEmpty(rid)) ids.Add(rid);
                }
                else if (child is V.ImageData vml)
                {
                    var rid = vml.RelationshipId?.Value;
                    if (!string.IsNullOrEmpty(rid)) ids.Add(rid);
                }

                CollectRelationshipIds(child, ids, false);
            }
        }

        private static bool IsCaptionStyled(Paragraph p)
        {
            var id = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            return !string.IsNullOrEmpty(id) && CaptionStyleIds.Contains(id.Trim());
        }

        private static ImagePart ResolveImagePart(MainDocumentPart main, string relationshipId)
        {
            try
            {
                return main.GetPartById(relationshipId) as ImagePart;
            }
            catch
            {
                return null;   // dangling relationship
            }
        }

        private static string ExtensionFor(string contentType)
        {
            switch ((contentType ?? "").ToLowerInvariant())
            {
                case "image/png": return ".png";
                case "image/jpeg": return ".jpg";
                case "image/gif": return ".gif";
                case "image/bmp": return ".bmp";
                case "image/tiff": return ".tif";
                case "image/x-emf":
                case "image/emf": return ".emf";
                case "image/x-wmf":
                case "image/wmf": return ".wmf";
                default: return ".img";
            }
        }
    }
}
