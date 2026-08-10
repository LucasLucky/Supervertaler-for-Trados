Supervertaler for Trados **v18.20.109** (Studio 2024) / **v19.20.109** (Studio 2026) — unsigned builds are attached below. Covers 18.20.109.

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

## [18.20.109 / 19.20.109] – 2026-07-20

### Added (Supervertaler MCP Server · your AI assistant as prompt engineer)

- **New `get_prompt_context` tool** hands your AI assistant everything it needs to write a translation prompt tailored to the project open in Trados: source/target languages, the detected domain, the source text, the relevant termbase terms, a few confirmed TM example pairs, and your current Default Translation Prompt as a starting point. Ask *"look at my project and write me a tailored prompt,"* refine it together, then *"save it"* (via `save_prompt`). The plugin makes **no** prompt-engineering API calls of its own – the AI you're already chatting with does the work, which is what it's best at.
- **New AI Setting – "Prompt context – source segments"** (Settings → AI Settings, under External AI assistants): controls how much of the source document `get_prompt_context` sends. **0 = the whole document** (the default – ideal for large-context models like Claude and for high-value projects where you want the AI to see everything); a positive number caps it. The AI can also override it per request with `maxSegments`.

### Fixed (AutoPrompt · generated prompts no longer claim segments arrive "one at a time, in isolation")

- **AutoPrompt's meta-prompt described segment delivery wrongly**, so every generated prompt told the translator AI it receives *"one segment at a time, in isolation"* – but Batch Translate/Proofread actually send **numbered batches** of segments (your *Batch size* setting, e.g. 75 per request). The generated prompts therefore forbade using context the AI could legitimately see, and left terminology "choices" open that can't stay consistent across batches. The template now describes batched delivery correctly: translate every delivered segment and keep count/order aligned; in-batch context (e.g. a nearby antecedent) **may** be used; batch boundaries are arbitrary, so document-wide checks belong to a QA pass; there is no memory between requests, so the prompt must **lock** every recurring term (no open "X or Y" choices); and ⟦TC: …⟧ correction markers stay attached to their own segment, never pooled at the end of a batch. Existing AutoPrompt-generated prompts in your library keep the old wording – regenerate (or hand-edit) the ones you rely on.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
