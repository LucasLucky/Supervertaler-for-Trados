Supervertaler for Trados **v18.20.94** (Studio 2024) / **v19.20.94** (Studio 2026) — unsigned builds are attached below. Covers 18.20.94.

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
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for ChatGPT desktop / other MCP apps (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, have translations drafted into the document, all from Claude's own chat window. To install: download the file and **double-click it** – Claude Desktop installs it as an extension. Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. For **ChatGPT desktop** and other MCP apps without extension support: download `Supervertaler-MCP-Server-exe.zip`, unzip it somewhere permanent, and add the exe in the app's MCP settings (ChatGPT: Settings → Plugins → MCPs → Add server) – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made config snippet. [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

_See CHANGELOG.md._

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
