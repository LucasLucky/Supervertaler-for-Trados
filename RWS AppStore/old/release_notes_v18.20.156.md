# RWS App Store Manager - v18.20.156

Two builds ship from this one release (identical feature set, distinct
version numbers so the App Store never sees a collision):

| Build | Version number | Min studio | Max studio | Checksum (SHA-256) |
|-------|----------------|------------|------------|--------------------|
| Studio 2024 | `18.20.156.0` | `18.0` | `18.9` | `eb5a7c6970280b51c9f40b31636b153c60878118eb402564884d1c2b3641f26f` |
| Studio 2026 | `19.20.156.0` | `19.0` | `19.0.9` | `c147222216a2cad340d4736ad1988f29226b795da5cf2dd077ff770ee1ac5a16` |

This release covers everything since **v18.20.134**, the last version actually
published for Studio 2024 — v18.20.144.0 was submitted but never cleared
review, so the Studio 2024 changelog below restates 135–144 as well as the
new work through 156. Studio 2026 users who already received 144 will see
some of this as a repeat; that's expected.

---

## Changelog

### [18.20.156 / 19.20.156] – 2026-08-02

#### Changed (Updates · "Not Now" now quietens updates for a week)

- **The update dialog's "Skip This Version" button is now "Not Now", and it silences update prompts for seven days rather than for one version.** Skipping a version only ever silenced the exact build named in the dialog, so the next release asked again — fine at one release a week, but Supervertaler is moving to submitting each meaningful fix to the App Store as it lands, which would have turned a per-version skip into a near-daily dialog you could never quiet. A time window decouples the prompt from the release rate: the more often updates ship, the *less* often you are interrupted. Settings that already recorded a skipped version keep working.

#### Fixed (Updates · builds from before the July renumbering were never offered an update)

- **If you are still on a version starting with "4", the plugin has been telling you that you are up to date when you are not.** On 2 July the numbering changed so that the major version identifies the Trados generation (18.x = Studio 2024, 19.x = Studio 2026); before that a single 4.x sequence covered both. The update check only considers versions whose major matches the running build, so once the App Store stopped listing 4.x there was nothing for those installs to match — and they were quietly told there was no update, every time, indefinitely. Legacy builds are now matched against the generation of the Studio actually running, so they are offered the current version. (Found while helping a user who had installed in June, never seen an update notice, and was consequently missing months of fixes.)

#### Changed (Distribution · the App Store is now the only channel)

- **The plugin is published through the [RWS App Store](https://appstore.rws.com/plugin/432) only.** GitHub releases keep the changelog, the tags and the MCP server files, but no longer carry the plugin. Three reasons, all of them things users actually hit: App Store builds are RWS-signed, so Studio stops asking you to confirm an "unsigned plug-in" at **every** start (a warning that is not an error, but reads like one); the plugin's update check reads the App Store catalogue, so anyone who installed from GitHub was running a build newer than the catalogue and was therefore never told about updates again; and an archive of old builds is a liability, since builds predating the trial anchor could be used to restart the trial indefinitely. All historical plugin downloads have been removed from GitHub.
- If you need a fix before it clears App Store review, email support@supervertaler.com and the build can be sent directly.

### [18.20.155 / 19.20.155] – 2026-08-01

#### Added (SuperSearch · your termbases are now searchable too)

- **SuperSearch now searches terminology alongside files and TMs.** "Where does this phrase appear?" and "what have I called this term?" are the same question at different granularities, and answering them in two different panels meant searching twice. All three kinds of termbase are covered: your **Supervertaler** termbases, the project's **MultiTerm** (`.sdltb`) termbases, and Trados 2026's **`.ttb`** termbases – through the same reader the rest of the plugin uses, so nothing new has to be configured.
- **The scope dropdown is now one entry per source**: **Everything** · **Project files** · **TMs** · **Termbases**. "Everything" replaces the old "Files + TMs" and now includes terminology; "TMs" is the old "TMs only". A scope you had already chosen is carried over, not reset.
- Search options behave identically in every scope – case sensitivity, regular expressions and whole-word matching all run through the same matcher the file and TM searches use, as does the source + target box combination.
- **Terminology comes from the index TermLens already holds in memory**, rather than a fresh read of the database — so a termbase search is effectively instant instead of taking tens of seconds on a large termbase collection, with no second copy of your terminology in memory. The database is read only as a fallback, when TermLens has not finished its initial load yet.
- **Only the termbases you have switched on are searched** – Supervertaler termbases with their **Read** tick set, and MultiTerm/`.ttb` termbases enabled in Trados Project Settings. The Read column is your statement of which terminology is in play for a job; searching the rest would contradict it and make every search pay for termbases you deliberately turned off.
- Termbases are discovered when the project opens, so the **TBs** button — beside **Files** and **TMs** — is populated before your first search. It works like the other two: click it to include or exclude individual termbases.
- Termbase hits show the **termbase name in green** (echoing TermLens's MultiTerm chips) and the termbase **kind** in the Status column – `Supervertaler`, `MultiTerm` or `TTB`. Navigate and Replace don't apply to a term (it isn't a document location) and say so.
- The results grid's first column is now headed **Found in** rather than *File/TM*, since it can hold a file, a TM or a termbase name.
- Search terms are matched **in the project's direction**: a termbase declared the other way round (an EN→NL termbase in an NL→EN project) is oriented before matching, so the **Src** box always means "the language you translate from" rather than "whichever column that termbase happens to call source" — the same treatment TermLens gives terminology.

#### Fixed (SuperSearch · button labels clipped at some display scalings)

- Buttons in the SuperSearch bar now grow to fit their labels instead of using fixed widths, which had truncated **Stop** to "Sto" at some font/DPI combinations.

### [18.20.154 / 19.20.154] – 2026-08-01

#### Fixed (Batch translate & proofread · locked segments are now left alone)

- **Batch translation sent locked segments to the AI.** Locked segments typically have empty targets, so the "empty segments" scope picked them up first: a batch run would jump the editor to locked content at the top of the file and pay to translate exactly the text someone locked so it would be left alone – instead of starting at the first genuinely open segment. Verified by a user against the batch backup TMX. Locked segments are now excluded from every batch-translate scope, from batch proofreading (worse there: a locked segment has a target, so the proofreader could *rewrite* protected content), and from the segment counters above the Translate button, so the numbers match what a run will actually process. (Reported by a user.)

#### Added (Batch translate & chat · custom providers in the model menus)

- **Custom OpenAI-compatible profiles now appear in the provider menus.** The model selector at the bottom of Batch Translate (and the chat status bar) listed every built-in provider's models but silently omitted the user-defined custom endpoints, so anyone comparing institutional gateways had to open Settings for every switch. Both menus now end with a "Custom (OpenAI-compatible)" submenu listing each profile with its model, active profile ticked – switching is one click, exactly like the built-ins. (Requested by a user.)

### [18.20.153 / 19.20.153] – 2026-08-01

All of the below came out of one production incident: an AI adding a term pair through the MCP server wrote it reversed into two termbases of opposite directions, and the tools reported success throughout.

#### Fixed (Supervertaler MCP Server · add_term wrote reversed entries and reported success)

- **The root cause was a contract gap, not broken direction logic.** The per-termbase orientation code was doing its job – but it rests on the assumption that `source` is the term in the *project's* source language, and nothing said so or checked it. The AI passed the pair the other way round; the orientation logic then faithfully produced a wrong entry in *each* termbase, one "aligned", one "swapped", both reversed. No language detection can catch this in translation work – term pairs are routinely identical across languages (radar, transponder) – so the fix is to make orientation explicit rather than guessed.
- **`add_term` now takes `sourceLang`/`targetLang`.** When supplied, each termbase stores the pair according to its own declared direction – one call is correct for an en→nl and an nl→en termbase simultaneously. Without them, the project-direction assumption still applies but is now stated loudly in the tool contract, and a termbase whose languages cannot be related to the project's **refuses instead of writing silently**: no document open, or an unrelated language pair, is an error asking for explicit languages. A wrong silent write is far worse than a refusal.
- **The response now proves what happened.** Instead of a bare list of termbase names, every targeted termbase reports back: `added` (with *exactly* what was stored – both terms in stored order, the termbase's languages, and whether the pair was reoriented), `duplicate`, or an error with the reason. Success can be verified, not trusted.

#### Added (Supervertaler MCP Server · add_term targeting and full fields)

- **`termbases` parameter** – restrict the write to named termbases (or numeric ids) instead of fanning out to every Write-enabled one. Fan-out was the direct reason one wrong call corrupted two termbases. The default remains all Write-enabled termbases, now itemised in the response; unknown or read-only names are reported per entry without blocking the rest. Duplicate detection was already per termbase and stays that way.
- **`definition`, `domain` and `notes`** can now be supplied – the storage always existed and `lookup_term` already returned the fields; they were simply unreachable from the MCP side, so all context had to be typed in by hand afterwards.

#### Fixed (Supervertaler MCP Server · lookup_term was blind to half the database)

- **Exact lookup matched the source column only**, despite claiming "source or target". A query in the project's target language found nothing unless an entry stored that text in its source column – which is precisely what reversed entries do, so during the incident the tool surfaced *only* the corrupted entries and made them look normal, while hiding the correct ones. Worse, any exact hit suppressed the substring fallback (which does search both columns), so each query returned exactly one misleading termbase. Exact matching now covers source and target terms alike.
- **Hits now report their evidence.** Each hit carries `matchedField` ("source"/"target"/"both") plus the entry's stored language pair, and the contract states plainly that results are returned exactly as stored, never reoriented – making `lookup_term` usable for verifying what `add_term` wrote. During the incident that verification was structurally impossible, which is how a reversed write passed its own check.

### [18.20.152 / 19.20.152] – 2026-08-01

#### Added (TermLens · target selections highlight their term chips)

- **Selecting text in the target segment now lights up the term chips whose translation the selection covers.** The counterpart of 20.151's source-selection tracking, with one deliberate difference: a source selection highlights a continuous run of words, while a target selection highlights only term chips. That is not a shortcut – there is no word-alignment data between source and target in the editor, so mapping arbitrary target text back onto source words would be guesswork, and on heavily reordered language pairs it would guess wrong often. What TermLens does know is every chip's translation, abbreviation and target synonyms, so those are matched against your selection instead: select *"transverse ship axis (roll) and/or longitudinal ship axis"* and the chips for *dwarsscheepsas* and *langsscheepsas* light up. Partial words work too (*"radiating elem"* finds *radiating elements*). Matching is textual: if your target wording departs from every rendering the termbase knows, that chip stays unlit. Whichever side you selected last drives the highlight, so the panel always reflects exactly one selection.

### [18.20.151 / 19.20.151] – 2026-08-01

#### Added (TermLens · your editor selection is now mirrored in the panel)

- **Selecting text in the source segment now highlights the corresponding words in TermLens.** On a long segment – a patent claim running to a dozen lines – the panel shows the whole segment's terms, and finding the part you are actually reading meant scanning the entire flow. Now the words covered by your editor selection carry a soft yellow band, matched and unmatched alike, so your eye lands straight on the right region and the term chips around it. The highlight follows the selection live, clears when the selection does, and when the same phrase occurs more than once in a segment the occurrence nearest your cursor is the one that lights up. Selections spanning an inline tag simply show no highlight rather than a wrong one.

#### Fixed (Add term entry dialog · generic title-bar icon)

- **The Add term entry dialog showed the generic WinForms icon instead of the Supervertaler logo.** The dialog is one class with three entry paths – add, edit and multi-termbase edit – and only the edit path set the icon. All three now share it.

### [18.20.150 / 19.20.150] – 2026-08-01

#### Fixed (TermLens & TermPicker · Escape now dismisses both pop-ups)

- **Escape now closes the floating TermLens popup and the Alt+P TermPicker.** The popup opens on a Ctrl tap and closed on a second tap, but Escape – the key everyone tries first – did nothing; the TermPicker window equally ignored it. Two causes, one deep: the TermLens popup deliberately never takes keyboard focus (so your typing stays in the editor), and – measured, not assumed – Studio's input pipeline consumes dialog-navigation keys before they reach the places WinForms normally hands them to a plugin. Escape is therefore intercepted with a low-level keyboard hook inside Studio's own process, which dismisses whichever Supervertaler surface is open (TermLens popup, TermPicker window, or the docked TermPicker pane – which hands focus back to the editor) and swallows the keypress so Studio does not also act on it. When nothing of ours is open – or another application is in the foreground – Escape passes through completely untouched.
- Worth knowing if Escape still seems dead after updating: on the machine where this was diagnosed, a background application's global keyboard hook was swallowing Escape system-wide – it did nothing in Notepad or the Start menu either, and no application could see it. Screen-capture tools, clipboard managers, dictation software and macro tools all install such hooks. If Escape does nothing anywhere, the problem is outside Supervertaler; closing those tools one at a time will find the culprit.

### [18.20.149 / 19.20.149] – 2026-07-31

#### Fixed (Translate active segment · single segments now get the same context as a batch)

- **Translating a single segment (Alt+T, or right-click → Translate active segment) produced noticeably weaker translations than Batch Translate – and the difference was real, not an impression.** Both use the same provider, model, prompt and termbase configuration, but the batch pipeline also hands the AI the document context (the surrounding source text, up to your configured limit) and your SuperMemory bank context. The single-segment path passed neither, so the model translated one isolated sentence with no register, no disambiguation and nothing to stay consistent with. Single-segment translation now sends the same context blocks a batch run does, honouring the same *Include document context* setting and the same 32-bit memory limits. If you translate segment by segment as a way into Supervertaler, the quality should now match what Batch Translate gives you. (Reported by a user.)

### [18.20.148 / 19.20.148] – 2026-07-30

All of the below came out of one real job: a 2,889-segment manual translated end to end through the MCP server. None of it was found in testing.

#### Added (Supervertaler MCP Server · compare the whole document against your TM)

- **`compare_document_to_tm` reports every segment translated differently from what the TM already holds for the same source.** Concordance search answers "was this phrase translated before?" one query at a time, for phrases you already suspect; it cannot answer "across this file, where have I departed from the client's reference TM?", because that is a join over every segment rather than a lookup. On the job that prompted this, a term coined in good faith already had an established rendering in the client's own TM, and no amount of searching would have found it — you only look up what you already doubt. Runs against file-based `.sdltm` and GroupShare TMs alike, through the Trados API rather than by reading the file format directly.
- The comparison happens **inside the plugin**, so only the deviations travel to the assistant, never the TM. Sending a whole TM across for the model to diff would be enormously expensive and would fall apart on a large one — the reference TM in that report held 1,490 units and a master TM is far bigger. Ordinary spacing differences are ignored; non-breaking spaces are not, so a target that quietly lost one still shows up.
- Only finished segments are checked by default, and only sources that match the TM verbatim — so a clean result means nothing contradicts the TM, not that the whole document agrees with it. The response says so explicitly, and says that a difference is not automatically an error: a deliberate improvement is indistinguishable from a mistake here, so the assistant is told to present the list for review rather than align anything itself.

#### Fixed (TermLens · terms with "(s)" were invisible, and Alt+Down mangled them)

- **A term written with the optional-plural convention – `verkoper(s)`, `party(ies)` – never matched anything in TermLens.** The tokeniser's character class read `%-/`, which looks like three literal characters but is a *range* covering U+0025 to U+002F, so it also matched `(`, `)`, `*`, `+` and `&`. `kandidaat-koper(s)` therefore tokenised as one single word, which no termbase entry could ever equal, and the panel simply reported no matches. Terms are now split at brackets as they always should have been, so an entry for `kandidaat-koper` highlights inside `kandidaat-koper(s)`. Percentages, `km/h`, `well-known`, `R&D`, `don't`, `C++`, `1.234,56`, `H₂O` and `m²` all tokenise exactly as before. (Reported by a user.)
- **Alt+Down on a selection ending in a bracket lost the closing bracket**, offering `kandidaat-verkoper(s` for the new entry — visibly wrong, and wrong in a way that was easy to save by accident. Balanced bracket groups now survive intact, leading or trailing, so `kandidaat-verkoper(s)` and `(her)certificering` are saved exactly as selected — the entry records what you chose, and TermLens indexes a bracket-stripped alias alongside it so the stored form still matches the `kandidaat-verkoper` token the tokeniser produces. Both spellings resolve to the same entry, and an alias merges with an existing base-form entry rather than being shadowed by it. Stray unbalanced edge punctuation (`koper)`, `verkoper,`) is still trimmed. (Reported by a user, twice — the first shipped fix reduced selections to the base term, which the same user demonstrated was the wrong call.)
- **Existing entries are not repaired automatically.** An entry saved under the old behaviour may still carry an unbalanced bracket (`… at Work (Cpbw`, `re)certification`) — mechanical repair is possible but some cases need a human decision about what was intended, so nothing in your termbase is rewritten behind your back. Ask the AI to list entries with unbalanced brackets if you want to review yours.

#### Fixed (Supervertaler MCP Server · find & replace quietly unconfirmed finished work)

- **A single find & replace demoted every segment it touched to Draft**, with no way to opt out. Editing a segment's content makes Studio reset its confirmation status – correct while you are still translating, wrong when you are running a consistency sweep over a file that is already finished. On a fully translated document the replacement worked and the file silently became unfinished; you only noticed if you thought to re-check the statuses afterwards. Each changed segment now keeps the status it had. The AI can still ask for a specific status where that is what you want, and the response reports which of the two happened. (Reported by a user.)

#### Added (Supervertaler MCP Server · non-breaking spaces you can actually see)

- **A new `check_nbsp` QA check** lists translated segments that came out with fewer non-breaking spaces than their source. Non-breaking spaces are invisible in Studio, in the AI's view of your segments and in every report, so a lost one normally surfaces only when the client rejects the file – which matters if your style guide wants one between a value and its unit (230 V, 3,5 mm, 50 %) or before a figure reference.
- **The AI can now write one, as `&nbsp;`.** A non-breaking space placed directly into a tool call reaches Trados only *sometimes*: depending on the AI client and the individual call it either arrives intact or turns into an ordinary space along the way, and the write reports success either way – so nothing distinguishes the two. Escape codes are no safer, because the client decodes them into the character first, and the character is what gets flattened. Intermittent is worse than broken here: it survives testing and then fails on the job. `update_segments` and `find_and_replace` therefore take a `decodeEntities` option, which lets the AI write the HTML entity `&nbsp;` (or any `&#NNN;` code point) and have Supervertaler convert it at the Trados end; plain ASCII travels intact, so nothing en route can mangle it. It covers both sides of find & replace, so *"put a non-breaking space between every value and its unit"* fixes a whole document in one pass – and because find & replace now preserves confirmation status, a finished file stays finished. Opt-in by design, so a document that genuinely contains the text `&nbsp;` is never silently rewritten. Supervertaler itself was never the culprit: it stores and returns the character faithfully, which is exactly why the loss was so hard to spot.

#### Fixed (Supervertaler MCP Server · verification results that looked current but weren't)

- **`run_verification` reads the last *saved* state of your files, and its findings gave no sign of it.** That is documented behaviour, but the response came back as a full, confident findings list, so edits the AI had just applied were invisible to it – in one case reporting 17 segments as still untranslated when they had all been translated moments earlier. The response now carries an explicit `stale` flag whenever there are unsaved AI edits, and tells the AI to save and re-run rather than report anything. Nothing is saved automatically: that stays your decision.

#### Fixed (Supervertaler MCP Server · large write batches could lose their confirmation)

- **Batches above roughly 45 segment updates outran the connection timeout.** The write itself went through, but the confirmation never came back, leaving the AI unable to tell success from failure – and a retry would apply the same edit twice. The per-call limit drops from 200 to 40, and the timeout on the MCP server side is raised well beyond any legitimate call, so both ends of the problem are closed.

#### Added (Supervertaler MCP Server · a warning when no termbase is switched on)

- **`get_active_project` now warns when the open project has no read-enabled termbase.** Termbases are activated per project, so a project with all of them switched off is indistinguishable over MCP from one with no terminology attached: lookups simply return nothing, and nothing says why. A whole job was translated that way before anyone noticed. `list_resources` carries the same warning alongside its `readEnabled` flags.

### [18.20.147 / 19.20.147] – 2026-07-29

#### Fixed (Clipboard Mode · multi-paragraph translations were cut short)

- **Pasting back a segment whose translation runs to more than one paragraph kept only the first one**, silently discarding the rest. If your source segment contains blank lines – a fault description followed by the steps to fix it, say – everything after the first blank line was lost on re-import. The response parser treated a blank line as the end of the translation; it now keeps reading to the end of the segment block, which was already marked by the next `Segment N` header. (Reported by a user.)
- **The same parser also cut a translation short at any paragraph beginning with a word and a colon** – `Note: …` in English, or `注意：…` in Chinese and Japanese. This was never reported separately, but anyone translating into CJK was especially likely to hit it. Both stopping rules are gone; the only thing that now ends a translation is the source-language label, for the models that put the pair the other way round.

#### Added (Supervertaler MCP Server · your memory bank, from any AI client)

- **Three read-only tools put SuperMemory on the MCP server**, so Claude Desktop – or any MCP client – can consult your memory bank while you translate, whatever CAT tool you have open. `get_supermemory_context` loads the bank for the current project and cites the articles it drew from; `search_supermemory` searches the active bank by keyword; `list_supermemory_banks` shows which banks exist and which is active. Nothing is written back.
- **The tools reach existing installs without reinstalling the MCP extension** – the exe reads its tool list from the plugin, so a plugin update is enough.

#### Fixed (SuperMemory · unverified notes no longer overrule the AI)

- **A note you had flagged as low-confidence, or that was never finished, carried the same authority as a verified one.** The prompt told the AI that knowledge-base decisions take priority, full stop – so a half-written Quick Add note could override a model that had it right. Low-confidence, draft and stub articles are now marked *unverified* and explicitly presented as a hint rather than an instruction, with the AI told to prefer its own judgement where the two disagree. Notes with no confidence set are unchanged.

#### Fixed (Dialogs · long text no longer clipped)

- **The in-app survey cut off longer questions mid-sentence**, and could overlap its own controls at some display-scaling settings. Both dialogs now size themselves to their text instead of using fixed positions, so they render correctly whatever your resolution, DPI scaling or system font size.
- **A one-off notice could reappear on every launch.** Several startup tasks each saved the whole settings file, so whichever finished last silently discarded what the others had written. They now re-read immediately before saving.

### [18.20.146 / 19.20.146] – 2026-07-28

#### Fixed (AI Assistant · GPT-5.6 failed instantly in chat)

- **Any GPT-5.6 model (Sol, Terra, Luna) returned an immediate error in the Supervertaler Assistant chat**: *"Function tools with reasoning_effort are not supported for gpt-5.6-sol in /v1/chat/completions"*. The chat gives the model tools so it can look things up in your project (projects, statistics, TMs, termbases) – and OpenAI does not allow that combination with reasoning on this endpoint, applying a reasoning setting of its own that the request never asked for. The chat request now opts out of reasoning explicitly, so GPT-5.6 works there again.
- **This covered everything that runs through the Assistant chat** – your own messages, **AutoPrompt**, and QuickLauncher prompts sent to the Assistant – since they all submit through the same chat. **Batch Translate and Batch Proofread were never affected**: they send no tools, so GPT-5.6 keeps its full reasoning exactly where it matters most for translation quality. GPT-5.5 and earlier are unchanged throughout. (Reported by a user.)

### [18.20.145 / 19.20.145] – 2026-07-27

#### Added (Supervertaler MCP Server · the AI can remove Trados comments too)

- **New `delete_comment` tool** rounds out comment handling (read, add, edit – and now remove). It's addressed exactly like `update_comment`: call `get_comments`, then pass the segment id and the comment's index, or `all=true` to clear every comment on a segment. It removes the **whole** comment, version history included – Studio's per-version *Delete version* surgery stays in the editor, where it belongs. Like the other destructive tools, the AI is told to act only on your clear request or confirmation and to say which comment it removed; a comment marker left empty is unwrapped so no dangling annotation remains on the segment, and the change is part of the document's unsaved edits until you (or `save_document`) save.

### [18.20.144 / 19.20.144] – 2026-07-27

#### Added (AI models · the GPT-5.6 family)

- **GPT-5.6 Sol, Terra and Luna are now selectable** (Settings → AI Settings). OpenAI released this three-tier family on 9 July 2026, all with a ~1M-token context window:
  - **Sol** – the flagship, for complex translation and AutoPrompt. **$5/$30 per million tokens: the same price as GPT-5.5, which it supersedes**, so there is no reason to stay on 5.5 for quality work.
  - **Terra** – GPT-5.5-class quality at **$2.50/$15**, half the price. A strong everyday default.
  - **Luna** – **$1/$6**, for high-volume batch work.
- Pricing for all three is in the shared pricing list, so cost estimates and usage reports handle them out of the box. GPT-5.5 remains available and its prices stay listed, so existing projects and past usage logs still resolve.

### [18.20.143 / 19.20.143] – 2026-07-27

#### Fixed (AI · the timeout fix now covers every GPT-5.x route)

- **GPT-5.5 via OpenRouter** had the same problem as the direct OpenAI route and is now recognised as a reasoning model too.
- **Any GPT-5.x model ID** – including one you type in yourself as a custom model, and the GPT-5.6 family – now gets the long timeout automatically, instead of only the older o-series being recognised.

### [18.20.142 / 19.20.142] – 2026-07-27

#### Fixed (AI · AutoPrompt timed out on GPT-5.5 and other slow OpenAI models)

- **AutoPrompt failed with "The request timed out." on GPT-5.5** (reported by a user; GPT-5.4 Mini worked fine on the same job). AutoPrompt asks the model for a large amount of output, and the OpenAI request paths allowed a flat two minutes for it regardless of how much was requested – where the Claude paths have always allowed ten minutes for large generations. All OpenAI paths now scale the same way, based on the size of the request rather than on a list of known-slow models, so this keeps working for models released after this build.
- **GPT-5.5 is now recognised as a reasoning model**, so every request to it gets the longer timeout, not just large ones.
- **AI request timeouts are now recorded in the diagnostic log**, and the error message suggests trying a faster model or sending less context. Previously a timeout left no trace in the log at all, which made it impossible to diagnose from a bug report.

### [18.20.141 / 19.20.141] – 2026-07-27

#### Fixed (Batch Operations · Proofread prompt put verdicts on the wrong segments) — [#50](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/50)

- **The clipboard Proofread prompt numbered its review list from 1 instead of using the real segment numbers**, so as soon as any segment was skipped – a tag-only segment, for instance – every number after it was wrong, and the AI's verdicts were reported against the wrong segments. Nothing looked amiss: the output was well-formed and only a manual comparison revealed the drift. Found on a real 949-segment job where three tag-only segments pushed 826 of 946 verdicts three segments out of place. The batch now uses the same `[SEGMENT NNNN]` document numbers as the document-context block (and as the API path, which was never affected), and states that the numbers are deliberately non-contiguous.
- **The prompt also specified its output format twice, in two different ways** (`[SEGMENT 0002] ISSUE` with `Issue:`/`Evidence:`/`Suggestion:` versus `Segment 2: ISSUE` with `Problem:`/`Suggestion:`). A model following the second one dropped the evidence citations the first one asks for. The format is now defined once.

### [18.20.140 / 19.20.140] – 2026-07-27

#### Fixed (TermPicker)

- **Escape now closes the term-details window** – in the docked pane and in the Alt+P popup alike. (Windows treats Escape as a dialog key, so it never reached the list; it is handled a level up now.) In the popup, a second Escape closes the picker itself. The details window also closes when you move to another row, so it can no longer describe the previous term.
- **The top row no longer flashes when you press Alt+P.** The list was hiding its selection while the editor had focus and redrawing it on arrival; the selected row now stays visibly selected (grey when unfocused, blue when focused). The list is also double-buffered, so rebuilding it on each segment change doesn't flicker.

### [18.20.139 / 19.20.139] – 2026-07-27

#### Fixed (TermPicker pane)

- **The pane no longer starts empty.** If you kept TermPicker visible with the TermLens panel collapsed to a tab, the pane stayed blank until you clicked that tab: Studio only starts a panel when it is first shown, so TermLens wasn't yet following the document that the picker takes its matches from. The pane now starts TermLens itself, so it is populated the moment you open it.
- **You can now see which terms have details.** Rows whose term carries a definition, domain, notes or a URL are marked with an amber dot – the same signal the TermLens chips give – so it's clear when pressing `I` will show you something.
- **Escape closes the details popup** (previously it stayed on screen). In the Alt+P popup, a second Escape then closes the picker itself.
- **The right-click menu is back**: Edit Term, Mark as Non-Translatable and Delete Term, matching the TermLens chips. It acts on the row you right-click, and is disabled for MultiTerm entries, which are read-only.

### [18.20.138 / 19.20.138] – 2026-07-27

#### Added (TermPicker · press I for term details)

- **Pressing `I` on a row in TermPicker shows the term's metadata** – the same popup, with the same content, as hovering a TermLens chip: forbidden / MultiTerm / non-translatable tags, and for every entry its synonyms, definition, domain, notes and URL. Press `I` again to dismiss it. Works in both the Alt+P popup and the dockable pane, and matches the `I` key that the TermLens popup has always had. TermPicker's keyboard set is now: arrows to navigate, ←/→ to collapse/expand synonyms, a term number to jump, **I** for details, **E** to edit, Enter to insert (and Esc to close the popup).

### [18.20.137 / 19.20.137] – 2026-07-27

#### Changed (TermPicker pane · polish from first use)

- **The pane now opens pinned**, i.e. permanently visible. Previously it arrived auto-hidden, sliding in and straight back out again, which looked like a glitch. Studio still remembers wherever you drag it afterwards.
- **Alt+P now moves focus into the pane when it is open**, instead of covering it with the popup: from there arrows navigate, ←/→ collapse/expand synonyms, a term number jumps to it, Enter inserts. With no pane in your layout, Alt+P opens the popup exactly as before.
- **Escape closes the TermPicker popup** (the list was swallowing the key).
- **Pressing E on a row opens the term editor**, matching the TermLens popup's key – in both the pane and the popup. MultiTerm entries are skipped, as those termbases are read-only.

### [18.20.136 / 19.20.136] – 2026-07-27

#### Added (TermPicker · now available as a dockable pane)

- **TermPicker can now be docked like TermLens**, for anyone who prefers a flat, sortable list as their permanent terminology display rather than TermLens's in-context chips. Open it from Studio's **View** tab (it is not pinned by default, so your existing layout doesn't change when you update). The pane updates on every segment change, in step with the TermLens panel, and inserting from it behaves exactly like the popup and the chips – same capitalisation adaptation, same keyboard grammar (arrows to navigate, Right/Left to expand/collapse synonyms, a term number to jump, Enter to insert).
- Both terminology views are now available in both placements: TermLens as a docked panel or at the cursor (tap **Ctrl**), TermPicker as a docked pane or at the cursor (**Alt+P**) – so you can choose the representation and the placement independently. **Alt+P still opens the popup** even when the pane is visible, mirroring how Ctrl-tap works alongside the docked TermLens panel.

### [18.20.135 / 19.20.135] – 2026-07-27

#### Changed (TermPicker · new shortcut, synonyms shown up front)

- **TermPicker now opens with Alt+P** (was Ctrl+Shift+P). Ctrl+Shift+P is also Trados Studio's own *View Target*, so it appeared twice in Studio's keyboard-shortcut list and looked like a conflict. Alt+P is free. Note that Studio keeps your existing binding across plugin updates – if you had it on Ctrl+Shift+P, clear that and set Alt+P under **File > Options > Keyboard Shortcuts > Supervertaler for Trados**.
- **TermPicker opens with every synonym group already expanded**, so a single Alt+P shows all alternative translations at a glance instead of hiding them behind collapsed markers. Left/Right still collapse and re-expand individual groups.
- The **About** dialog's shortcut list now includes Alt+P (TermPicker) and Ctrl+Alt+V (voice commands), and its entry for *Translate active segment* is corrected to **Alt+T** – it still showed the old Ctrl+T, which was replaced in 20.119 because it collides with Trados's *Apply Translation Result*.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases/tag/v18.20.156
