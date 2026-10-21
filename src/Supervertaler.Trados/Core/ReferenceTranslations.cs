using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Supervertaler.Trados.Core
{
    /// <summary>One segment of the open document, as Add segments to SuperMemory sees it.</summary>
    internal sealed class ReferenceSegment
    {
        /// <summary>The file it is in, in a document of several files; null otherwise.</summary>
        public string File;

        /// <summary>The number Studio shows: "12", or "331 a" for a split segment.</summary>
        public string Number;

        /// <summary>The number's leading digits, -1 when it has none.</summary>
        public int Value;

        /// <summary>Translated, Translation Approved or Signed Off.</summary>
        public bool Confirmed;

        /// <summary>Plain text, tracked changes accepted. Lazy: only a segment in
        /// the range is ever read, so a 10,000-segment document costs one pass of
        /// numbers and statuses, not 20,000 text extractions.</summary>
        public Lazy<string> Source;
        public Lazy<string> Target;
    }

    /// <summary>The segments a range picks, and what it left out.</summary>
    internal sealed class ReferencePick
    {
        public readonly List<ReferenceSegment> Segments = new List<ReferenceSegment>();

        /// <summary>Segments between the start and the end, before skipping any.</summary>
        public int InRange;

        public int SkippedUnconfirmed;

        /// <summary>No source text or no translation: nothing to learn from.</summary>
        public int SkippedEmpty;

        /// <summary>The picked text passed the caller's limit, and picking stopped there.</summary>
        public bool OverLimit;

        /// <summary>Why nothing can be picked, in words; null when the range is usable.</summary>
        public string Problem;
    }

    /// <summary>
    /// What a translation sends from a bank today, before per-document narrowing,
    /// in tokens - the figures the dialog shows and the budget check uses.
    /// </summary>
    internal sealed class BankFigures
    {
        /// <summary>The bank's own files: brief, style, terminology and the rest of its root.</summary>
        public int Own;

        /// <summary>The <c>_shared</c> layer loaded with it; 0 when the bank is <c>_shared</c>.</summary>
        public int Shared;

        /// <summary>The part of <see cref="Own"/> that is brief.md: 0 when it is
        /// missing, still the untouched skeleton, or marked for the assistants.</summary>
        public int Brief;

        /// <summary>brief.md says <c>audience: assistant</c>, so no translation sends it.</summary>
        public bool BriefAssistantOnly;
    }

    /// <summary>
    /// Add segments to SuperMemory: a range of reviewed segments, written into the
    /// bank's <c>brief.md</c> under <c>## Reference translations</c>, so a later
    /// translation - the description, after the claims - follows the approved
    /// wording.
    ///
    /// <para><b>Why brief.md</b> (Michael, 21 Oct 2026): it is the one bank file a
    /// translation always sends whole. A bank over <see cref="BankExtract.Threshold"/>
    /// is narrowed per document: terminology.md keeps only rows whose term occurs
    /// in the document, which would drop whole segments, and other files are put
    /// to an AI choice. The brief is never selected away, and the budget cuts it
    /// last (<see cref="KbContext.TrimToTokenBudget"/>).</para>
    ///
    /// <para><b>Replaced, not added to:</b> a bank is per client, so one job's
    /// claims left in it would go with every later job for that client.</para>
    ///
    /// <para>Nothing here touches Studio or writes: the action gathers the
    /// segments, <see cref="Measure"/> reads the bank through
    /// <see cref="MemoryBankReader"/>, and <see cref="BankFileStore"/> writes.</para>
    /// </summary>
    internal static class ReferenceTranslations
    {
        /// <summary>The ## heading in brief.md.</summary>
        public const string Heading = "Reference translations";

        /// <summary>Above this, the dialog says what the section costs per request.</summary>
        public const int WarnTokens = 4000;

        /// <summary>
        /// Refused when the bank's own files would come to more than this: past it,
        /// the budget cuts that bank's own terminology and style from every prompt,
        /// even with <c>_shared</c> left out entirely. <c>_shared</c> is not counted
        /// - it is narrowed per document and dropped first, and on its own it can
        /// already be over the budget before narrowing (decided with Michael, 21 Oct
        /// 2026).
        /// </summary>
        public static int BudgetTokens => TranslationBankContext.TokenBudget;

        /// <summary>The estimate every bank figure uses (<see cref="KbContext.EstimatedTokens"/>).</summary>
        public static int Tokens(int chars) => chars / 4;

        public static int Tokens(string text) => Tokens(text?.Length ?? 0);

        /// <summary>
        /// The bank as a translation loads it now (assistant-only notes left out,
        /// nothing narrowed). <paramref name="briefText"/> is brief.md as read for
        /// the write, or null when there is none. Reads a few files: call it when
        /// the bank is chosen, not on every keystroke.
        /// </summary>
        public static BankFigures Measure(string bankDir, string briefText)
        {
            var f = new BankFigures
            {
                BriefAssistantOnly = !string.IsNullOrEmpty(briefText) && MemoryBankReader.IsAssistantOnly(briefText)
            };

            KbContext ctx = null;
            try
            {
                ctx = new MemoryBankReader(bankDir).LoadContext(null, null, null, null,
                    tokenBudget: 0, forTranslation: true);
            }
            catch { /* a bank that cannot be read counts as empty; the write will say why */ }
            if (ctx == null) return f;

            f.Brief = Tokens(ctx.ClientProfileText);
            f.Own = Tokens(Len(ctx.ClientProfileText) + Len(ctx.StyleGuideText)
                + ctx.TerminologyArticles.Sum(Len) + ctx.ExtraArticles.Sum(Len));
            f.Shared = Tokens(Len(ctx.SharedBriefText) + Len(ctx.SharedStyleText)
                + Len(ctx.SharedTerminologyText) + ctx.SharedExtraArticles.Sum(Len));
            return f;
        }

        /// <summary>
        /// The bank's own tokens once <paramref name="newBody"/> is the section:
        /// the brief as it will read, in place of the brief as it is counted now.
        /// A brief still in its untouched skeleton counts as nothing today and as
        /// all of it afterwards, which is what a translation will then send.
        /// </summary>
        public static int OwnAfter(BankFigures now, string briefText, string newBody)
        {
            if (now == null) return Tokens(newBody);
            if (now.BriefAssistantOnly) return now.Own;   // sent by no translation, before or after

            var brief = (briefText ?? "").Trim().Length;
            var body = (newBody ?? "").Trim().Length;
            var old = ExistingSection(briefText, out _);
            var after = old != null
                ? brief - old.Length + body
                : brief + ("\n\n## " + Heading + "\n\n").Length + body;
            return now.Own - now.Brief + Tokens(after);
        }

        private static int Len(string s) => s?.Length ?? 0;

        /// <summary>The leading digits of a Studio segment number, -1 when there are none.</summary>
        public static int LeadingNumber(string number)
        {
            var s = (number ?? "").Trim();
            int digits = 0;
            while (digits < s.Length && char.IsDigit(s[digits])) digits++;
            return digits > 0 && int.TryParse(s.Substring(0, digits), NumberStyles.None,
                CultureInfo.InvariantCulture, out var n) ? n : -1;
        }

        /// <summary>
        /// The segments from (<paramref name="fromFile"/>, <paramref name="from"/>)
        /// to (<paramref name="toFile"/>, <paramref name="to"/>), in document order.
        ///
        /// <para>By position, not by number: in a merged document numbers start
        /// again in each file, so "1 to 20" alone is in every file. The start is
        /// the first segment of its file numbered <paramref name="from"/> or more,
        /// the end the last of its file numbered <paramref name="to"/> or less, and
        /// everything between them is the range - across files, when they differ.
        /// A file of null means the whole document.</para>
        ///
        /// <para>Text is read only for segments in the range, and picking stops
        /// once the picked text passes <paramref name="maxChars"/> (0: no limit),
        /// so an over-large range costs no more than the limit.</para>
        /// </summary>
        public static ReferencePick Pick(IList<ReferenceSegment> all, string fromFile, int from,
            string toFile, int to, bool confirmedOnly, int maxChars = 0)
        {
            var pick = new ReferencePick();
            all = all ?? new List<ReferenceSegment>();
            if (all.Count == 0) { pick.Problem = "The document has no segments."; return pick; }
            if (from < 1 || to < 1) { pick.Problem = "Segment numbers start at 1."; return pick; }

            var start = -1;
            for (var i = 0; i < all.Count && start < 0; i++)
                if (SameFile(all[i].File, fromFile) && all[i].Value >= from) start = i;

            var end = -1;
            for (var i = all.Count - 1; i >= 0 && end < 0; i--)
                if (SameFile(all[i].File, toFile) && all[i].Value >= 0 && all[i].Value <= to) end = i;

            if (start < 0)
            {
                pick.Problem = "There is no segment " + from + Where(fromFile) + ".";
                return pick;
            }
            if (end < 0)
            {
                pick.Problem = "There is no segment " + to + " or lower" + Where(toFile) + ".";
                return pick;
            }
            if (start > end)
            {
                pick.Problem = "The range starts after it ends: segment " + from + Where(fromFile)
                    + " comes after segment " + to + Where(toFile) + ".";
                return pick;
            }

            var chars = 0;
            for (var i = start; i <= end; i++)
            {
                var s = all[i];
                pick.InRange++;
                if (confirmedOnly && !s.Confirmed) { pick.SkippedUnconfirmed++; continue; }

                var source = OneLine(s.Source?.Value);
                var target = OneLine(s.Target?.Value);
                if (source.Length == 0 || target.Length == 0) { pick.SkippedEmpty++; continue; }

                pick.Segments.Add(s);
                chars += source.Length + target.Length;
                if (maxChars > 0 && chars > maxChars) { pick.OverLimit = true; break; }
            }
            return pick;
        }

        /// <summary>
        /// The section body, Markdown: a line saying what it is, then each segment
        /// as its Studio number with the source and the translation under it, and a
        /// ### heading with the file name wherever a new file starts. Every line of
        /// a segment starts with "- " or "**", so no text from a document can open a
        /// heading or a code fence of its own (<see cref="BankFileStore.ReplaceSection"/>
        /// refuses a body with a ## heading, and a fence would hide the next one).
        /// </summary>
        public static string Body(IList<ReferenceSegment> segments, string sourceLabel, string targetLabel,
            string rangeLabel, DateTime when)
        {
            sourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "Source" : sourceLabel.Trim();
            targetLabel = string.IsNullOrWhiteSpace(targetLabel) ? "Translation" : targetLabel.Trim();

            var sb = new StringBuilder();
            sb.Append("Reviewed translations from this job (")
              .Append(rangeLabel)
              .Append("), added with Add segments to SuperMemory on ")
              .Append(when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
              .Append(". They are approved wording: where the text you translate repeats or refers to one of these")
              .Append(" passages, follow its translation and terminology. Adding segments to this bank again replaces")
              .Append(" this section.")
              .Append('\n');

            string file = null;
            foreach (var s in segments ?? new List<ReferenceSegment>())
            {
                if (s.File != null && !string.Equals(s.File, file, StringComparison.Ordinal))
                {
                    file = s.File;
                    sb.Append('\n').Append("### ").Append(OneLine(file)).Append('\n');
                }
                sb.Append('\n')
                  .Append("**").Append(OneLine(s.Number)).Append("**").Append('\n')
                  .Append("- ").Append(sourceLabel).Append(": ").Append(OneLine(s.Source?.Value)).Append('\n')
                  .Append("- ").Append(targetLabel).Append(": ").Append(OneLine(s.Target?.Value)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>"segments 1–20", with the file in a merged document, and both ends when they are in different files.</summary>
        public static string RangeLabel(IList<ReferenceSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "no segments";
            var first = segments[0];
            var last = segments[segments.Count - 1];
            if (segments.Count == 1)
                return "segment " + first.Number + (first.File != null ? " of " + first.File : "");
            if (string.Equals(first.File, last.File, StringComparison.Ordinal))
                return "segments " + first.Number + "–" + last.Number + (first.File != null ? " of " + first.File : "");
            return "segment " + first.Number + " of " + first.File + " to segment " + last.Number + " of " + last.File;
        }

        /// <summary>
        /// The body of the <c>## Reference translations</c> section of a file as it
        /// is, or null when there is none. <paramref name="count"/> says how many
        /// such sections there are: with more than one, <see cref="BankFileStore.ReplaceSection"/>
        /// refuses, and the dialog says so before the write rather than after.
        /// Headings are found as BankFileStore finds them: level 2, outside
        /// frontmatter and code fences, compared without case.
        /// </summary>
        public static string ExistingSection(string text, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(text)) return null;

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var i = 0;
            if (lines.Length > 0 && lines[0].Trim() == "---")
                for (var j = 1; j < lines.Length; j++)
                    if (lines[j].Trim() == "---" || lines[j].Trim() == "...") { i = j + 1; break; }

            string fence = null;
            StringBuilder body = null;
            string found = null;
            for (; i < lines.Length; i++)
            {
                var line = lines[i];
                var t = line.TrimStart();
                if (fence != null)
                {
                    if (t.StartsWith(fence, StringComparison.Ordinal)) fence = null;
                    body?.Append(line).Append('\n');
                    continue;
                }
                if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal))
                {
                    fence = t.Substring(0, 3);
                    body?.Append(line).Append('\n');
                    continue;
                }

                var level = HeadingLevel(line, out var heading);
                if (level > 0 && level <= 2)
                {
                    if (body != null && found == null) found = body.ToString();
                    body = null;
                    if (level == 2 && string.Equals(heading, Heading, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                        body = new StringBuilder();
                    }
                    continue;
                }
                body?.Append(line).Append('\n');
            }
            if (body != null && found == null) found = body.ToString();
            return found?.Trim();
        }

        /// <summary>
        /// A merged document whose files the map could not tell apart: they are
        /// told apart by their numbering instead, which Studio starts again at 1 in
        /// each file and never lets fall within one (a merge keeps the surviving
        /// number, a split adds a letter). Without this, "1 to 20" ran from the
        /// first file into the last. Returns what the dialog says about it, or
        /// null when nothing was done.
        /// </summary>
        public static string NumberFiles(IList<ReferenceSegment> segments, int fileCount)
        {
            if (fileCount < 2 || segments.Any(s => s.File != null)) return null;

            var run = 1;
            var previous = -1;
            foreach (var s in segments)
            {
                if (s.Value >= 1 && previous >= 1 && s.Value < previous) run++;
                if (s.Value >= 1) previous = s.Value;
                s.File = "file " + run;
            }
            return "Supervertaler could not read which file each segment of this merged document is in, so the files "
                + "are called file 1, file 2 and so on, in the order the document shows them: a new one starts where the "
                + "segment numbers start again. Check the segments below.";
        }

        /// <summary>A segment's text on one line: soft returns and paragraph
        /// separators become spaces, so the text cannot start a line of its own.</summary>
        internal static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            var space = false;
            foreach (var c in text)
            {
                var ws = c == '\r' || c == '\n' || c == '\u2028' || c == '\u2029' || c == '\t';
                if (ws || c == ' ')
                {
                    if (!space && sb.Length > 0) sb.Append(' ');
                    space = true;
                    continue;
                }
                sb.Append(c);
                space = false;
            }
            return sb.ToString().TrimEnd();
        }

        private static bool SameFile(string segmentFile, string wanted) =>
            wanted == null || string.Equals(segmentFile, wanted, StringComparison.Ordinal);

        private static string Where(string file) => file == null ? "" : " in " + file;

        /// <summary>1-6 for an ATX heading, 0 otherwise; the same rule as BankFileStore.</summary>
        private static int HeadingLevel(string line, out string text)
        {
            text = null;
            int i = 0;
            while (i < line.Length && i < 3 && line[i] == ' ') i++;
            int h = 0;
            while (i + h < line.Length && line[i + h] == '#') h++;
            if (h == 0 || h > 6) return 0;
            var rest = i + h;
            if (rest < line.Length && line[rest] != ' ' && line[rest] != '\t') return 0;

            var t = line.Substring(rest).Trim();
            var k = t.Length;
            while (k > 0 && t[k - 1] == '#') k--;
            if (k == 0) t = "";
            else if (k < t.Length && (t[k - 1] == ' ' || t[k - 1] == '\t')) t = t.Substring(0, k).Trim();
            text = t;
            return h;
        }
    }
}
