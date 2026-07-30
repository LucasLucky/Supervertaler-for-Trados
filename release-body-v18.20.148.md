Supervertaler for Trados **v18.20.148** (Studio 2024) / **v19.20.148** (Studio 2026) — unsigned builds are attached below. Covers 18.20.148.

## 📦 Installing from here (unsigned build – read first)

The plugins attached to this release are the **unsigned** builds. The version on the **RWS App Store is signed and notarised** – that's the recommended channel for most users. These downloads are for trying the latest fixes **before App Store approval** (which can take a few days, especially over a weekend).

**To install:**
1. Download the zip for your Trados version (table below).
2. **Extract it** – inside is a single `.sdlplugin` file.
3. Close Trados Studio, then double-click the `.sdlplugin` to run the Plugin Installer. **Do not rename the file** – Trados matches the filename against the plugin manifest.
4. Trados will warn that the plugin is **not signed**; that is expected for the direct build – click through to continue.

| Download | Trados version |
|---|---|
| `Supervertaler-for-Trados-Studio-2024.zip` | Trados Studio 2024 |
| `Supervertaler-for-Trados-Studio-2026.zip` | Trados Studio 2026 |
| `Supervertaler-MCP-Server.mcpb` | AI assistant extension for Claude Desktop (optional, see below) |
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for other local MCP clients, e.g. Claude Code (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, run QA checks, have translations drafted into the document, all from Claude's own chat window. To install: download the file, then in Claude Desktop open **Settings → Extensions → Advanced settings** and click **Install extension…** (double-click on the file also works if your system associates `.mcpb` with Claude; drag-and-drop does not). Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. Other MCP clients that run local servers (e.g. **Claude Code**) can use `Supervertaler-MCP-Server-exe.zip` instead: unzip it somewhere permanent and point the client's MCP config at the exe – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made snippet. Note: **ChatGPT's desktop app is not supported** – it runs MCP servers in the cloud and cannot reach a local Trados session ([details](https://docs.supervertaler.com/trados/mcp-server/)). [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

## [18.20.148 / 19.20.148] – 2026-07-30

All of the below came out of one real job: a 2,889-segment manual translated end to end through the MCP server. None of it was found in testing.

### Added (Supervertaler MCP Server · compare the whole document against your TM)

- **`compare_document_to_tm` reports every segment translated differently from what the TM already holds for the same source.** Concordance search answers "was this phrase translated before?" one query at a time, for phrases you already suspect; it cannot answer "across this file, where have I departed from the client's reference TM?", because that is a join over every segment rather than a lookup. On the job that prompted this, a term coined in good faith already had an established rendering in the client's own TM, and no amount of searching would have found it — you only look up what you already doubt. Runs against file-based `.sdltm` and GroupShare TMs alike, through the Trados API rather than by reading the file format directly.
- The comparison happens **inside the plugin**, so only the deviations travel to the assistant, never the TM. Sending a whole TM across for the model to diff would be enormously expensive and would fall apart on a large one — the reference TM in that report held 1,490 units and a master TM is far bigger. Ordinary spacing differences are ignored; non-breaking spaces are not, so a target that quietly lost one still shows up.
- Only finished segments are checked by default, and only sources that match the TM verbatim — so a clean result means nothing contradicts the TM, not that the whole document agrees with it. The response says so explicitly, and says that a difference is not automatically an error: a deliberate improvement is indistinguishable from a mistake here, so the assistant is told to present the list for review rather than align anything itself.

### Fixed (TermLens · terms with "(s)" were invisible, and Alt+Down mangled them)

- **A term written with the optional-plural convention – `verkoper(s)`, `party(ies)` – never matched anything in TermLens.** The tokeniser's character class read `%-/`, which looks like three literal characters but is a *range* covering U+0025 to U+002F, so it also matched `(`, `)`, `*`, `+` and `&`. `kandidaat-koper(s)` therefore tokenised as one single word, which no termbase entry could ever equal, and the panel simply reported no matches. Terms are now split at brackets as they always should have been, so an entry for `kandidaat-koper` highlights inside `kandidaat-koper(s)`. Percentages, `km/h`, `well-known`, `R&D`, `don't`, `C++`, `1.234,56`, `H₂O` and `m²` all tokenise exactly as before. (Reported by a user.)
- **Alt+Down on a selection ending in a bracket lost the closing bracket**, offering `kandidaat-verkoper(s` for the new entry — visibly wrong, and wrong in a way that was easy to save by accident. Balanced bracket groups now survive intact, leading or trailing, so `kandidaat-verkoper(s)` and `(her)certificering` are saved exactly as selected — the entry records what you chose, and TermLens indexes a bracket-stripped alias alongside it so the stored form still matches the `kandidaat-verkoper` token the tokeniser produces. Both spellings resolve to the same entry, and an alias merges with an existing base-form entry rather than being shadowed by it. Stray unbalanced edge punctuation (`koper)`, `verkoper,`) is still trimmed. (Reported by a user, twice — the first shipped fix reduced selections to the base term, which the same user demonstrated was the wrong call.)
- **Existing entries are not repaired automatically.** An entry saved under the old behaviour may still carry an unbalanced bracket (`… at Work (Cpbw`, `re)certification`) — mechanical repair is possible but some cases need a human decision about what was intended, so nothing in your termbase is rewritten behind your back. Ask the AI to list entries with unbalanced brackets if you want to review yours.

### Fixed (Supervertaler MCP Server · find & replace quietly unconfirmed finished work)

- **A single find & replace demoted every segment it touched to Draft**, with no way to opt out. Editing a segment's content makes Studio reset its confirmation status – correct while you are still translating, wrong when you are running a consistency sweep over a file that is already finished. On a fully translated document the replacement worked and the file silently became unfinished; you only noticed if you thought to re-check the statuses afterwards. Each changed segment now keeps the status it had. The AI can still ask for a specific status where that is what you want, and the response reports which of the two happened. (Reported by a user.)

### Added (Supervertaler MCP Server · non-breaking spaces you can actually see)

- **A new `check_nbsp` QA check** lists translated segments that came out with fewer non-breaking spaces than their source. Non-breaking spaces are invisible in Studio, in the AI's view of your segments and in every report, so a lost one normally surfaces only when the client rejects the file – which matters if your style guide wants one between a value and its unit (230 V, 3,5 mm, 50 %) or before a figure reference.
- **The AI can now write one, as `&nbsp;`.** A non-breaking space placed directly into a tool call reaches Trados only *sometimes*: depending on the AI client and the individual call it either arrives intact or turns into an ordinary space along the way, and the write reports success either way – so nothing distinguishes the two. Escape codes are no safer, because the client decodes them into the character first, and the character is what gets flattened. Intermittent is worse than broken here: it survives testing and then fails on the job. `update_segments` and `find_and_replace` therefore take a `decodeEntities` option, which lets the AI write the HTML entity `&nbsp;` (or any `&#NNN;` code point) and have Supervertaler convert it at the Trados end; plain ASCII travels intact, so nothing en route can mangle it. It covers both sides of find & replace, so *"put a non-breaking space between every value and its unit"* fixes a whole document in one pass – and because find & replace now preserves confirmation status, a finished file stays finished. Opt-in by design, so a document that genuinely contains the text `&nbsp;` is never silently rewritten. Supervertaler itself was never the culprit: it stores and returns the character faithfully, which is exactly why the loss was so hard to spot.

### Fixed (Supervertaler MCP Server · verification results that looked current but weren't)

- **`run_verification` reads the last *saved* state of your files, and its findings gave no sign of it.** That is documented behaviour, but the response came back as a full, confident findings list, so edits the AI had just applied were invisible to it – in one case reporting 17 segments as still untranslated when they had all been translated moments earlier. The response now carries an explicit `stale` flag whenever there are unsaved AI edits, and tells the AI to save and re-run rather than report anything. Nothing is saved automatically: that stays your decision.

### Fixed (Supervertaler MCP Server · large write batches could lose their confirmation)

- **Batches above roughly 45 segment updates outran the connection timeout.** The write itself went through, but the confirmation never came back, leaving the AI unable to tell success from failure – and a retry would apply the same edit twice. The per-call limit drops from 200 to 40, and the timeout on the MCP server side is raised well beyond any legitimate call, so both ends of the problem are closed.

### Added (Supervertaler MCP Server · a warning when no termbase is switched on)

- **`get_active_project` now warns when the open project has no read-enabled termbase.** Termbases are activated per project, so a project with all of them switched off is indistinguishable over MCP from one with no terminology attached: lookups simply return nothing, and nothing says why. A whole job was translated that way before anyone noticed. `list_resources` carries the same warning alongside its `readEnabled` flags.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
