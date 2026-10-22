# Consistent repetitions in Batch Translate (Trados and memoQ)

**Status:** APPROVED by Michael on 22 Oct 2026, with all six decisions as recommended (below). Nothing built yet;
phase 1 (core) is next.
**Source:** Michael's handoff of 22 Oct 2026, "Consistent repetitions in Batch Translate" (goal, evidence, design v1,
match key, acceptance test), kept outside this public repository because its evidence names client jobs. This note
checks that design against the code of both products and says how to build it.
**Line numbers:** Trados as of `61d3727`, core as of `6620274`, memoQ as of `7154cd7`.

---

## In short

**The goal.** One run of Batch Translate (Trados) or Pre-translate (memoQ) never delivers two different translations
for the same source segment. It is automatic, on by default, and changes nothing that is sent to the AI.

**What is common.** One shared piece in core decides what counts as a repetition (the *match key*) and which
translation a group of repetitions gets. It also counts what happened for the summary. The two products differ only
in how they read segments and hand results back.

**Trados** can do almost everything the handoff asks:
- Batches already run one after another, in document order, and each result is written the moment its batch
  returns.
- So the change is one step in front of the write. A later repetition gets the group's translation instead of the
  AI's.
- The tags come out right with no extra work. Trados rebuilds every target from that segment's *own* source tags,
  by position.

**memoQ** can do the core of it, but not all of it:
- memoQ writes the results itself and runs Pre-translate as several requests at once. So the group's translation is
  the *first one to come back*, not necessarily the first in the document. Every later repetition in the run gets that
  one.
- memoQ never shows the add-in the text already in a target, and does not send rows it filled from its own TM.
  Copying a TM-filled first occurrence (acceptance case 5) and listing conflicting existing translations (case 6)
  therefore cannot be done in memoQ.

**Decisions for Michael** are in the next section. Each one has a recommendation.

---

## Decisions for Michael

**Decided 22 Oct 2026: all six as recommended.** In short:
1. Copies are labelled "interactive" in v1, plus one live probe of "auto-propagated".
2. In memoQ, the first translation to come back is the one every repetition gets.
3. A failed repetition is filled with the group's translation.
4. The backup TMX keeps the AI's own version in a `<note>`.
5. `find_inconsistencies` uses the same key.
6. Batch Proofread is phase 3.

The reasoning behind each is kept below.

1. **What a copy is labelled as in Trados.**
   - Since 20 Oct every write is labelled "interactive", your edit (`Core/SegmentOrigin.cs:15-17`, your choice, no
     setting).
   - The handoff asks for copies to be labelled "auto-propagated", so Studio's filters show them. Studio has that
     origin, but nothing is known yet about whether it keeps it when a plugin writes it.
   - *Recommendation:* keep "interactive" in v1 and run one live probe of "auto-propagated". Switch only if Studio
     keeps it and you want it.
2. **memoQ: the first translation to come back is the one every repetition gets.**
   - memoQ does not tell the add-in where a row sits in the document, and runs batches side by side, so "first in
     document order" is unknowable there.
   - *Recommendation:* accept first-to-return. It is consistent, which is the goal. Which of two equally valid
     wordings wins is then arbitrary.
3. **A repetition whose own translation failed gets the group's translation.**
   - This covers a missing number in the reply, a refused reply, or a batch error, when the group already has a
     translation.
   - It costs no extra request and leaves fewer empty segments.
   - *Recommendation:* yes, in both products.
4. **The batch backup TMX (Trados).**
   - The aligned translation is written, as the handoff says.
   - *Recommendation:* also keep the AI's own discarded version in that unit's `<note>`, so the backup still records
     what the model actually returned. It costs a few bytes.
5. **`find_inconsistencies` uses the same key as the plugin.** The tool currently folds non-breaking spaces into
   ordinary spaces and ignores tags (Trados `AiAssistantViewPart.cs:4369-4373`; memoQ `QaChecks.cs:149-162`).
   - With the plugin's key, two sentences that differ only in an NBSP are no longer grouped as repetitions.
   - *Recommendation:* use the same key, as the handoff says, so the post-run check agrees with the plugin.
6. **Batch Proofread (Trados) in v1, or straight after.**
   - Its results are applied by hand from the Reports tab, a different flow from Batch Translate (§3.8).
   - *Recommendation:* phase 3, after Batch Translate in both products has been in use.

---

## 1. What the code does today

### Trados (facts this design relies on)

- **One run covers one document only.** It covers the document open in the editor, including every file merged into
  it, and nothing else.
  - `CollectSegments` walks `_activeDocument.SegmentPairs` (or the filtered list) in order
    (`AiAssistantViewPart.cs:13652-13744`).
  - So "across all files in scope" means across the files of the open, possibly merged, document.
- **What a run skips.**
  - Locked segments, always (`:13682-13686`).
  - Segments whose source has no text (`:13701-13705`).
  - Non-empty targets under the "empty only" scopes (`:13707`).
  - Finalised segments under "All unfinished" (`:13710-13711`).
  - "All segments" overwrites whatever is there.
  - Skipped segments are not kept anywhere.
- **What is sent.**
  - The source as numbered `<tN>…</tN>` / `<tN/>`, numbered by position. The tag's own id and kind are ignored
    (`Core/SegmentTagHandler.cs:140-257`).
  - Soft returns (U+2028/U+2029) are turned into spaces before sending (`:13698-13699`).
  - Each `BatchSegment` carries its own `TagMap` and its `ISegmentPair` (`Core/BatchTranslator.cs:17-79`). It has
    no file id, status or lock flag.
- **Batches run strictly one after another.** One `for` loop with an `await` per batch (`BatchTranslator.cs:426-606`).
  There is no `Parallel`, `WhenAll` or semaphore in Batch Translate or Batch Proofread.
- **Results are written per batch, in document order.**
  - `SegmentTranslated` fires for each segment of a batch in list order (`:520-567`).
  - It is handled synchronously on the UI thread by `OnBatchSegmentTranslated` (`AiAssistantViewPart.cs:10650-10733`).
  - That calls `SegmentWriteCheck.ProcessChecked` → `ReconstructTarget(sp.Target, sp.Source, translation,
    e.TagMap)`.
  - The `<tN>` text is rebuilt from **that segment's own** `TagMap`.
- **Failures are counted, not written.**
  - A batch error, a missing number or a twice-refused reply leaves the segment empty and adds it to `notTranslated`
    (`BatchTranslator.cs:559-564, 595-605`).
  - "Retry segments left empty" (off by default) sends them again later (`:614-697`).
- **The backup TMX.**
  - It records `e.SourceText` (exactly what was sent) and `e.Translation` (the parsed reply), per written segment
    (`AiAssistantViewPart.cs:10718`; `Core/BatchTranslationBackup.cs`).
  - It is flushed every 10 units and at the end. No other serialiser is involved.
- **The handler is shared.** Alt+T (translate active segment) also uses `OnBatchSegmentTranslated`
  (`:14654-14655`), and SuperBench subscribes to `SegmentTranslated` (`Core/SuperBenchRunner.cs:105-117`).
- **There is no repetition handling anywhere** in Batch Translate, Proofread, the prompts or the writers.
  `find_inconsistencies` is the only place that groups repetitions (`AiAssistantViewPart.cs:4318-4477`).

### memoQ (facts this design relies on)

- **memoQ is an MT provider here; this is case (b) of the handoff.**
  - memoQ calls `ISession.TranslateCorrectSegment`, mostly the array overload with metadata
    (`SupervertalerSession.cs:57-64`).
  - memoQ writes what is returned. The add-in cannot touch any other row.
- **Pre-translate arrives in parallel.**
  - memoQ calls from `Parallel.ForEach` workers.
  - A 13-row Pre-translate came as two calls at the same moment, 7 + 6 rows (probe, 22 Oct).
  - Within one call, chunks run one after another (`Core/BatchTranslator.cs:214-271`).
- **There is no "run" in the SDK.**
  - Sessions have no shared id, and `Dispose` is never called meaningfully.
  - `SegmentIndex` is the position in the call, not in the document.
  - `Core/LookupOrigin.cs` tells a Pre-translate call from a grid lookup by memoQ's caller (0.1.23), but only when
    bridge mode is on (`SupervertalerSession.cs:73-76`).
- **The add-in never sees a target's existing text** (CLAUDE.md "What memoQ hands us"). Rows memoQ filled from its
  own TM are not sent at all. Whether locked rows are ever sent is unknown.
- **Tags.**
  - Sent as memoQ's segment XML, with formatting excluded (`Core/TagBridge.cs:28-32`).
  - A reply is rebuilt with `ConvertXML2Segment(text, requestingSegment.ITags)` (`:46-67`). The tag ids in a reply
    resolve against the *requesting* row's tag list.
- **Existing caches do not help.**
  - `TranslationCache` is keyed on a whole chunk's prompt (`BatchTranslator.cs:419-420`), so the same sentence in
    another chunk is a miss.
  - `StagedTranslations` serves Claude Desktop's staged pairs first (`:126-145`).
- **There is no batch backup TMX, and no Batch Proofread, in memoQ.**

---

## 2. The shared part (core)

Two small classes in core (Supervertaler-Plugin-Core, owned by the Trados session). Neither uses any memoQ or Trados
type. Both plugins compile the same sources, so both behave the same.

### 2.1 `RepetitionKey`: the match key

Built from a product-neutral token list, which each plugin produces from its own segment:

- **Text tokens:** the text exactly as in the source. Case is kept, and so are NBSP (U+00A0), narrow NBSP, tabs,
  line breaks, soft returns (U+2028/U+2029) and Unicode subscripts. Nothing is folded.
- **Tag tokens:** a *kind* and a *role* (open, close, standalone), never an id. Nesting is implied by the order of
  open and close tokens. Two segments share a key only if their tag token sequences are equal, which makes the tags
  map one-to-one by position.
- **The key** is the token sequence encoded as one string, with a separator that cannot occur in text (U+001F).

**Kind, per product** (to be confirmed in code before building; §10):
- **Trados.** The element type (tag pair, placeholder, locked content, other container) plus what the tag *is*.
  - That means the semantic name `BilingualTagNamer` already detects (`b`, `i`, `u`, `bi`; `Core/Export/BilingualTagNamer.cs:217-270`).
  - Otherwise, the tag's display text with any id attribute removed.
  - Bold and italic around the same words must not share a key, because `<tN>` alone cannot tell them apart.
- **memoQ.** The tag element name from the segment XML (`inline_tag`, `b`, …), without its `id`. `spec_char` counts
  as text, by its value.

**Built from the source itself, not from the text that was sent.** The Trados prompt turns soft returns into spaces
(`AiAssistantViewPart.cs:13698`), but the key must keep them apart (handoff: line breaks, soft returns).

### 2.2 `RepetitionRun`: the groups in one run

It holds one entry per key: the **group translation**, in the notation the product sends, plus the counts. Its
rules:

1. **A group's translation is fixed once, by the first of these to happen:**
   - (a) an existing target the run keeps, on the group's first occurrence that has one and is not overwritten
     (Trados only);
   - (b) the first translation the run *writes successfully* for any occurrence. In Trados that is in document order.
     In memoQ it is the first to come back.
2. **Every later write in the group uses the group translation instead of the AI's own.** That includes retries,
   re-asks, and (decision 3) occurrences whose own translation failed.
3. **A segment the run does not write is never changed.** That covers locked segments, segments outside the scope,
   and existing targets the run does not overwrite.
   - If such a segment's existing target differs from the group translation, the group is recorded as a
     **conflict**.
   - Conflicts are listed in the summary and nothing is changed (Trados only; memoQ cannot see existing targets).
4. **Counts for the summary:** segments written, repetitions aligned, failures filled from the group, conflicts.

**Thread-safety.**
- In Trados everything runs on one thread, as writes are marshalled to the UI thread.
- In memoQ, one lock guards "set the group translation if none yet, then read it back". That is a single
  `GetOrAdd`.
- Nothing is held across a model call.

### 2.3 Tags in the copied translation

- **Trados needs no remapping.**
  - The `<tN>` numbers are positional, and the writer rebuilds every target from the destination segment's own
    `TagMap` (`ReconstructTarget`).
  - So writing the group translation into a repetition puts that repetition's own tags, with their own ids, at the same
    positions. That is acceptance case 3, the same text with different tag ids.
  - The key guarantees the two tag sequences match.
- **memoQ: probably none either, but this must be verified.**
  - A reply is rebuilt against the requesting row's own tag list. If the ids in memoQ's segment XML are positional
    indices into that list, the group translation re-parses correctly against any repetition with the same key.
  - If they are not, core gets `RemapByPosition(tagged, fromTokens, toTokens)`, which rewrites the nth tag's id from
    the first occurrence's to the repetition's.
  - A harness test settles it (§8).

### 2.4 The summary line

`RepetitionRun.Summary()` gives the handoff's line, for example:

> 586 segments – 130 repetitions aligned to their first occurrence – 2 groups with conflicting existing translations (listed).

The conflicting groups follow, one line each, with their segment numbers.

---

## 3. Trados

### 3.1 Collect once, all of the scope

- `CollectSegments` already walks every pair in the scope. It records every pair it **skips**, as well as the
  ones it sends:
  - the key;
  - the order;
  - whether it is locked;
  - whether the run will write it;
  - its existing target in the source's `<tN>` numbering, via `SerializeAlignedTo` (`SegmentTagHandler.cs:297-389`,
    already used by Proofread).
- Pairs outside the scope (outside the filter, for example) are not recorded and never touched. One walk, no second
  pass.

### 3.2 Before a run starts

`RepetitionRun` is seeded from the collected list:
- groups with more than one member;
- the group translation from rule 1(a) where a first occurrence keeps an existing target. This is acceptance case 5,
  a TM-filled first occurrence;
- conflicts known at the start, which is case 6.

### 3.3 At each write

- In `OnBatchSegmentTranslated`, before `Write`, for a Batch Translate run only:
  - if the segment is in a group that already has a translation, write that instead of `e.Translation`;
  - otherwise write the AI's translation. After a successful write, it becomes the group translation.
- The backup TMX records what was written, plus the AI's version in a `<note>` (decision 4).
- Alt+T and SuperBench share this handler and event. They must not align, so the run object is passed in only by the
  Batch Translate entry point (`:10317`), not looked up from a field the other two share.

### 3.4 Failures in a batch (decision 3)

- After each batch, `BatchTranslator` reports the segments it could not translate. That needs a small event or
  callback; today they only go into `notTranslated`.
- Each one whose group has a translation is written with it, through the same path.
- This happens *after each batch*, as the handoff asks, so a cancelled run never leaves a group half done.

### 3.5 Cancel and crash

- Every write already uses the group translation at the moment it is made, so every segment written before a cancel
  or crash is consistent with its group.
- Nothing is written later "to fix things up", so there is no half-aligned state to leave behind.
- The run object lives only for the run. It goes with the handler's locals on success, failure and cancel alike.

### 3.6 The summary

The `RepetitionRun` summary is logged after the existing "Complete: N translated, M failed" line
(`Controls/BatchTranslateControl.cs:1415-1425`, `AiAssistantViewPart.cs:10757-10812`).

### 3.7 The setting

- `AiSettings.KeepRepetitionsConsistent`, default **on**.
- It follows the `ProofreadCommentLabel` pattern (`Settings/AiSettings.cs:114-115`), including a seed in the one
  existing `[OnDeserializing]` method (`:337-346`). A second such method throws and wipes every setting.
- There is a checkbox in the AI Settings panel (`Controls/AiSettingsPanel.cs`, `Check(...)` / `Span(...)`).

### 3.8 Batch Proofread (phase 3; decision 6)

- Proofread runs batches one after another too (`Core/BatchProofreader.cs:116-259`). A user applies the findings
  from the Reports tab (`AiAssistantViewPart.ProofreadApply.cs:62-155`).
- **The proposal.** Group findings by key **and** by identical existing target in `<tN>` numbering.
  - Within such a group, the first occurrence's finding stands for all of them, and the AI's findings on the others
    are dropped.
  - **Apply** on it applies the correction to every member that still reads as it did. The existing source and
    target re-check in `ApplyCorrection` (`:342-353`) makes that safe.
- Occurrences whose targets already differ are a consistency finding in their own right. They are not merged.

### 3.9 Found on the way (separate from this feature)

- `OnBatchSegmentTranslated` reads `_activeDocument` **at write time** (`:10664`).
  - If the user switches documents during a run, later writes go to the new document with pairs from the old one.
  - The run should capture its document at the start. Do that as part of this work, because the run object is
    created there anyway.
- The batch write handler does not re-check a segment's lock at write time (`:10650-10733`). That matters only if
  a segment is locked during a run.

---

## 4. memoQ

### 4.1 A run, without one in the SDK

- A **Pre-translate run** is continuous Pre-translate activity:
  - it starts with the first Pre-translate call when none is in flight;
  - it ends when no Pre-translate call has been in flight for 60 seconds.
- An in-flight count plus the time of the last completed call is all the state it needs. Two Pre-translates started
  within a minute of each other become one run, which is harmless, because consistency across them is no worse.
- **Pre-translate or grid lookup** is decided by `LookupOriginDetector.Current()`, read at session entry (as today).
  When the setting is on, it is read on every array call, not only in bridge mode.
  - Grid lookups neither use nor fill the run. The handoff is about Batch Translate. A suggestion for the row being
    edited stays the model's own.
  - `Unknown` counts as Pre-translate. The worst case, a renamed caller in a future memoQ, is a grid lookup that
    reuses a run's translation, which is still a correct translation.

### 4.2 Where it happens

- One place: `SupervertalerSession.TranslateMany`, after `BatchTranslator.TranslateAsync` returns, **and** after the
  session's own per-row fallback (`:139-143`).
- That single spot covers every path: chunks, retries, the one-row early return (`BatchTranslator.cs:206-212`), and
  the fallback.
- The step is `results = run.Align(segs, results)`.
- **The run lives in a static store keyed by language pair.** memoQ runs two engines per pair, and an
  `EngineContext` field would split the run between them (`EngineContext.cs:604-607`).
- **For each row:**
  - a row that is empty or copied from its source (`NothingToTranslate`) is left alone;
  - a successful result (model or staged) with no group translation yet becomes the group translation, stored as
    tagged text from `ToTaggedText(result.Translation)`;
  - any other row in a group with a translation gets `FromTaggedText(groupTagged, segs[i])`, and that includes a
    failed row (decision 3). Its `Info` reads "same translation as an earlier repetition in this run".
- Bridge mode (Claude Desktop) captures produce no result, so they neither fill nor use a group.

### 4.3 What memoQ cannot do (say so in the help page)

- **Case 5 cannot be done.** A first occurrence memoQ filled from its own TM is never sent, so its translation
  cannot be copied.
  - Whether memoQ's own Pre-translate already fills later repetitions from that TM hit needs a live check (§10).
- **Case 6 cannot be done.** Existing targets are never visible, so conflicts cannot be listed.
- **Locked rows.** If memoQ sends them, the add-in cannot tell them apart. If it does not, they are untouched.
  Needs a live check.

### 4.4 Summary and setting

- **Summary.**
  - The plugin log gets a "repetitions aligned" count per call, and a run summary line when the run ends.
  - The existing "rows: …" line (`BatchTranslator.cs:306-322`) gains a "reused" count. Otherwise aligned rows are
    counted as "by the model".
  - memoQ has nowhere to show a run summary on screen.
- **Setting.**
  - `SharedSettings.KeepRepetitionsConsistent`, a shared-file boolean that defaults to on through `BoolOr`. The model
    is `SendMemoryBank` (`Core/SharedSettings.cs:55, 251`).
  - It needs a checkbox in memoQ's options dialog and in the prompt editor's settings, both covered by the
    dialog-fit test.

---

## 5. `find_inconsistencies`

- Both products switch to `RepetitionKey` for grouping (decision 5).
- Targets keep their current comparison: tags stripped, whitespace collapsed. A difference in a target's NBSP is a
  separate check (`check_nbsp`).
- In memoQ the tool works on the live link's paragraphs (`MemoQBridge.cs:2222-2240`), not on grid rows, so it cannot
  confirm a Pre-translate row by row. Say so in its description.

---

## 6. Failure paths

| What happens | Trados | memoQ |
|---|---|---|
| The first occurrence's batch fails | The next occurrence written successfully sets the group; the failed one is filled from it after its batch (decision 3), or by a retry pass, with the same translation | The first success to come back sets the group |
| A write is declined by Studio | Counted as failed as today; it does not set the group | n/a |
| The user cancels | Everything written is consistent; unwritten segments stay as they were | memoQ stops calling; the run ends after 60 s idle |
| Plugin or process crash | Same as cancel: no deferred writes | The process is gone; so is the run |
| Two occurrences come back at once | Cannot happen (one thread) | One lock: the first `GetOrAdd` wins, the other gets its result |
| memoQ renames its callers | n/a | `Unknown` counts as Pre-translate (§4.1) |
| The setting is off | Exactly today's behaviour | Exactly today's behaviour |

---

## 7. Scale

**Designed for a 10,000-segment run.**
- **Keys:** built once per segment from the serialisation that is already done for sending.
- **Grouping:** one dictionary lookup per segment.
- **Memory:** about one short string and a small record per segment, so a few MB at 10,000.
- **Trados:** no second walk of the document. Recording skipped pairs adds a serialisation per skipped pair to the
  existing walk.
  - Measure it on a large document. If it shows, record skipped pairs only when the setting is on.
- **memoQ:** one stack walk per array call, about 1,000 for 10,000 rows in batches of ten, well under a second in all.
  - The run store is capped at 50,000 groups as a safeguard and cleared when the run ends, so a never-ending stream
    of Pre-translates cannot grow it without bound.

---

## 8. Tests

**Offline, core tests (console, `core/tests`):**
- `RepetitionKey`: acceptance cases 3 (tag ids differ → same key) and 4 (NBSP vs space → different keys). Also
  soft return vs space, `CH₄` vs `CH<t1>4</t1>`, bold vs italic around the same words, case, and nesting.
- `RepetitionRun`: rules 1-4 on a scripted run.
  - cases 1 and 2: within a file, and across files;
  - case 5: an existing target on the first occurrence;
  - case 6: conflicts listed, nothing changed;
  - case 7: locked, untouched;
  - a failed first occurrence;
  - two concurrent `GetOrAdd`s.

**Offline, Trados (`.dev` harness, against the built DLL):**
- the key built from real Studio segments with the same text and different tag ids;
- `ReconstructTarget` of one occurrence's `<tN>` translation into another's pair (case 3);
- the run object passed only by Batch Translate, not Alt+T or SuperBench.

**Offline, memoQ (`tools/*-test.ps1` through `run-harness.ps1`):**
- whether `ConvertXML2Segment` resolves ids positionally (§2.3), using two segments with the same structure and
  different tags;
- `Align` through the real session with stand-in callers for Pre-translate and the grid (as in
  `lookup-origin-test.ps1`).
  - The model calls go to a fake OpenAI-compatible server inside the harness that returns two different wordings, so
    alignment is visible with no key and no cost.
- the run ending after idle;
- grid lookups untouched.

**Live, Michael:**
- The 7-case test project in Studio and in memoQ.
- `find_inconsistencies` returns 0 groups for segments written in the run.
- **Nothing sent changed.** The prompt log or backup TMX of the same project with the setting on and off has
  identical `<tN>` sources and batch prompts.
- A real 586-segment, four-file project (table cells repeated six times, paragraphs repeated across two files) comes
  out identical.
- One probe of the "auto-propagated" origin (decision 1).
- In memoQ: whether TM-filled first occurrences get propagated by memoQ itself, and whether locked rows are sent.

---

## 9. Phases (one session each, each on its own branch)

1. **Core:** `RepetitionKey` and `RepetitionRun` with their tests. Offline only.
2. **Trados Batch Translate:**
   - collection, write substitution, failures filled, summary, backup note, setting;
   - capture the run's document at start (§3.9);
   - harness tests, then the live acceptance test.
3. **memoQ Pre-translate:** run store, `Align`, setting in both dialogs, harness with a fake server, live test.
4. **`find_inconsistencies` in both products, and Trados Batch Proofread** (§3.8).

---

## 10. To verify before building

- **Trados tag kind for the key.** Is a tag pair's display text free of ids, so that it can stand for "what the tag
  is"? Check `BilingualTagNamer` and `TagProperties` on a real IDML and DOCX segment.
- **memoQ.** Are segment-XML tag ids positional indices into `ITags` (§2.3)?
- **memoQ.** Does memoQ itself propagate a TM hit to later repetitions during Pre-translate? Does it ever send locked
  rows?
- **Trados.** Does Studio keep an "auto-propagated" origin set by a plugin after `ProcessSegmentPair`? Only if
  decision 1 asks for it.

## Not in v1

- Near-repeats (95-99% fuzzy).
  - A possible v2 is an "in-run memory": reference pairs added to later batch prompts. That changes what is sent, so
    it is a separate decision.
- Repetitions across separate runs, or across documents not open together in Trados.
- Removing repeats from batches, or marking them "context only". Both are rejected in the handoff.
