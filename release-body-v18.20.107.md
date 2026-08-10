Supervertaler for Trados **v18.20.107** (Studio 2024) / **v19.20.107** (Studio 2026) — unsigned builds are attached below. Covers 18.20.100 → 18.20.107.

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

## [18.20.107 / 19.20.107] – 2026-07-18

### Fixed (TermLens · Trados Studio 2026 .ttb termbases)

- **Project termbases are now labelled by their real format** in Supervertaler Settings → Termbases – `[.ttb]` (Studio 2026's SQLite format) and `[.sdltb]` (MultiTerm) – instead of everything showing as `[MultiTerm]`.
- **A `.ttb` termbase you attach mid-session now appears on its own.** A just-attached `.ttb` can fail its first read while Studio is still wiring it up, and unlike `.sdltb` it has no fallback, so it produced no TermLens hits until you toggled it off and on. TermLens now retries a failed `.ttb` load automatically for a few seconds, so matches show up without the manual toggle.

## [18.20.106 / 19.20.106] – 2026-07-18

### Added (Supervertaler MCP Server · ask "what can I do?")

- **New `help` tool.** Ask your AI app *"what can I do?"* / *"what can you do?"* / *"help"* and it shows a curated, grouped menu of the things you can ask this Trados assistant – project status, finding segments, TM and terminology, quality checks, editing, batch tasks, and the prompt library – with example phrasings. It's an authoritative, consistent list (not the AI improvising from memory), and the card text is a plugin resource, so it stays in sync as features are added.

### Changed

- **The Analyse Files tool is now named `analyze_files`** (was `analyze`). "Analyse my project" is a natural request for a *review*, which made the AI reach for the read tools instead of the batch task; the clearer name maps "run analyse files" straight to Studio's Analyse Files task. No change to how you phrase it in chat.

## [18.20.105 / 19.20.105] – 2026-07-18

### Fixed (Supervertaler MCP Server · analysis leverage bands now show up)

- **`get_project_statistics` now reads the leverage breakdown from the analysis report** (`Reports\Analyze Files*.xml`), not the copy cached inside the `.sdlproj`. After running Analyse Files from your AI app, the perfect/in-context-exact/exact/fuzzy/new/repetition figures were coming back as zeros because the SDK writes them to the report file while leaving the project's inline copy empty. It now reads the most recent report, so the real match-leverage numbers (including your TM hits) come through. Confirmation statistics (draft/translated) are unchanged.

## [18.20.104 / 19.20.104] – 2026-07-18

### Changed (Supervertaler MCP Server · batch tasks no longer time out, and new tools appear without an app restart)

- **Batch tasks now run in the background instead of blocking.** Analyse Files, Pre-translate, Update Main TMs and Generate Target Translations can take minutes on a real project – longer than an AI app will wait for a single tool call, which is why *"analyse the project"* previously timed out. Now the tool returns immediately with a job id, and the AI checks progress with the new **`get_task_status`** tool (status, elapsed time, and the task's own messages such as pre-translate match counts). Only one batch task runs at a time. For Analyse Files, once it reports done, `get_project_statistics` shows the leverage bands.
- **The MCP server now tells your AI app when the tool list changes** (`tools/list_changed`). Previously, if Trados wasn't fully up when the AI app connected, the app could show a stale tool list until you restarted it. The server now watches the connection and refreshes the list on its own – so a newly-added tool (or Trados starting after the app) shows up without a restart.

## [18.20.103 / 19.20.103] – 2026-07-18

### Added (Supervertaler MCP Server · the AI can now run Analyse Files)

- **New `analyze` tool** runs Trados Studio's **Analyse Files** batch task on the open project. It computes the leverage breakdown (perfect / in-context-exact / exact / fuzzy bands / new / repetitions) and writes it into the project – which is exactly what `get_project_statistics` reads back. So if the analysis bands came back empty (because Analyse Files had never been run), you can now just ask the AI to *"analyse the project"* and then *"show me the statistics"* – no need to leave the conversation. Like the other batch tasks it runs against the last-saved state.

## [18.20.102 / 19.20.102] – 2026-07-18

### Fixed (Supervertaler MCP Server · project statistics now work for the project you have open)

- **`get_project_statistics` now reads from the project open in the editor** instead of looking it up by name in Trados' `projects.xml` on disk. The old lookup silently failed for recently-created projects and for projects registered under a different Studio version (Studio 2024 and 2026 keep *separate* `projects.xml` files, and the lookup only checked 2024/2022) – so asking for statistics on a fresh project returned "no project found". It now resolves the analysis report from the open project's own `.sdlproj`, so it works regardless of when or where the project was created. Looking up a *different* project by name still works and now also finds Studio 2026 projects. The response carries a `source` field (`open-project` or `projects.xml`) so it's clear which was used.

## [18.20.101 / 19.20.101] – 2026-07-18

### Added (Supervertaler MCP Server · your AI assistant can now work with your prompt library)

- **Three new MCP tools give your AI app access to your Supervertaler prompt library** – the same Markdown prompts you use in the QuickLauncher and Batch Translate, shared with the Supervertaler Workbench:
  - **`list_prompts`** – browse your prompts (name, description, folder, and flags), optionally filtered by folder or a search term.
  - **`get_prompt`** – read the full text of any prompt.
  - **`save_prompt`** – create a new prompt, or update one of your own, straight from the conversation. Built-in default prompts are protected (save your version under a new name instead).
  - This turns your AI app into a prompt engineer: *"look at my Default Translation Prompt and suggest improvements,"* then *"save that as a new prompt."* Because the tool list is now discovered from the plugin (see 20.100), these appear after a normal restart – no extension reinstall.

## [18.20.100 / 19.20.100] – 2026-07-18

### Changed (Supervertaler MCP Server · future tool updates no longer need an extension reinstall)

- **The MCP server now discovers its tools from the plugin at connect time**, instead of carrying a hard-coded list baked into the extension exe. The plugin publishes the tool registry over the bridge (new `GET /v1/tools`), and the server advertises whatever it finds there. The practical effect: when a plugin update adds new AI tools, they show up in your AI app on its next restart – you no longer have to download and reinstall the Claude Desktop extension to get them. The server keeps a local copy of the last known tool list, so your tools are still listed when Trados is closed, and ships with a built-in copy for the very first run.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
