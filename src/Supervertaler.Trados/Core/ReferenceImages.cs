using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Finds the drawings that belong to a Trados project.
    ///
    /// <para>Patent filings ship with figures, and the figures carry information
    /// the text does not — most of it reducible to "which physical part does each
    /// reference numeral denote". This class is step 1 of the reference-image
    /// feature: locating the images. What is eventually done with them (a numeral
    /// reconciliation report, a vision pass writing <c>figures.md</c> into the
    /// memory bank) builds on top.</para>
    ///
    /// <para>Deliberately has no dependency on Studio or on any AI provider, so it
    /// can be exercised without either.</para>
    /// </summary>
    public static class ReferenceImages
    {
        /// <summary>Folder names looked for by convention, in preference order.</summary>
        private static readonly string[] ConventionalNames = { "Images", "Figures", "images", "figures" };

        /// <summary>
        /// Extensions we treat as drawings. Deliberately narrow: a reference
        /// folder full of PDFs and DOCX would otherwise be reported as usable and
        /// then fail in the vision pass, which is a worse failure than saying
        /// "no images found" up front.
        /// </summary>
        private static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

        /// <summary>
        /// Resolves the reference-images folder for a project: the explicit
        /// per-project setting when it is set and exists, otherwise convention.
        ///
        /// <para>Returns null when nothing is found. Callers should treat that as
        /// "this project has no drawings", not as an error — most do not.</para>
        /// </summary>
        /// <param name="projectFilePath">Path to the .sdlproj file (or its folder).</param>
        /// <param name="explicitFolder">The per-project override; may be null or blank.</param>
        public static string Resolve(string projectFilePath, string explicitFolder)
        {
            // An explicit setting wins, but only if it still exists — a folder
            // that was moved or lives on a disconnected drive should fall back to
            // convention rather than making the feature look broken.
            if (!string.IsNullOrWhiteSpace(explicitFolder))
            {
                try
                {
                    if (Directory.Exists(explicitFolder)) return Path.GetFullPath(explicitFolder);
                }
                catch { /* malformed path — fall through to convention */ }
            }

            return FindByConvention(projectFilePath);
        }

        /// <summary>
        /// Looks for an Images/ or Figures/ folder near the project.
        ///
        /// <para>Searches the project folder, then walks UP two levels. That is not
        /// arbitrary: Studio keeps the .sdlproj in its own <c>Studio\</c> subfolder
        /// inside the job folder, and the drawings land beside it when they are
        /// extracted from the application PDF —
        /// <c>…\BRANTS (BARI-001-BE-EP)\Images\</c> next to
        /// <c>…\BRANTS (BARI-001-BE-EP)\Studio\</c>. Searching only the project
        /// folder would miss the case the convention exists to serve.</para>
        /// </summary>
        public static string FindByConvention(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath)) return null;

            try
            {
                var dir = Directory.Exists(projectFilePath)
                    ? projectFilePath
                    : Path.GetDirectoryName(projectFilePath);

                for (int level = 0; level < 3 && !string.IsNullOrEmpty(dir); level++)
                {
                    foreach (var name in ConventionalNames)
                    {
                        var candidate = Path.Combine(dir, name);
                        // Must actually contain images. A stray empty "Images"
                        // folder should not shadow a populated one a level up.
                        if (Directory.Exists(candidate) && HasAnyImage(candidate))
                            return Path.GetFullPath(candidate);
                    }

                    dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
                }
            }
            catch { /* permissions, malformed path — treat as "not found" */ }

            return null;
        }

        private static bool HasAnyImage(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder)
                    .Any(f => ImageExtensions.Contains(Path.GetExtension(f)));
            }
            catch { return false; }
        }

        /// <summary>
        /// The image files in a folder, ordered the way a reader would expect.
        ///
        /// <para>Ordering is the whole point of this method. The convention is one
        /// file per figure named for its label — <c>FIG. 1.png</c> …
        /// <c>FIG. 9.png</c> — and an ordinary string sort puts <c>FIG. 10</c>
        /// between <c>FIG. 1</c> and <c>FIG. 2</c>. A vision pass handed the
        /// figures out of order will caption them out of order, and the error is
        /// invisible in the output.</para>
        /// </summary>
        public static List<ReferenceImage> List(string folder)
        {
            var result = new List<ReferenceImage>();
            if (string.IsNullOrWhiteSpace(folder)) return result;

            try
            {
                foreach (var path in Directory.EnumerateFiles(folder))
                {
                    if (!ImageExtensions.Contains(Path.GetExtension(path))) continue;
                    result.Add(new ReferenceImage
                    {
                        Path = path,
                        FileName = Path.GetFileName(path),
                        FigureLabel = ParseFigureLabel(Path.GetFileNameWithoutExtension(path)),
                        FigureNumber = ParseFigureNumber(Path.GetFileNameWithoutExtension(path))
                    });
                }
            }
            catch { return result; }

            // Numbered figures first in numeric order, then anything unnumbered
            // alphabetically, so an odd file never displaces the sequence.
            result.Sort((a, b) =>
            {
                if (a.FigureNumber.HasValue && b.FigureNumber.HasValue)
                    return a.FigureNumber.Value.CompareTo(b.FigureNumber.Value);
                if (a.FigureNumber.HasValue) return -1;
                if (b.FigureNumber.HasValue) return 1;
                return StringComparer.OrdinalIgnoreCase.Compare(a.FileName, b.FileName);
            });

            return result;
        }

        // "FIG. 1", "Fig 1", "Figure 1", "FIG-1", "1" — the label as the text
        // would cite it, so the vision pass can label its output without guessing.
        private static readonly Regex FigureRe = new Regex(
            @"^\s*(?:fig(?:ure)?\.?\s*[-_]?\s*)?(\d{1,3})\s*([a-z])?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>The figure label, normalised to "FIG. n" (or "FIG. nA"), or
        /// the bare filename when it does not look like a figure at all.</summary>
        public static string ParseFigureLabel(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) return "";
            var m = FigureRe.Match(fileNameWithoutExtension);
            if (!m.Success) return fileNameWithoutExtension.Trim();

            var suffix = m.Groups[2].Success ? m.Groups[2].Value.ToUpperInvariant() : "";
            return "FIG. " + m.Groups[1].Value + suffix;
        }

        /// <summary>The figure's number, or null when the name is not a figure.</summary>
        public static int? ParseFigureNumber(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) return null;
            var m = FigureRe.Match(fileNameWithoutExtension);
            if (!m.Success) return null;
            return int.TryParse(m.Groups[1].Value, out var n) ? n : (int?)null;
        }
    }

    /// <summary>One drawing on disk.</summary>
    public class ReferenceImage
    {
        public string Path { get; set; }
        public string FileName { get; set; }

        /// <summary>Normalised label, e.g. "FIG. 3". Falls back to the filename
        /// when the name does not follow the convention.</summary>
        public string FigureLabel { get; set; }

        /// <summary>Figure number when the filename yields one; null otherwise.</summary>
        public int? FigureNumber { get; set; }

        public override string ToString() => FigureLabel + " (" + FileName + ")";
    }
}
