> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.180** (Studio 2024) / **v19.20.180** (Studio 2026). Covers 18.20.178 → 18.20.180.

## 📦 How to install

**Supervertaler for Trados is published through the [RWS App Store](https://appstore.rws.com/plugin/432).** Install it from there, or from inside Studio via **Add-Ins → RWS App Store**. Those builds are signed by RWS, so Studio loads them without an "unsigned plug-in" prompt at every start, and the plugin's own update check keeps you current.

The plugin binary is **not attached to GitHub releases** – this page is the changelog and the record of what changed in each build. App Store updates go through RWS review, so a brand-new fix can take a day or two to appear; if you are waiting on something specific, email support@supervertaler.com and I will send you the build directly.

| Also attached | What it is |
|---|---|
| `Supervertaler-MCP-Server.mcpb` | AI assistant extension for Claude Desktop (optional, see below) |
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for other local MCP clients, e.g. Claude Code (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, run QA checks, have translations drafted into the document, all from Claude's own chat window. To install: download the file, then in Claude Desktop open **Settings → Extensions → Advanced settings** and click **Install extension…** (double-click on the file also works if your system associates `.mcpb` with Claude; drag-and-drop does not). Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. Other MCP clients that run local servers (e.g. **Claude Code**) can use `Supervertaler-MCP-Server-exe.zip` instead: unzip it somewhere permanent and point the client's MCP config at the exe – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made snippet. Note: **ChatGPT's desktop app is not supported** – it runs MCP servers in the cloud and cannot reach a local Trados session ([details](https://docs.supervertaler.com/trados/mcp-server/)). [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

## [18.20.180 / 19.20.180] – 2026-08-14

### Added (SuperSearch now searches the web too) — [#64](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/64)

- **Select a term, press Ctrl+Alt+L, and 41 reference sites are one keystroke away** – IATE, Linguee, Reverso, ProZ, Juremy, Glosbe, EUR-Lex, Wikipedia and the rest, with the query and your project's own language pair already filled in. There is nothing to type and no language dropdown to set. Also on the editor right-click menu as **Search the web**, and on a new **🌐** button in the SuperSearch bar.
- **Ctrl+Alt+L is a new, second shortcut – Alt+S is unchanged.** Alt+S still searches your project files, TMs and termbases into the results grid, exactly as before. Ctrl+Alt+L is the web half. Neither affects the other, and both can be rebound in Studio's keyboard shortcut settings.
- **Web resources are a fourth SuperSearch scope**, sitting beside Files, TMs and TBs. Click **Web (n)** to choose which sites are active; five are on out of the box – Beijerterm, IATE, Linguee, ProZ and Reverso – and the other 36 are one tick away. Your own sites can be added with a URL template.
- **Results open either in a Supervertaler window or in your own browser**, your choice, from a checkbox in that same dialog. Neither is a fallback for the other and both are worth having: your browser brings your ad blocker and your signed-in sessions, while the Supervertaler window keeps one window and refreshes its tabs in place instead of leaving a trail of browser windows behind.
- **In the Supervertaler window, tabs load only when you click them.** Eight enabled resources would otherwise mean eight embedded browsers at once inside Studio 2024, which is a 32-bit application with a memory ceiling that Studio itself already presses against.
- **A term picked from the target side is searched in the target language.** Looking up a Dutch word in an EN→NL project searches nl→en, not en→nl – the latter is how you get a screen of nothing and conclude the site is broken.
- **Sites that demand a human-verification check are flagged, not fought.** ProZ in particular blocks embedded browsers; when that happens the tab offers to hand the page to your own browser, where you are already signed in and pass instantly. It is an offer, not a jump: nothing drags you out of the editor mid-segment.
- **Four resources were repaired or removed after checking all 41 against the live sites.** 2lingual, Oxford Collocations and the Financiële Begrippenlijst had all changed their URL schemes and had been quietly returning nothing – the Dutch one addressed an A–Z index page that has never been a term page at all. ChemIndustry is gone: the domain changed hands and now serves an unrelated site.
- **The resource list is interchangeable with the standalone SuperLookup app**, which uses the same file format, so a list exported from one imports into the other unchanged.

### Fixed (AutoPrompt could attribute wording to a source that never supplied it) — [#58](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/58)

- **A generated prompt could describe wording as "anchored by validated TM segment" when the TM contained nothing of the sort.** Seen on a real run: several glossary notes citing validated segments against a TM consisting of one title and five section headings, and one citing a segment for a word that does not appear in the source at all. Worth fixing carefully, because a provenance note is easy to read back as your own earlier decision rather than as something to check. A provenance claim may now only name a source that actually supplied the term — TM wording only for terms literally present in the supplied pairs, house wording only for rules in the knowledge base, termbase wording only for supplied terms — and where an input is absent, the prompt says so explicitly rather than leaving the claim available.
- **Given 7 validated TM pairs, the model was emitting 11**, filing its own renderings under "additional validated project segments" — one even carrying a tracked-change marker, which no human TM produces. That section outranks the glossary, so an invented entry was the most authoritative and least grounded thing in the prompt. Both branches now say "and no others", and say where a self-derived rendering belongs instead.
- **A glossary row could carry three candidate translations under a heading marked MANDATORY, LOCKED.** On a 535-segment patent run the Notes column was used to smuggle in an alternative: the locked cell offered *housing (enclosure)* while the note governed the term's only actual occurrence with a third rendering. Batch translation has no memory between batches, so an open choice is resolved differently each time. One locked target per row now, with the model told to split a term into one row per collocation where it genuinely differs. The same rule now covers mappings written in prose, which previously escaped it.
- **A memory bank you created but never filled in was announced to the model as "hard-won translation decisions and client-specific rules", followed by nothing** — an invitation for the model to supply conventions of its own and present them as established. A bank still matching the skeleton it was created from is now treated as absent rather than empty-but-asserted. Anything unrecognised counts as content, so a bank you have edited is never silently dropped.
- **When no termbase is enabled for the AI, the prompt now says so honestly.** The old warning claimed no glossary would be sent; in fact the model built one from the source text anyway, and the result was indistinguishable from a termbase-backed glossary in the finished prompt. The derived glossary is now explicitly derived, and its provenance is recorded in the saved prompt's metadata — shown in the library panel and QuickLauncher tooltip, where a person reads it, and never sent to the translating AI, where a "verify before use" line would have licensed the very substitution the lock exists to prevent.

### Fixed (a finished Batch Translate run could still be unsaved)

- **Batch translations went into the in-memory document and stayed there** until Studio's AutoSave next fired or you saved by hand — so a 27-batch job could finish and still exist only in memory minutes later. The project is now saved once when a run completes, including when you cancel it, which is when keeping the partial output matters most. If the save fails you are told to press Ctrl+S; the translations are in the document either way.
- **Saving after every batch was considered and rejected**: Studio's save is synchronous on the UI thread, so it would freeze Trados at every batch boundary to close a gap that AutoSave and the every-10-segments backup TMX already cover.

### Fixed (the cost warning recommended models your provider may not have)

- **The cost tip named GPT-5.4 Mini and GPT-5.5 whatever provider you were using**, so a user on Claude, Gemini or Ollama was advised to switch to a model that does not exist for them. It no longer names a model.

## [18.20.179 / 19.20.179] – 2026-08-11

### Fixed (TermLens missed terms wrapped in Markdown, e.g. `**doelstelling**`) — [#63](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/63)

- **A one-word term did not highlight when the segment had Markdown emphasis around it.** In a document where the client writes `**doelstelling**` inside the text – literal asterisks, not Studio tags – the term was in the termbase and TermLens showed nothing. Reported from a live job.
- **Multi-word terms in the same document did match**, which made it look random: `**duidelijk en concreet**` highlighted while `**doelstelling**` did not, two segments apart. They are found by different means, and only the single-word path was tripped by the asterisks.
- **Your prompts were never missing those terms.** The AI side matches on word boundaries and reads straight through the asterisks, so Batch Translate, AutoPrompt and the chat had the terminology all along. What was affected is what you can see and click: TermLens chips, TermPicker, and Alt+number insertion – which is worse than it sounds, because a term with no highlight looks like a term you never saved.
- Markdown emphasis is now trimmed from the ends of a word before it is looked up, covering `**bold**`, `*italic*` and `_italic_`. Terms with an underscore or asterisk inside them, like `snake_case`, are untouched.

## [18.20.178 / 19.20.178] – 2026-08-11

### Fixed (a new termbase switched itself on for the AI) — [#62](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/62)

- **Every termbase created since the AI opt-in was introduced has been sent to the AI by default**, which is the opposite of the intent and of what the settings describe. The "AI" column is stored as a list of termbases to *exclude*, and a brand-new termbase was in no list at all – so it counted as included, and the grid then showed its AI box ticked, making it look like a deliberate choice.
- **Creating a termbase is not consent to send its contents to a model.** A new termbase may be large, unreviewed, or full of material that has no business in a prompt. From this build a new one starts **Read-enabled but not AI-enabled**, whichever way it was created: the **+ Add** button, importing a Trados `.sdltb`/`.ttb` into a new termbase, or an AI assistant creating one through the MCP server. Tick its **AI** column when you want it used.
- **Existing termbases are left exactly as they are.** The stored list cannot tell "the user switched this on" apart from "this was never recorded", so correcting it in bulk would silently switch off termbases people had chosen on purpose. **If you have created a termbase recently, check its AI column** – it may be on without you having asked for it.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
