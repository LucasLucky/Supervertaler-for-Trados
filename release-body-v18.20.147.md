Supervertaler for Trados **v18.20.147** (Studio 2024) / **v19.20.147** (Studio 2026) — unsigned builds are attached below. Covers 18.20.147.

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

## [18.20.147 / 19.20.147] – 2026-07-29

### Fixed (Clipboard Mode · multi-paragraph translations were cut short)

- **Pasting back a segment whose translation runs to more than one paragraph kept only the first one**, silently discarding the rest. If your source segment contains blank lines – a fault description followed by the steps to fix it, say – everything after the first blank line was lost on re-import. The response parser treated a blank line as the end of the translation; it now keeps reading to the end of the segment block, which was already marked by the next `Segment N` header. (Reported by a user.)
- **The same parser also cut a translation short at any paragraph beginning with a word and a colon** – `Note: …` in English, or `注意：…` in Chinese and Japanese. This was never reported separately, but anyone translating into CJK was especially likely to hit it. Both stopping rules are gone; the only thing that now ends a translation is the source-language label, for the models that put the pair the other way round.

### Added (Supervertaler MCP Server · your memory bank, from any AI client)

- **Three read-only tools put SuperMemory on the MCP server**, so Claude Desktop – or any MCP client – can consult your memory bank while you translate, whatever CAT tool you have open. `get_supermemory_context` loads the bank for the current project and cites the articles it drew from; `search_supermemory` searches the active bank by keyword; `list_supermemory_banks` shows which banks exist and which is active. Nothing is written back.
- **The tools reach existing installs without reinstalling the MCP extension** – the exe reads its tool list from the plugin, so a plugin update is enough.

### Fixed (SuperMemory · unverified notes no longer overrule the AI)

- **A note you had flagged as low-confidence, or that was never finished, carried the same authority as a verified one.** The prompt told the AI that knowledge-base decisions take priority, full stop – so a half-written Quick Add note could override a model that had it right. Low-confidence, draft and stub articles are now marked *unverified* and explicitly presented as a hint rather than an instruction, with the AI told to prefer its own judgement where the two disagree. Notes with no confidence set are unchanged.

### Fixed (Dialogs · long text no longer clipped)

- **The in-app survey cut off longer questions mid-sentence**, and could overlap its own controls at some display-scaling settings. Both dialogs now size themselves to their text instead of using fixed positions, so they render correctly whatever your resolution, DPI scaling or system font size.
- **A one-off notice could reappear on every launch.** Several startup tasks each saved the whole settings file, so whichever finished last silently discarded what the others had written. They now re-read immediately before saving.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
