Supervertaler for Trados **v18.20.108** (Studio 2024) / **v19.20.108** (Studio 2026) — unsigned builds are attached below. Covers 18.20.108.

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

## [18.20.108 / 19.20.108] – 2026-07-19

### Added (TermLens · import MultiTerm termbases into Supervertaler)

- **Import a Trados MultiTerm termbase into your Supervertaler termbase.** A new **Import .sdltb/.ttb…** button in Supervertaler Settings → Termbases reads a Trados termbase – `.ttb` (Studio 2026) or `.sdltb` (MultiTerm) – and imports its terms into a Supervertaler termbase, so they show up in TermLens (and in the Supervertaler Workbench, which shares the same database). A mapping dialogue detects the languages from the file and shows an example entry so you can confirm which side is which; you choose which descriptive fields (definition, note, subject/domain, status, part of speech …) map onto which Supervertaler fields, with sensible defaults filled in. Extra terms for a language are imported as synonyms, and a term's "forbidden/deprecated" status maps to the forbidden flag. Which language is stored as source or target is just an organisational choice – TermLens matches terminology in either direction automatically. `.ttb` import works in both the Studio 2024 and 2026 builds; `.sdltb` import needs the 32-bit Access engine and so runs in the Studio 2024 build (in Studio 2026, convert the termbase to `.ttb` first).

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
