Supervertaler for Trados **v18.20.134** (Studio 2024) / **v19.20.134** (Studio 2026) — unsigned builds are attached below. Covers 18.20.134.

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

## [18.20.134 / 19.20.134] – 2026-07-26

### Fixed (MCP Server · tools failing with a UI-thread error) — [#44](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/44)

- **MCP tools could fail with "This method/property must be called on the UI thread"** – reported for `add_comment`, but it could affect any tool that reads or changes the Studio document. It happened whenever the Supervertaler Assistant panel had not been opened in that Studio session: the plugin decided whether it needed to hand work to Studio's UI thread by asking that panel, and a panel that has never been shown answers misleadingly, so the call went ahead on the wrong thread and Studio rejected it. Working only in TermLens – as the new voice-command workflow encourages – made it reliably reproducible. The plugin now captures Studio's UI thread at startup and always routes bridge work through it, independently of which panels are open.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
