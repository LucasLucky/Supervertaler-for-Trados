Supervertaler for Trados **v18.20.99** (Studio 2024) / **v19.20.99** (Studio 2026) — unsigned builds are attached below. Covers 18.20.99.

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

## [18.20.99 / 19.20.99] – 2026-07-18

### Added (Supervertaler MCP Server · your AI assistant can now run your whole Trados workflow)

- **The connection now starts automatically** – no more clicking the Supervertaler Assistant panel to wake it up. As soon as you have a document open in the Trados editor, your AI app can reach the project. (Previously the connection only started once you activated the Assistant panel; it now starts on its own regardless of which panel is in front.)
- **Find and replace across your translations** – *"replace every 'shall' with 'must' in my targets."* The AI can preview exactly which segments would change before applying anything, respects your inline tags (matches that would break formatting are skipped and reported), and can restrict to one file or one confirmation status.
- **Run Studio's own QA and act on it** – *"run verification and show me the findings."* The AI runs Trados' built-in Verify Files (QA Checker 3.0, tag and terminology checks: punctuation, brackets, repeated words, spelling, length, etc.) and gets the findings back per segment, with the QA rule and severity. It catches things the AI's own checks don't, and each finding links straight to the segment so it can jump there or comment on it.
- **Trados batch tasks by conversation** – *"pre-translate everything with my TM matches," "save my confirmed translations to the TM," "export the translated Word document."* Pre-translate, Update Main Translation Memories, and Generate Target Translations can all be triggered from your AI app.
- **Jump to any segment** – *"take me to segment 47"* – the AI moves the Studio editor to the segment it's discussing, by the number you see in the grid or its id.
- **Read, add, and update Trados comments** – flag a source issue for the client, leave a review note, or rewrite an existing comment after fixing the segment it describes.
- **Your Trados project termbases are now included** – terminology lookups, the terminology QA check, and the resource listing now search the termbases attached to your Trados project (the new **.ttb** format in Studio 2026 and **MultiTerm .sdltb** in Studio 2024), not just your Supervertaler termbases. Definitions come through too.
- Segment listings now include the **segment number you see in Studio's grid**, so the AI cites the right number when it talks to you (and never invents one).

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
