> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.177** (Studio 2024) / **v19.20.177** (Studio 2026). Covers 18.20.174 → 18.20.177.

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

## [18.20.177 / 19.20.177] – 2026-08-11

### Fixed (a term containing a pipe was mangled by a TSV export/import round trip) — [#61](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/61)

- **A term whose own text contains a `|` came back split in two.** The TSV export uses `|` to separate a term from its synonyms, and never escaped the character when it appeared in the term itself – so exporting and re-importing turned `DC| mode` into the term `DC` with a synonym ` mode`. Silently, on both the source and target side, with correct-looking counts. Found in a real project termbase, on two entries.
- **Pipes and backslashes in a term are now escaped**, so the delimiter and the character can be told apart. Verified across the awkward combinations, including a term containing both.
- **This does not repair an existing export.** In a file written before this build, a delimiter and a literal pipe are indistinguishable, and no amount of later cleverness can separate them. Re-export anything you intend to keep.
- **Files written from now on are also slightly different for older builds**: a pre-20.177 Supervertaler reading one will show a stray backslash in an affected term rather than splitting it. Only terms containing a pipe or a backslash are affected at all.
- **The MultiTerm XML and TBX exports were never affected** – they use XML escaping, and a round trip through them preserves such terms exactly.

## [18.20.176 / 19.20.176] – 2026-08-11

### Fixed (MultiTerm XML export named its languages wrongly)

- **The exported `<language>` element repeated the language code where MultiTerm expects the language name** – `type="EN"` instead of `type="English"`. MultiTerm matches its language indexes by that name, so an import would at best have created indexes called "EN" and "NL". Caught by comparing the export against a MultiTerm XML file known to import cleanly; the two are now identical in that respect. Everything else about the structure already matched exactly.

## [18.20.175 / 19.20.175] – 2026-08-11

### Added (get terminology *out* of Supervertaler and into Trados) — [#60](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/60)

- **Export now offers MultiTerm XML and TBX, alongside the existing TSV.** Until now terminology only travelled one way: a Trados termbase could be imported into Supervertaler, and nothing could go back. Pick the format in the save dialog on **Settings → Termbases → Export**.
- **MultiTerm XML** is what Glossary Converter and MultiTerm import, so it is the route to a `.sdltb` — and, via Studio 2026's Termbases view, to a `.ttb`. **TBX** is the ISO standard and is read by most other CAT tools too, so it is the better choice if you are not only a Trados shop.
- **Both carry more than the TSV export does.** Definition, context, part of speech and URL have always been dropped by the TSV export; the two new formats have proper homes for them, so they are kept.
- **Supervertaler still cannot write a `.sdltb` or `.ttb` directly, and the dialog says so.** Those are a Microsoft Access database and an undocumented SQLite schema respectively; writing either means guessing at a format that is not published, and a termbase Studio half-accepts would be worse than one it refuses. One conversion step outside the plugin is the honest trade.
- **What a round trip does not preserve:** MultiTerm entries are concept-oriented and can hold many languages, while a Supervertaler termbase is bilingual rows. Going out and back gives you your terms, not your original structure.

### Added (the AI can copy a project termbase into Supervertaler in one step) — [#59](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/59)

- **New `import_project_termbase` MCP tool.** Ask your AI assistant to copy the Trados termbase attached to the open project into a Supervertaler termbase and it now happens in a single step — the same operation as the *Import .sdltb/.ttb…* button, which an assistant previously could not reach at all. Its only option was adding terms one at a time, which for a few hundred terms is not a realistic offer.
- **It asks first, and it is safe to repeat.** A dry run reports what would be imported — how many entries, which language pair, how each Trados field will be mapped — before anything is written. Running it twice adds nothing the second time: every entry is checked against what is already there.
- **It respects the Write column.** An existing termbase must be Write-enabled, exactly as when the assistant adds a single term. A name that does not exist yet is created for you, and is deliberately left *not* Write-enabled, so the assistant cannot then add to it without your say-so.
- **Your Trados termbase is never touched** — it is read through a temporary snapshot, which also means a `.ttb` currently open in Studio is read correctly rather than half-read.

### Changed (Termbases tab wording)

- **The *Import .sdltb/.ttb…* button now has a tooltip**, because nothing said what it imported *into*. It also states that the Trados file is only ever read.
- The Export and Import tooltips said "CSV file" while both dialogs have always used tab-separated `.tsv`.

## [18.20.174 / 19.20.174] – 2026-08-11

### Fixed (nothing told you when your termbases were switched off for the AI) — [#58](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/58)

- **A termbase has two separate ticks — Read and AI — and only the Read one was ever checked.** With Read on and AI off, TermLens shows term matches on screen exactly as usual while every prompt goes out with an empty glossary. Nothing anywhere said so, and a prompt carrying no glossary looks identical to a model that ignored one, so this could run for months unnoticed. Found on the developer's own machine, on a live job with a 221-term glossary attached specifically for it.
- **This is the out-of-the-box state, not a setting anyone chose.** Termbases are not sent to the AI by default, and the change that introduced that default added every termbase that already existed to the "off" list. So unless you have been into the AI column since, the answer is probably that none of yours is enabled.
- **Three places now tell you.** A **Batch Translate or Batch Proofread** run says so in its log before it sends anything. **AutoPrompt** stops and asks, the same way it already warns when a termbase is too large for it. And an AI assistant connected over the MCP server is told when it asks about the project, so it can warn you rather than quietly producing untermed work.
- The wording distinguishes the two failures. "No termbase is read-enabled" and "read-enabled but none reaches the AI" need different fixes in different places, and being sent to check a tick that is already on helps nobody.
- **Where the tick is:** the **AI** column in the termbase grid on **Settings → Termbases**, which is also where the AI Settings tab has always pointed.
- **Also corrected: two older messages named a tab that does not exist.** The read-enabled warning and the `list_resources` note both said "Supervertaler settings > TermLens"; the tab is called **Termbases**. Nobody had reported it, but an instruction naming the wrong tab is worse than none.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
