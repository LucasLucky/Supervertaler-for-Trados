Supervertaler for Trados **v18.20.98** (Studio 2024) / **v19.20.98** (Studio 2026) — unsigned builds are attached below. Covers 18.20.96 → 18.20.98.

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

## [18.20.98 / 19.20.98] – 2026-07-17

### Added (Import/Export · Trados segment comments now appear in every export format)

- **Bilingual exports now include your Trados segment comments** – previously they were silently dropped in every format. Comments (from both the source and target side, including comments on a selection) are exported as `Author (yyyy-MM-dd): text`, with multiple comments on one segment stacked per line. Where they appear: a **Comments column** in the DOCX table and the HTML report (only added when at least one exported segment actually has a comment, so comment-free exports keep their familiar layout), and a **`Comment:` line** in the Bilingual Text format – the same line label the Supervertaler Workbench uses, so files remain readable by both tools. Comments are reference material for the proofreader: they are **not** written back into Trados on re-import, and the Notes column stays a free writing space.

### Fixed (Import/Export · comment lines from Workbench-made text files can no longer corrupt a re-imported target)

- **The Bilingual Text re-import parser now understands `Comment:` lines, including multi-line comments.** Before, a text file containing comment lines (e.g. one exported by the Supervertaler Workbench, whose format always includes them) could leak comment text into the re-imported target when a segment had no `Status:` line, and a comment continuation line that happened to look like a language line (`NB: check this`) could even be mistaken for the translation itself. Comment lines and their continuations are now cleanly skipped, matching the Workbench parser's rules.

## [18.20.97 / 19.20.97] – 2026-07-17

### Changed (Import/Export · DOCX exports no longer contain Word bookmarks)

- **Bilingual DOCX exports no longer wrap each source cell in a hidden Word bookmark** (`SV_seg_1`, `SV_seg_2`, …). For anyone with Word's "Show bookmarks" display option enabled – common on translators' machines, where CAT-related add-ins often switch it on – every source segment appeared surrounded by light grey square brackets, which looked like stray characters in the file. The bookmarks were a leftover from a retired export layout: re-import identifies each row by the number in the `#` column and the sidecar manifest, and never read the bookmarks, so nothing about the round-trip changes. Existing exports with bookmarks re-import exactly as before.

## [18.20.96 / 19.20.96] – 2026-07-17

### Changed (Import/Export · single-file exports are now named after the file, not the project)

- **A bilingual export that contains one source file is now named after that file** – e.g. `Application as filed.docx_bilingual_text.txt` instead of `<project name>_bilingual_text.txt`. This applies everywhere a single file is exported: a document opened on its own, a merged multi-file document with just one file ticked, and each file emitted by the "Separate file per file" output mode. Previously, opening a project's files in separate editor tabs and exporting each one suggested the **same project-based name for every file** – so the second export would silently overwrite the first (including its re-import sidecar manifest). Only a genuine combined export (several files ticked, "Combine into one file") still uses the project name.
- **"Separate file per file" outputs drop the project-name prefix** – files are now `<source file>_bilingual.docx` rather than `<project> — <source file>_bilingual.docx`. The project name is still recorded inside the file's header block and in the sidecar manifest.

### Fixed (Import/Export · manifest recorded the wrong file when exporting a non-active file)

- **Exporting a single non-active file from a merged document now records that file's name** in the export header and sidecar manifest. Previously the manifest always claimed the segments came from the file whose tab was active, even when you had ticked only a different file in the file list.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
