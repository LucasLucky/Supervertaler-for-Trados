Supervertaler for Trados **v18.20.122** (Studio 2024) / **v19.20.122** (Studio 2026) — unsigned builds are attached below. Covers 18.20.116 → 18.20.122.

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

## [18.20.122 / 19.20.122] – 2026-07-24

### Changed (AI models · Claude Opus 5 added, superseded models retired)

- **Claude Opus 5 added** (released 24 July 2026). Anthropic’s new flagship Opus: near-Fable-5 intelligence at **$5/$25 per million tokens** – the same price as Opus 4.8 and half of Fable 5 – with no always-on-thinking surcharge. It is now the premium choice for hard legal/technical translation and long-context jobs. **Claude Sonnet 5 remains the recommended default** for routine work.
- **Claude Opus 4.8 and Claude Sonnet 4.6 removed from the model picker** – both are superseded (Opus 5 costs the same as Opus 4.8 and is better; Sonnet 5 supersedes Sonnet 4.6), so keeping them only made the list harder to choose from. The OpenRouter routes were updated to match (Sonnet 5 / Opus 5). Their prices stay in the shared pricing list, so cost figures for existing projects and past usage logs still resolve. If you had one of the retired models selected, pick a current one in **Settings → AI Settings**.

## [18.20.121 / 19.20.121] – 2026-07-24

### Fixed (MCP Server · non-ASCII search terms now match)

- **`get_segments` (and every other query-based tool) returned zero results for any non-ASCII search text.** A `contains=` / `q=` filter for a word like *oriëntatie*, or a symbol like *α*, matched nothing even when the document plainly contained it, while ASCII words (*hoek*, *strok*) worked. Cause: the bridge read query parameters via .NET Framework’s `HttpListenerRequest.QueryString`, which does not reliably UTF-8-decode percent-escaped non-ASCII (the MCP client correctly sends `ori%C3%ABntatie`, but the plugin decoded it to mojibake that never matched). The bridge now parses the raw query with explicit UTF-8 decoding, so accented, Greek, CJK and other non-ASCII searches work across all tools (`get_segments`, TM/termbase search, lookups, etc.). ASCII queries are unaffected. Found via the Supervertaler MCP Server while chatting to Claude about a live project.

## [18.20.120 / 19.20.120] – 2026-07-24

### Fixed (TermLens · terms with an apostrophe, e.g. "SDG’s", now match) — [#19](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/19)

- **Single-word terms containing an apostrophe were never matched.** The word tokeniser splits on apostrophes, so a segment like "SDG’s" was cut into "SDG" + "s" and a termbase entry "SDG’s" could never be looked up. Such terms now go through the same substring matcher that multi-word terms use, so they are found whole.
- **Curly vs straight apostrophes now fold together.** Word, InDesign and most DTP tools auto-convert a typed apostrophe to the "smart" curly form (U+2019), so a term stored with one apostrophe form silently failed to match a segment carrying the other. Matching now folds curly/modifier/fullwidth apostrophes to a plain ' on both sides (the same length-preserving normalisation that already folds Unicode spaces and sub/superscripts). A term stored as "SDG’s" matches "SDG's" in the text and vice versa.
- Terms *without* an apostrophe are unaffected: "SDG" still matches "SDG’s" as before.

## [18.20.119 / 19.20.119] – 2026-07-24

### Fixed (Keyboard shortcuts · "Translate active segment" no longer collides with Ctrl+T)

- **The default shortcut for "Translate active segment" moved from `Ctrl+T` to `Alt+T`.** `Ctrl+T` is a Trados **factory default** ("Apply Translation Result"), so a fresh install had *both* commands on the same key. Pressing it fired both – the native match-apply and Supervertaler’s translate – which raced on the same segment and could **freeze Studio** (seen once the AI write and the native apply landed on the same keypress). `Alt+T` is collision-free. This affects **new installs and default bindings only** – Studio stores each user’s keyboard shortcuts, so if you already rebound it (or cleared the Trados `Ctrl+T`), your setup is untouched; to switch, reassign "Translate active segment" to `Alt+T` in **File → Options → Keyboard Shortcuts**. The `Ctrl+T` row is gone from the "free up Trados shortcuts" list in the help docs.
- **The dead duplicate action is relabelled.** The long-deprecated "Translate active segment (deprecated – use Ctrl+T)" entry (kept registered only so Studio doesn’t crash on a missing type) no longer references `Ctrl+T`; it now reads "(deprecated – do not use)".

## [18.20.118 / 19.20.118] – 2026-07-24

### Added (Licensing · server-side trial registration, observe-only)

- **Trial installs now register with the licence server on startup** ([#47](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/47)). The server records the trial’s authoritative start date on first contact and returns the same original date ever after, giving the trial one reliable record that survives reinstalls, data-folder moves, and clock changes. **In this release the server’s answer is only recorded, not enforced** – local trial behaviour is completely unchanged, and the call fails silently when offline (a legitimate user is never blocked or nagged). A later release will make the server date authoritative with a generous offline-grace window for air-gapped work. Privacy: only the anonymous machine hash already used for licence activation, plugin/Studio version, locale, and the trial’s local start date are sent – details in the privacy policy at supervertaler.com/privacy.
- **"Cost shouldn’t be a barrier."** The trial-expired message and the Licence settings panel now say it explicitly: if the price is a problem for you, get in touch and we’ll work something out.

## [18.20.117 / 19.20.117] – 2026-07-23

### Added (AI Assistant · Claude Fable 5)

- **Claude Fable 5 is now selectable as a Claude model** (Supervertaler Settings → AI Settings). Fable 5 is Anthropic’s most capable model (released June 2026), sitting above Opus 4.8: it runs deeper, always-on reasoning on every request and costs double Opus – $10/$50 per million tokens vs Opus 4.8’s $5/$25 – and the always-on reasoning itself bills as output tokens, so the real per-job cost is higher than the sticker ratio suggests. Worth reaching for on the hardest jobs – dense legal/technical material, AI Proofreader passes over a whole document – while **Claude Sonnet 5 stays the recommended default** for routine translation and batch work. The shared pricing list (`pricing.json`) now covers Fable 5, so the cost estimator handles it out of the box.
- **Response parsing handles always-on reasoning.** Fable 5 puts a "thinking" block before the text in every response; the response parser previously read only the first content block, so every Fable 5 call – including Test Connection – failed with "Could not parse Claude response". The parser now extracts the text-typed block(s) regardless of position (the chat/tool path already did). A safety refusal (Fable 5’s content classifiers) now also produces a clear "Claude declined this request" message instead of a generic parse error.

## [18.20.116 / 19.20.116] – 2026-07-23

### Fixed (TermLens · invisible characters can no longer hide your term matches)

- **Multi-word terms now match segments containing Unicode space variants.** InDesign/IDML-derived documents routinely carry a no-break space inside a phrase ("display panel" with U+00A0 instead of a plain space). TermLens's multi-word matching is an exact substring search, so a termbase entry stored with a normal space silently never matched such a segment – and because single words still matched, the miss looked like a termbase problem rather than a document quirk. Matching now folds all Unicode space variants (no-break, narrow no-break, en/em/thin/hair spaces, ideographic space) to a plain space on both the segment side and the termbase-index side – covering Supervertaler termbases, MultiTerm `.sdltb`, Studio 2026 `.ttb` and API-fallback termbases.
- **Terms can no longer be *saved* with invisible characters.** Selecting text in the editor to add a term pair copied any no-break/zero-width characters straight into the termbase – producing an entry that looked identical to a clean one but could never match anything (a no-break space even stopped the entry being classified as a multi-word term at all). Every term/synonym write path – add, quick-add, batch add, edit, TSV import, non-translatables – now folds space variants to a plain space, strips zero-width characters (ZWSP, word joiner, BOM) and collapses runs of spaces before storage.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
