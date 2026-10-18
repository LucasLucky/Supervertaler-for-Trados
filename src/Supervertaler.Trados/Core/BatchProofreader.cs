using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Models;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    // ─── Event Args ──────────────────────────────────────────

    public class ProofreadSegmentEventArgs : EventArgs
    {
        public int SegmentIndex { get; set; }
        public ProofreadingIssue Issue { get; set; }
    }

    public class ProofreadCompletedEventArgs : EventArgs
    {
        public int TotalChecked { get; set; }
        public int IssueCount { get; set; }
        public int OkCount { get; set; }
        public TimeSpan Elapsed { get; set; }
        public bool Cancelled { get; set; }
    }

    // ─── Engine ──────────────────────────────────────────────

    /// <summary>
    /// Core batch proofreading engine. UI-agnostic – communicates via events.
    /// Parallel to BatchTranslator – uses the same event-driven pattern.
    /// </summary>
    public class BatchProofreader
    {
        public event EventHandler<BatchProgressEventArgs> Progress;
        public event EventHandler<ProofreadSegmentEventArgs> SegmentProofread;
        public event EventHandler<ProofreadCompletedEventArgs> Completed;

        /// <summary>
        /// Proofreads segments in batches using the configured LLM provider.
        /// This method is async and should be called on a background thread
        /// or via Task.Run.
        /// </summary>
        public async Task ProofreadAsync(
            List<BatchSegment> segments,
            string sourceLang,
            string targetLang,
            AiSettings aiSettings,
            List<TermEntry> termbaseTerms,
            int batchSize,
            CancellationToken cancellationToken,
            string customPromptContent = null,
            List<(string source, string target)> documentSegments = null,
            string kbContext = null,
            bool targetOnly = false)
        {
            var sw = Stopwatch.StartNew();
            int totalChecked = 0;
            int issueCount = 0;
            int okCount = 0;

            if (segments == null || segments.Count == 0)
            {
                Completed?.Invoke(this, new ProofreadCompletedEventArgs
                {
                    Elapsed = sw.Elapsed
                });
                return;
            }

            // Build system prompt with full bilingual document context (no truncation –
            // proofreading specifically benefits from cross-document consistency checks)
            // and the memory bank, which the translation is checked against.
            var includeDoc = aiSettings?.IncludeDocumentContext != false;
            var includeTermMeta = aiSettings?.IncludeTermMetadata != false;
            var systemPrompt = ProofreadingPrompt.BuildSystemPrompt(
                sourceLang, targetLang, termbaseTerms, customPromptContent,
                includeDoc ? documentSegments : null,
                includeTermMeta, kbContext, targetOnly);

            // Resolve provider settings
            var provider = aiSettings.SelectedProvider ?? LlmModels.ProviderOpenAi;
            var model = aiSettings.GetSelectedModel();
            var apiKey = LlmClient.ResolveApiKey(provider, aiSettings.ApiKeys);
            string baseUrl = null;

            if (provider == LlmModels.ProviderOllama)
                baseUrl = aiSettings.OllamaEndpoint ?? "http://localhost:11434";
            else if (provider == LlmModels.ProviderCustomOpenAi)
            {
                var profile = aiSettings.GetActiveCustomProfile();
                if (profile != null)
                {
                    baseUrl = profile.Endpoint;
                    model = profile.Model;
                    apiKey = profile.ApiKey;
                }
            }

            // Determine max tokens based on model
            var modelInfo = LlmModels.FindModel(model);
            int maxTokens = modelInfo?.DefaultMaxTokens ?? 16384;

            if (batchSize <= 0) batchSize = 20;

            // Split into batches
            int totalBatches = (segments.Count + batchSize - 1) / batchSize;

            RaiseProgress(0, segments.Count, "Starting proofreading...", false, TimeSpan.Zero);

            using (var client = new LlmClient(provider, model, apiKey, baseUrl, maxTokens,
                ollamaTimeoutMinutes: aiSettings.OllamaTimeoutMinutes))
            {
                for (int batchNum = 0; batchNum < totalBatches; batchNum++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    int startIdx = batchNum * batchSize;
                    int endIdx = Math.Min(startIdx + batchSize, segments.Count);
                    int batchCount = endIdx - startIdx;

                    RaiseProgress(startIdx, segments.Count,
                        $"Proofreading batch {batchNum + 1}/{totalBatches} " +
                        $"(segments {startIdx + 1}\u2013{endIdx})...",
                        false, sw.Elapsed);

                    try
                    {
                        var batchSw = Stopwatch.StartNew();

                        var userPrompt = UserPromptFor(segments, startIdx, endIdx, targetOnly);

                        // Call LLM. enablePromptCaching: the system prompt (instructions + termbase +
                        // memory bank + bilingual document context) is byte-stable across all batches
                        // in this proofread run. Anthropic and OpenRouter→Anthropic add explicit cache_control markers;
                        // OpenAI/DeepSeek/Gemini 2.5+ benefit from implicit automatic caching.
                        var response = await client.SendPromptAsync(
                            userPrompt, systemPrompt, maxTokens, cancellationToken,
                            feature: PromptLogFeature.Proofread,
                            enablePromptCaching: true);

                        // Parse response
                        var parsed = ProofreadingPrompt.ParseBatchResponse(
                            response, startIdx + 1, batchCount);

                        // Map parsed results back to segments by absolute number
                        var resultMap = new Dictionary<int, (bool isOk, string issue, string suggestion, string evidence, string corrected)>();
                        foreach (var p in parsed)
                            resultMap[p.segmentNumber] = (p.isOk, p.issueDescription, p.suggestion, p.evidence, p.corrected);

                        // Apply results
                        int batchIssues = 0;
                        int batchOk = 0;
                        int batchUnread = 0;

                        for (int i = startIdx; i < endIdx; i++)
                        {
                            int number = segments[i].Index + 1; // absolute document position, matches prompt

                            // Extract paragraph unit ID and segment ID from ref array
                            string puId = null, segId = null;
                            if (segments[i].SegmentPairRef is string[] refArr && refArr.Length >= 2)
                            {
                                puId = refArr[0];
                                segId = refArr[1];
                            }

                            var proofResult = new ProofreadingIssue
                            {
                                SegmentIndex = segments[i].Index,
                                SegmentNumber = segments[i].Index + 1, // actual document segment number
                                SourceText = segments[i].SourceText,
                                TargetText = segments[i].ExistingTarget,
                                SegmentPairRef = segments[i].SegmentPairRef,
                                ParagraphUnitId = puId,
                                SegmentId = segId
                            };

                            if (resultMap.TryGetValue(number, out var result))
                            {
                                proofResult.IsOk = result.isOk;
                                proofResult.IssueDescription = result.issue;
                                proofResult.Suggestion = result.suggestion;
                                proofResult.Evidence = result.evidence;
                                // What Apply on the Reports tab would write; null when
                                // the reply gave none or it changes nothing.
                                if (!result.isOk)
                                    proofResult.CorrectedTarget = ProofreadCorrection.Normalise(
                                        segments[i].ExistingTarget, result.corrected);
                            }
                            else
                            {
                                // No result from AI – mark as OK by default
                                proofResult.IsOk = true;
                                batchUnread++;
                            }

                            SegmentProofread?.Invoke(this, new ProofreadSegmentEventArgs
                            {
                                SegmentIndex = segments[i].Index,
                                Issue = proofResult
                            });

                            if (proofResult.IsOk)
                            {
                                batchOk++;
                                okCount++;
                            }
                            else
                            {
                                batchIssues++;
                                issueCount++;
                            }

                            totalChecked++;
                            RaiseProgress(i + 1, segments.Count, null, false, sw.Elapsed);
                        }

                        batchSw.Stop();
                        RaiseProgress(endIdx, segments.Count,
                            $"\u2713 Batch {batchNum + 1} complete: " +
                            $"{batchOk} OK, {batchIssues} issues" +
                            $" ({batchSw.Elapsed.TotalSeconds:F1}s)",
                            false, sw.Elapsed);

                        // A segment the reply gave no verdict for that could be read
                        // is counted as OK above, so a reply in a shape the parser
                        // does not know looked exactly like a clean run (18 Oct 2026:
                        // 21 of 21, three real issues among them). Say so.
                        if (batchUnread > 0)
                            RaiseProgress(endIdx, segments.Count,
                                $"Batch {batchNum + 1}: no verdict could be read for {batchUnread} of {batchCount} " +
                                "segments in the AI's reply, so they are shown as OK. Run them again, or try another model.",
                                true, sw.Elapsed);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (PromptTooLargeException ex)
                    {
                        // Every batch carries the same instructions: see BatchTranslator.
                        RaiseProgress(endIdx, segments.Count,
                            $"✗ Stopped at batch {batchNum + 1}: {ex.Message}",
                            true, sw.Elapsed);
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log the batch error and continue to next batch
                        totalChecked += batchCount;
                        RaiseProgress(endIdx, segments.Count,
                            $"\u2717 Batch {batchNum + 1} failed: {ex.Message}",
                            true, sw.Elapsed);
                    }
                }
            }

            sw.Stop();

            Completed?.Invoke(this, new ProofreadCompletedEventArgs
            {
                TotalChecked = totalChecked,
                IssueCount = issueCount,
                OkCount = okCount,
                Elapsed = sw.Elapsed,
                Cancelled = cancellationToken.IsCancellationRequested
            });
        }

        /// <summary>
        /// The user prompt for the batch of <paramref name="segments"/> from
        /// <paramref name="startIdx"/> up to <paramref name="endIdx"/>. Internal so
        /// Preview prompt builds it through the same function the run does.
        /// Segments carry their absolute document numbers (Index + 1), so the model
        /// can cite them against the same numbers in # DOCUMENT CONTENT, and their
        /// tags as placeholders, the target's numbered as the source's
        /// (<see cref="SegmentTagHandler.ToProofreadText"/>), so a Corrected line
        /// comes back with them. <paramref name="targetOnly"/> leaves the
        /// source out (<see cref="ProofreadingPrompt.BuildTargetOnlySystemPrompt"/>).
        /// </summary>
        internal static string UserPromptFor(IList<BatchSegment> segments, int startIdx, int endIdx,
            bool targetOnly = false)
        {
            var promptSegments = new List<(int number, string source, string target)>(endIdx - startIdx);
            for (int i = startIdx; i < endIdx; i++)
            {
                promptSegments.Add((
                    number: segments[i].Index + 1, // absolute document position, 1-based
                    source: segments[i].SourceText,
                    target: segments[i].ExistingTarget
                ));
            }
            return ProofreadingPrompt.BuildBatchUserPrompt(promptSegments, targetOnly);
        }

        private void RaiseProgress(int current, int total, string message,
            bool isError, TimeSpan elapsed)
        {
            Progress?.Invoke(this, new BatchProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message,
                IsError = isError,
                Elapsed = elapsed
            });
        }
    }
}
