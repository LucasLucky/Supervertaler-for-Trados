Supervertaler for Trados **v18.20.146** (Studio 2024) / **v19.20.146** (Studio 2026) — unsigned builds are attached below. Covers 18.20.145 → 18.20.146.

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

## [18.20.146 / 19.20.146] – 2026-07-28

### Fixed (AI Assistant · GPT-5.6 failed instantly in chat)

- **Any GPT-5.6 model (Sol, Terra, Luna) returned an immediate error in the Supervertaler Assistant chat**: *"Function tools with reasoning_effort are not supported for gpt-5.6-sol in /v1/chat/completions"*. The chat gives the model tools so it can look things up in your project (projects, statistics, TMs, termbases) – and OpenAI does not allow that combination with reasoning on this endpoint, applying a reasoning setting of its own that the request never asked for. The chat request now opts out of reasoning explicitly, so GPT-5.6 works there again.
- **This covered everything that runs through the Assistant chat** – your own messages, **AutoPrompt**, and QuickLauncher prompts sent to the Assistant – since they all submit through the same chat. **Batch Translate and Batch Proofread were never affected**: they send no tools, so GPT-5.6 keeps its full reasoning exactly where it matters most for translation quality. GPT-5.5 and earlier are unchanged throughout. (Reported by a user.)

## [18.20.145 / 19.20.145] – 2026-07-27

### Added (Supervertaler MCP Server · the AI can remove Trados comments too)

- **New `delete_comment` tool** rounds out comment handling (read, add, edit – and now remove). It's addressed exactly like `update_comment`: call `get_comments`, then pass the segment id and the comment's index, or `all=true` to clear every comment on a segment. It removes the **whole** comment, version history included – Studio's per-version *Delete version* surgery stays in the editor, where it belongs. Like the other destructive tools, the AI is told to act only on your clear request or confirmation and to say which comment it removed; a comment marker left empty is unwrapped so no dangling annotation remains on the segment, and the change is part of the document's unsaved edits until you (or `save_document`) save.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
