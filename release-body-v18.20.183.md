> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.183** (Studio 2024) / **v19.20.183** (Studio 2026). Covers 18.20.183.

## 📦 How to install

**Supervertaler for Trados is published through the [RWS App Store](https://appstore.rws.com/plugin/432).** Install it from there, or from inside Studio via **Add-Ins → RWS App Store**. Those builds are signed by RWS, so Studio loads them without an "unsigned plug-in" prompt at every start, and the plugin's own update check keeps you current.

The plugin binary is **not attached to GitHub releases** – this page is the changelog and the record of what changed in each build. App Store updates go through RWS review, so a brand-new fix can take a day or two to appear; if you are waiting on something specific, email support@supervertaler.com and I will send you the build directly.

| Also attached | What it is |
|---|---|
| `Supervertaler-MCP-Server.mcpb` | AI assistant extension for Claude Desktop (optional, see below) |
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for other local MCP clients, e.g. Claude Code (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, run QA checks, have translations drafted into the document, all from Claude's own chat window. To install: download the file, then in Claude Desktop open **Settings → Extensions → Advanced settings** and click **Install extension…** (double-click on the file also works if your system associates `.mcpb` with Claude; drag-and-drop does not). Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. Other MCP clients that run local servers (e.g. **Claude Code**) can use `Supervertaler-MCP-Server-exe.zip` instead: unzip it somewhere permanent and point the client's MCP config at the exe – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made snippet. **ChatGPT desktop works too** – unzip the exe somewhere permanent and register it as a STDIO server in your Codex config; the [setup guide](https://docs.supervertaler.com/trados/mcp-server/#setting-it-up) gives the exact file and the block to paste. What cannot work is a client that runs the server in the cloud, such as the claude.ai or chatgpt.com websites – the bridge is local to your machine by design. [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

## [18.20.183 / 19.20.183] – 2026-08-16

### Fixed (settings quietly reverting, depending on which panel you opened them from)

- **A setting could be undone by a panel that was not even involved.** Change your memory bank in the Supervertaler Assistant, then open Settings from the TermLens panel and click OK: the bank goes back to what it was. Nothing warns you, and the panel afterwards agrees with the reverted value, so the change looks like it never happened rather than like it was lost.
- **The cause was five copies of one settings file.** Each panel and dialog held its own, and saving wrote the whole thing back, so whichever saved last silently reverted every field another had changed since it loaded. There is now one shared instance, so "a stale copy" is no longer something that can exist. This was not a memory-bank fault: **any** setting could be lost this way — API keys, which termbases are ticked, batch size, provider choice. The memory bank is simply where the damage was visible, because it ends up as the wrong terminology in a finished translation.
- **A new prompt not appearing in the dropdown was the same fault**, seen from the other end, and is fixed by the same change.
- **Two of the five ways to open Settings could not see your changes at all.** The licence link in the About box and the QuickLauncher menu header each opened their own copy of the settings file, so anything you had changed in either panel since Studio started was reverted the moment you clicked OK — and nothing refreshed afterwards, so the panels went on showing values the file no longer held until you restarted. Every gear icon, menu entry and link now opens the same Settings, and both panels update whichever one you came from.
- **Changing a setting no longer freezes Studio for up to two minutes**, depending on which gear icon you used. Settings reloads the termbases when it closes, and one of the two paths did it on the interface thread. Both now do it in the background, as the faster one already did.
- **A memory bank created inside the Settings dialog now appears in the dropdown straight away.** It used to appear only if you had opened Settings from the TermLens panel rather than the Assistant.
- **Deleting a termbase and then pressing Cancel used to leave the settings pointing at it.** The termbase itself was already gone — deletion happens immediately — but the references to it were only cleaned up if you pressed OK. They are now cleaned up either way.

### Fixed (the AI being handed less than it asked for, without being told)

- **Part of a memory bank could be left out of an answer with nothing to say so.** A bank that does not fit the size limit is trimmed, which is necessary — but it was silent, and two of your three articles look exactly like all three. A rule you had written down could be simply absent from what the AI saw, and neither of you would know. The AI is now told which files were left out and that it should ask rather than guess when a question turns on them.
- **`get_supermemory_context` ignored the bank you asked for.** Ask for one client's bank while another is active and you were quietly given the active one, with the response naming your requested bank back at you — so the reply read as confirmation. The argument now works, and an unknown bank name is refused, listing the banks that exist, rather than falling back to the active one and injecting another client's locked terminology.
- **Files you added to a memory bank yourself did nothing.** Only three fixed filenames were ever read into a prompt, while the bank listing counted every `.md` — so a hand-written `figures.md` was reported as present and contributed nothing. All Markdown files at the top of a bank are now read, under their own filename.

### Fixed (Batch Operations showing a different prompt from the one it would use)

- **The dropdown could show one prompt while the tick inside it marked another.** The closed box was filled in from a guess based on the project name; the tick came from the prompt you had actually set as active. So a run used the ticked prompt while the box named a different one. The active prompt now decides both. The project-name guess still applies when no prompt is active — a guess should not outrank a choice.

### Changed

- **The bilingual Word export no longer has a Notes column.** It was empty on the way out and discarded on the way back, so it named nothing you could rely on, and sitting next to Comments it suggested a distinction that did not exist. Its width has gone to Source and Target. Files exported before this change still import correctly.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
