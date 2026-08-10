Supervertaler for Trados **v18.20.115** (Studio 2024) / **v19.20.115** (Studio 2026) — unsigned builds are attached below. Covers 18.20.110 → 18.20.115.

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

## [18.20.115 / 19.20.115] – 2026-07-23

### Added (Supervertaler MCP Server · no more "now press Ctrl+S in Studio")

- **New `save_document` tool** – the AI can save the document open in the editor itself (the same as Ctrl+S, covering all files of a merged document) instead of handing you back to Studio to do it. It's instructed to save only when you ask or approve – AI-written translations still land as Draft for your review first – but *"save and then run the analysis"* is now one instruction. The batch-task tools now point the AI at `save_document` for their save-first-then-run flows.

## [18.20.114 / 19.20.114] – 2026-07-22

### Added (Supervertaler MCP Server · "look at segment 331" now fetches exactly segment 331)

- **`get_segments` can now fetch by grid number** – new `fromNumber`/`toNumber` parameters retrieve exactly the segment(s) you refer to by the number you see in Studio's grid, instead of the AI paging through the document and occasionally landing on the wrong window (which could produce confidently wrong conclusions about "what's in segment N"). Works in merged multi-file documents too: numbers restart per file, so the AI combines the range with the file name – and when it doesn't, the response says the match spans files. The tool now explicitly instructs the AI to use exact-number fetch, never offset-guessing, when you mention a segment number.

## [18.20.113 / 19.20.113] – 2026-07-22

### Added (Supervertaler MCP Server · the AI can now curate your termbase, not just add to it)

- **Term lookups now tell the AI which termbases are actually in use.** `lookup_term` searches every Supervertaler termbase in your database – including ones whose **Read** tick is off – which is useful for "do I have this anywhere?" questions, but was invisible. Hits from inactive termbases are now flagged, so the AI can say "found, but only in an inactive termbase", and a new `activeOnly` option restricts the search to your Read-enabled termbases – handy once you've accumulated many termbases and want lookups limited to the active set (*"only consult my active termbases"*).
- **New `update_term` and `delete_term` tools** complete the terminology loop: when the AI spots an outdated or wrong pair in your Supervertaler termbase, it can now fix or remove it instead of telling you to do it by hand. Rails: only termbases with the **Write** column ticked (the same gate as `add_term`); the entry must be identified by its **exact** current source and target; every other field of the entry (definition, notes, domain, flags) is preserved on update; and the response spells out exactly what changed, so the chat transcript doubles as your audit trail. Deleting is flagged to the AI as destructive – it's told to act only on your clear request or confirmation. Trados project termbases (`.ttb`/`.sdltb`) remain **read-only by design** – editing a live Studio termbase file from outside risks corrupting it, so those edits belong in Studio.

## [18.20.112 / 19.20.112] – 2026-07-21

### Changed (Supervertaler MCP Server · the connection now starts with Studio, not with the first document)

- **The AI bridge starts as soon as Trados Studio is up** – previously it waited for a document to be opened in the editor, so with Studio sitting on the Projects view your AI app saw a dead connection (and a stale tool list). The machine-wide tools (`list_projects`, `list_tms`, `list_project_templates`, the prompt library, `help`) don't need a document at all, and now work the moment Studio is running. Tools that do need one keep answering gracefully ("no document is open in the editor") until you open it.

### Fixed (Supervertaler MCP Server · Studio 2026's project registry is actually found now)

- **`list_projects` (and the by-name project lookups) missed every Studio 2026 project**, because Studio 2026 keeps its Documents folder under a *different name* than expected – `Studio 2026 Release`, not `Studio 2026`. The Studio folders are now discovered by enumerating `Documents\Studio *` instead of hardcoding names, so all versions' registries, Translation Memories folders and Project Templates folders are found regardless of how the edition names its folder – current and future.

## [18.20.111 / 19.20.111] – 2026-07-21

### Added (Supervertaler MCP Server · your AI assistant can now see all your projects, TMs and templates)

- **Four new machine-wide tools**: **`list_projects`** (every project registered in Trados Studio, with status, dates, paths and which Studio version registered it), **`get_project`** (details of any registered project by name – languages, files, status – without opening it), **`list_tms`** (the file TMs in your Studio folders plus those referenced by your projects), and **`list_project_templates`**. Ask *"what projects do I have?"*, *"when did I create the ACME job?"*, *"which TMs are on this machine?"*
- **All of these read every Studio version's registry** – Studio 2026, 2024 and 2022 each keep a separate project list, and previously only one was consulted (which is why a Studio 2026 project could come back as "not found"). Projects registered under more than one version are deduplicated. The same multi-registry search now also backs `get_project_statistics`'s by-name lookup, and the TM/template folders of all three versions are scanned.

## [18.20.110 / 19.20.110] – 2026-07-21

### Added (Supervertaler MCP Server · the plugin can now tell you when your extension needs updating)

- **Version handshake between the plugin and the MCP extension.** The extension exe now reports its protocol level to the plugin on every request, so the plugin knows whether the installed extension supports everything it needs. If a future plugin version ever requires a newer extension, you'll hear about it in three places without any new machinery: your AI assistant tells you directly in chat (via the `help` tool and project-status responses), and the **Connect AI assistant** dialog shows the status. Nothing nags today – every current extension remains fully supported; this just puts the plumbing in place so "your extension is outdated" can never again go unnoticed. Older extensions that predate the handshake are detected automatically (they simply don't report a version).

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
