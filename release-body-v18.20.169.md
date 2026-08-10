> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.169** (Studio 2024) / **v19.20.169** (Studio 2026). Covers 18.20.169.

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

## [18.20.169 / 19.20.169] – 2026-08-09

### Changed (SuperMemory · a memory bank is now three files you can actually read)

- **A memory bank is now `brief.md`, `terminology.md`, `style.md` and a `reference/` folder — and nothing else.** The seven numbered folders are gone, along with one-Markdown-article-per-fact and the YAML metadata on each. You are meant to open these files and edit them; the new **📂 Open folder** button in the toolbar exists for exactly that.
- **Why it changed.** A real bank reached 136 terminology files — for what is a 136-row table — behind a 97-file inbox backlog nobody had processed. Around 15% of articles had malformed metadata that silently excluded them from the very filtering the folders existed to enable: they were in the bank and they were not reaching the AI, and nothing said so. By that size no human could read the bank and tell. Knowledge you cannot audit is not knowledge you can rely on, and three files can be read start to finish in a few minutes.
- **Terminology is a table.** One row per decision, with a Scope column saying how far it travels (`project`, `client`, `domain`). A table is the format in which a *wrong* entry is findable — you can scan a hundred rows in half a minute and spot the one that says the wrong thing; you cannot do that with a hundred files.
- **New `_shared` bank, always loaded alongside the active one.** It holds the defaults that are true of your work rather than of any one client — house style, domain conventions, jurisdictional rules. **The active bank overrides it where they disagree**, and the AI is told which layer is which so it can apply the override rather than average the two. A rule earns its place in `_shared` once it has held for more than one client.
- **`reference/` is the audit trail.** Source material — client style guides, PDFs, glossaries, tracked-changes harvests — kept unmodified and never sent to the AI. Everything in the three files is derived from something, and keeping the original is what lets you check a rule that looks wrong.
- **Old banks are detected, not silently ignored.** A bank on the previous layout has none of the three files, so it would contribute nothing to a prompt without saying a word. When one is active an amber **⚠ Convert this bank** button appears; converting folds the old articles into the three files and moves the originals to `reference/_legacy`. Nothing is deleted. The conversion copies text across as-is rather than distilling it — deciding which of a hundred old decisions still hold is judgement, and a machine that guesses confidently there is how the old system filled up with material nobody could check.
- **Quick Add (Ctrl+Alt+M) appends a table row** instead of writing an article per term. Successive additions accumulate in one table rather than scattering. Raw notes go to `reference/`.
- **Process Inbox, Distill and Health Check are gone**, along with the inbox counter. They existed to manage complexity the new structure does not have, and on a converted bank they had nothing left to operate on.
- **Overview and Summary are replaced by ☰ Report**, which is computed from the files rather than from metadata that no longer exists: which of the three files are present and how big, how many rows the terminology table holds, **how many tokens the bank actually adds to a prompt**, whether `_shared` is being applied, and warnings for what now goes wrong — a missing brief, a terminology file still in prose, files in the bank root that are searchable but never sent to the AI. No AI call, so it is instant and free.
- Documentation for the whole section has been rewritten to match.

### Fixed (announcements and settings could lose what they had recorded)

- **A one-off notice could reappear after you had dismissed it** — sometimes, and not always, which made it look random. The dismissal was stored in the main settings file, and that file is written back whole from around 29 places; anything holding a copy loaded at startup (the AI Assistant keeps one for the session) would overwrite the flag on the next unrelated save, minutes later. Dismissals now live in their own small file where nothing else can clobber them.
- **The same flaw could have lost survey answers and the usage-statistics choice.** Saving now merges those append-only records with whatever is already on disk, so a stale copy can no longer erase them. They record things that *happened*, so a union is always the correct result.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
