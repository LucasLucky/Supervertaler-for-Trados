using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Supervertaler.Core;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The memory-bank block a TRANSLATION sends - Batch Translate, Alt+T, the
    /// prompt preview and SuperBench - for one document.
    ///
    /// <para>It differs from the chat's block in two ways. Notes marked
    /// <c>audience: assistant</c> stay out: they are written for the AI Assistant
    /// and the MCP clients, and the chat and MCP still load them. And a bank over
    /// <see cref="BankExtract.Threshold"/> is narrowed to the part this document
    /// needs (see <see cref="BankExtract"/>): the terminology rows that occur in
    /// it, and the articles one small AI call picks from the start of the
    /// document.</para>
    ///
    /// <para>That call is made the first time a document is translated, never
    /// when it is opened, so no AI call is made and no document text leaves the
    /// machine unless the translator translates (decided 27 Sep 2026). The result
    /// is then reused for the document until the bank changes, so every request
    /// of a job carries the same bytes and reads the provider's prompt
    /// cache.</para>
    ///
    /// <para>Everything here may run off the UI thread. It reads no Trados object:
    /// the view part gathers the document and the bank on the UI thread and hands
    /// them in.</para>
    /// </summary>
    internal static class TranslationBankContext
    {
        /// <summary>The budget every translation prompt has carried the bank under.</summary>
        internal const int TokenBudget = 24000;

        /// <summary>How long the article choice may take before every article is kept.</summary>
        internal static readonly TimeSpan ChooserTimeout = TimeSpan.FromSeconds(90);

        /// <summary>How many extract files are kept - one per document ever translated would grow forever.</summary>
        internal const int KeepFiles = 200;

        /// <summary>An extract file not rewritten for this long belongs to a finished job.</summary>
        internal static readonly TimeSpan KeepFilesFor = TimeSpan.FromDays(90);

        /// <summary>
        /// Where each document's extract is written for the translator to read.
        /// Outside the bank folders, which may be synced or kept in git; memoQ
        /// keeps its own under memoq/.
        /// </summary>
        internal static string ExtractsDir => Path.Combine(UserDataPath.TradosDir, "bank-extracts");

        internal sealed class Result
        {
            /// <summary>What goes into the prompt; null when there is nothing to send.</summary>
            public string Block;

            /// <summary>Lines for the Batch log, each complete in itself.</summary>
            public List<string> Log = new List<string>();
        }

        /// <summary>
        /// The files <see cref="MemoryBankReader.LoadContext"/> reads - the
        /// top-level <c>*.md</c> of the bank and of <c>_shared</c> - by name, size
        /// and time, so an edit, an addition, a removal or a rename all change it.
        /// <c>reference/</c> never reaches a prompt, so it is not looked at. A few
        /// files, cheap enough to take on every translation.
        /// </summary>
        internal static string BankFingerprint(string bankDir)
        {
            var sb = new StringBuilder();
            foreach (var dir in new[] { bankDir, SharedDir(bankDir) })
            {
                sb.Append('>').Append(dir);
                if (dir == null || !Directory.Exists(dir)) continue;
                try
                {
                    var files = new DirectoryInfo(dir).GetFiles("*.md", SearchOption.TopDirectoryOnly);
                    Array.Sort(files, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
                    foreach (var f in files)
                        sb.Append('|').Append(f.Name).Append(':').Append(f.Length)
                          .Append(':').Append(f.LastWriteTimeUtc.Ticks);
                }
                catch
                {
                    // Never matches a cached block: a bank that could not be listed
                    // is read again next time rather than remembered as it looked.
                    sb.Append("|unlisted:").Append(Guid.NewGuid().ToString("N"));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Selects from <paramref name="full"/> for this document and formats the
        /// result. Never throws: the bank is optional, and a translation must not
        /// fail because of it.
        ///
        /// <para><paramref name="full"/> is loaded untrimmed (budget 0) and for
        /// translation, and is modified in place. <paramref name="reload"/> loads
        /// the bank again the old way - whole, within <see cref="TokenBudget"/> -
        /// for when selecting fails part-way and <paramref name="full"/> can no
        /// longer be trusted. <paramref name="documentText"/> may be null for a
        /// bank at or under the threshold, which is sent whole without it.</para>
        /// </summary>
        internal static Result Build(
            KbContext full,
            string documentText,
            string sourceLang,
            string targetLang,
            Func<ArticleSelectionRequest, IList<string>> chooseArticles,
            string chooserName,
            Func<KbContext> reload,
            string label,
            string fileStem)
        {
            var result = new Result();
            try
            {
                var extract = BankExtract.Build(full, documentText, sourceLang, targetLang, TokenBudget,
                    chooseArticles, earlierChoice: null, chooserName: chooserName);

                result.Block = extract.Context.HasContent ? MemoryBankReader.FormatForPrompt(extract.Context) : null;

                if (!extract.Selected)
                {
                    result.Log.Add(extract.Context.GetSummary() ?? ("SuperMemory: ~" + extract.Context.EstimatedTokens + " tokens"));
                    foreach (var line in extract.Report) result.Log.Add("  " + line);
                    return result;
                }

                var path = WriteFile(extract, label, fileStem, out var writeError);
                result.Log.Add("SuperMemory: sending " + Tokens(extract.TokensAfter) + " of the bank's "
                    + Tokens(extract.TokensBefore) + ", selected for this document.");
                foreach (var line in extract.Report) result.Log.Add("  " + line);
                result.Log.Add(path != null
                    ? "  What was sent, and why: " + path
                    : "  Could not write the extract file (" + writeError + ").");
                return result;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("SuperMemory", "Selecting from the bank failed: " + ex);
                result.Log.Clear();
                try
                {
                    var whole = reload?.Invoke();
                    result.Block = whole != null && whole.HasContent ? MemoryBankReader.FormatForPrompt(whole) : null;
                    result.Log.Add("SuperMemory: could not select from the bank (" + ex.Message + "); sending it "
                        + "as before, within the " + Tokens(TokenBudget) + " budget.");
                }
                catch (Exception ex2)
                {
                    DiagnosticLog.Log("SuperMemory", "Loading the bank whole failed too: " + ex2);
                    result.Block = null;
                    result.Log.Add("SuperMemory: could not read the bank (" + ex2.Message + "); nothing from it is sent.");
                }
                return result;
            }
        }

        /// <summary>
        /// The article choice: one request to the model the translation uses.
        /// A null answer or a throw makes <see cref="BankExtract.Build"/> keep every
        /// article and record why. It appears in Reports and the usage ledger under
        /// SuperMemory, like every other call that costs money.
        /// </summary>
        internal static IList<string> ChooseArticles(LlmClient client, ArticleSelectionRequest request, TimeSpan timeout)
        {
            using (var cancel = new CancellationTokenSource(timeout))
            {
                string reply;
                try
                {
                    reply = client.SendPromptAsync(
                            BankExtract.SelectionUserPrompt(request),
                            BankExtract.SelectionSystemPrompt,
                            cancellationToken: cancel.Token,
                            feature: PromptLogFeature.SuperMemory,
                            promptName: "Memory bank selection")
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    throw new TimeoutException("no answer within " + (int)timeout.TotalSeconds + " seconds");
                }
                return BankExtract.ParseSelection(reply, request.Candidates);
            }
        }

        /// <summary>
        /// A file name for one document's extract: its name, readable, and a short
        /// hash of its identity, so two documents called the same do not share one.
        /// </summary>
        internal static string FileStem(string documentName, string documentKey)
        {
            var name = documentName ?? "";
            if (name.EndsWith(".sdlxliff", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - ".sdlxliff".Length);
            var invalid = Path.GetInvalidFileNameChars();
            name = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd(' ', '.');
            if (name.Length == 0) name = "document";

            string hash;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(documentKey ?? ""));
                hash = string.Concat(bytes.Take(4).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
            return name + " - " + hash;
        }

        /// <summary>
        /// Writes the extract where the translator can read it. Returns the path,
        /// or null with <paramref name="error"/> set; never throws, since the file
        /// is for reading and the translation does not depend on it.
        /// </summary>
        internal static string WriteFile(BankExtractResult extract, string label, string fileStem, out string error)
        {
            error = null;
            try
            {
                var dir = ExtractsDir;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, fileStem + ".md");
                File.WriteAllText(path, BankExtract.FormatFile(extract, label, DateTime.Now), new UTF8Encoding(false));

                var pruned = Prune(dir, KeepFiles, KeepFilesFor, path);
                if (pruned > 0) DiagnosticLog.Log("SuperMemory", "Removed " + pruned + " old bank extract file(s)");
                return path;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                DiagnosticLog.Log("SuperMemory", "Could not write the bank extract: " + ex);
                return null;
            }
        }

        /// <summary>
        /// Keeps the newest <paramref name="keep"/> extract files and removes any
        /// older than <paramref name="maxAge"/>, never <paramref name="justWritten"/>.
        /// Returns how many went. A file that cannot be removed is left for next
        /// time: this is housekeeping, and must never fail the write it follows.
        /// </summary>
        internal static int Prune(string folder, int keep, TimeSpan maxAge, string justWritten)
        {
            var removed = 0;
            try
            {
                var files = new DirectoryInfo(folder).GetFiles("*.md")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();
                var cutoff = DateTime.UtcNow - maxAge;

                for (var i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (string.Equals(f.FullName, justWritten, StringComparison.OrdinalIgnoreCase)) continue;
                    if (i < keep && f.LastWriteTimeUtc >= cutoff) continue;

                    try { f.Delete(); removed++; }
                    catch { /* locked or in use: next time */ }
                }
            }
            catch { /* a folder we cannot list is not worth failing over */ }
            return removed;
        }

        /// <summary>The <c>_shared</c> bank beside <paramref name="bankDir"/>, or null when the bank is <c>_shared</c> itself.</summary>
        private static string SharedDir(string bankDir)
        {
            try
            {
                var name = Path.GetFileName((bankDir ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.Equals(name, MemoryBankReader.SharedBankName, StringComparison.OrdinalIgnoreCase)) return null;
                var root = Path.GetDirectoryName(bankDir);
                return string.IsNullOrEmpty(root) ? null : Path.Combine(root, MemoryBankReader.SharedBankName);
            }
            catch { return null; }
        }

        private static string Tokens(int n) =>
            n.ToString("N0", CultureInfo.InvariantCulture) + " tokens";
    }
}
