> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.184** (Studio 2024) / **v19.20.184** (Studio 2026). Covers 18.20.184.

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

## [18.20.184 / 19.20.184] – 2026-08-25

### Changed (QuickLauncher is on Alt+Q, because Ctrl+Q never worked)

- ★ **QuickLauncher has moved from `Ctrl+Q` to `Alt+Q`.** `Ctrl+Q` is a Trados factory default — **View Internally Source** — and Trados wins, so pressing it opened Trados's own command and QuickLauncher did nothing at all. No error, no hint that a plugin feature was meant to fire. On a fresh install the entire QuickLauncher menu, and the ten prompt slots behind it, were unreachable until you found the conflict yourself and cleared the binding in Studio's settings. `Alt+Q` matches the other shortcuts — `Alt+T` translate, `Alt+S` SuperSearch, `Alt+W` web search, `Alt+P` TermPicker. Trados does put **Tell me what you want to do** on `Alt+Q` (seen in Studio 2024), so it joins the short list of keys to free up in **File → Options → Keyboard Shortcuts** — a one-off, and Tell Me is a ribbon search box that works fine without a shortcut. The difference from `Ctrl+Q` is that the conflict is now documented in the help, the About box links to that page, and the key is one you would guess.
- **If you had already cleared the Trados binding to make `Ctrl+Q` work**, that key now does nothing for QuickLauncher. Use `Alt+Q`, or set your own under **File → Options → Keyboard Shortcuts**. You may also want to give **View Internally Source** its `Ctrl+Q` back.
- **The About box now lists QuickLauncher, its ten slots, and a link to the full shortcut reference.** That list is where you would go to look up a shortcut, and it did not mention `Ctrl+Q` — which is exactly how a flagship feature sat behind a dead key without it being obvious. The link goes to the docs page, which also carries the table of Trados defaults that override other Supervertaler keys and how to clear them.
- **The insert-term range was listed as `Alt+1…9`**; it is `Alt+0…9`.

### Fixed (the MCP server for ChatGPT was never updated after the first install)

- **Connect AI assistant… only ever downloaded the MCP server if it was missing**, so anyone who set ChatGPT up once kept that same server for ever. Pressing the button again rewrote the configuration and nothing else. The plugin could tell the server was too old for it — and said so — but there was no way to act on that short of deleting the file by hand. It now checks the installed server's version and replaces it when it is behind.
- **Updating no longer requires quitting ChatGPT.** Windows will not let a running program be deleted, so overwriting the server while ChatGPT had it open failed with a raw *"the process cannot access the file"*. The old server is now moved aside instead, which Windows does allow: ChatGPT keeps using it until you restart, and the new one is in place for next time. If even that is blocked, the message tells you what to quit rather than showing the underlying error, and your working server is left untouched.
- **A failed download can no longer leave you with no server at all.** The download used to unpack straight onto the existing file, which empties it before writing — so a download that failed half way took the working server with it. It now unpacks alongside and only swaps once the file is complete.
- **Installing the Claude Desktop extension over an older one** fails with an `EPERM … unlink` error, for the same reason: Claude Desktop leaves the server running while it replaces the extension's files. The Connect dialog now spells out the extra step — quit Claude Desktop from the notification area first — and explains the error if you hit it anyway. (Only when an extension is already installed; a first install is unaffected.)

### Added (translate two projects at once, in two Studios, with two AI assistants)

- ★ **Translate two different projects at the same time, in two versions of Trados Studio, each driven by its own AI assistant.** Open Studio 2024 and Studio 2026 side by side, tell ChatGPT *"use the 2024 one"* and Claude Desktop *"use the 2026 one"*, and set both going. Each assistant reads and writes only its own project and cannot touch the other's document. Two jobs progress at once, in two Studio windows, on one machine — and because you can talk to these apps by voice, both can be running while you are doing something else entirely.
- **Say which Studio you mean in plain language** — *"work with the 2026 one"*, *"use the Acme project"* — and that chat is bound to it for the rest of the session. Ask *"which Trados instances are running, and which are you using?"* to see the list first. Two new tools do the work: **list_trados_instances** and **select_trados_instance**.
- **The choice follows the project, not the process**, so it survives that Studio being closed and reopened. You do not have to say it again after a restart. Closing the other Studio works just as well — the one left is unambiguous immediately, with nothing to restart.
- **Or pair an app with a Studio permanently.** For people who always keep the same pairing, add `--instance 2024` to the server's arguments in the app's MCP configuration, or set `SUPERVERTALER_TRADOS_INSTANCE`. An app pinned this way never asks — and if the Studio it wants is not running, it says so instead of quietly using the other one.
- **Until you choose, reading works and editing waits.** Questions are always answered and the reply names the Studio and project it came from; anything that would change a document stops and lists what is open. Guessing is the one thing it will not do.
- **The Connect dialog warns when a second Studio is running**, and names its project — there is no way to tell from inside the first one, and it decides whether the AI will accept an edit at all.
- Requires the updated MCP server: reinstall the extension in Claude Desktop, or press **Connect AI assistant…** for ChatGPT, which now updates the server for you.

### Fixed (two Trados versions open at once no longer send the AI to the wrong one)

- **With Trados Studio 2024 and 2026 both open, an AI assistant could edit the wrong project without anything going wrong on screen.** Each Studio runs its own Supervertaler bridge, but they announced themselves in a single shared file, and whichever started last overwrote the other. So a chat app you had pointed at your 2024 project would quietly send its edits to the 2026 one instead — no error, no warning, the segments simply landed in the wrong document. Each Studio now publishes its own entry, carrying its Studio version and the project it has open.
- **When two are open, the AI is told so, and edits are refused until you say which one you mean.** Anything that reads — segments, TM matches, terminology, QA checks — still works and now names the Studio and project it answered from, so the AI can tell you which project it is describing. Anything that writes stops and asks. Refusing to guess is the point: a lookup from the wrong project is confusing, an edit into the wrong project is damage.
- **Closing one Studio no longer disconnects the other.** Shutting down deleted the shared entry no matter who owned it, so closing one Studio left the other running, connected to nothing, reporting only that no bridge could be found. Each Studio now cleans up after itself and hands the connection over to whichever is still running. A Studio that crashes or is killed is cleaned up by the next one to start.
- **A Studio still running now cleans up after one that has closed.** Trados ends its process without giving plugins a chance to tidy up, so a closed Studio always leaves its entry behind – harmless, because a stale entry is ignored, but it meant an older AI app saw a dead connection instead of being pointed at the Studio still running. The Studio that is still open now clears those entries and takes over the connection, rather than depending on the one that closed.
- **A closed Studio cannot come back as a phantom.** Windows reuses process numbers, so a fresh Studio could inherit a closed one's number and make it look as though two were open – which would refuse your edits for no reason. Entries now record when they were written and are checked against it.
- **Under the bonnet:** entries are matched on process identity rather than process id alone, so a recycled id cannot resurrect a Studio that has closed and block your edits with a phantom second instance. This needs the updated MCP server, which is on the release page — an older one keeps working exactly as before, on a single Studio.

### Added (the Library tab – see and edit your memory banks without leaving Trados)

- ★ **The Prompts tab is now the Library, and it shows SuperMemory.** Every memory bank and the files inside it appear beneath your prompt folders, so the one thing you could never see from inside the plugin – what the AI actually knows about a client – is now in front of you. Until now the only ways to look at a bank were Explorer or Obsidian.
- **The tree says two things nothing else did.** `_shared` is marked *“loaded with every bank”* rather than sitting in the list looking like an alternative to the active bank, and `reference/` is greyed and marked *“not read into prompts”* – it is the audit trail, and without that label people keep filing things there and wondering why the AI ignores them.
- **Bank files render as Markdown** rather than raw text, using the same converter as the chat panel, so a terminology table reads as a table. Select a file and **Edit** opens it for editing.
- **Rename and delete memory banks from inside the plugin.** Right-click a bank. Previously this meant closing Trados and renaming folders by hand. Deleting moves the bank to a `.trash` folder inside `memory-banks` rather than destroying it, so you can put it back by renaming that folder – and the confirmation tells you where it went.
- **`_shared` is protected**, and deleting the bank you are currently using is refused rather than quietly switching you to another one: which bank is active decides what every prompt is built from.
- **A Reference images row** on a bank and on its `figures.md`, naming the folder of drawings the current project points at. Groundwork for the figure-analysis feature; the analysis pass itself is not built yet, so no button pretends otherwise.
- **Editing a bank file leaves the rest of the file alone.** These files are shared with Obsidian and the Supervertaler assistant, which do not agree on line endings, so a naive save would rewrite every line and bury your one-word change in a whole-file diff. Supervertaler now writes each file back in its own style. If something else changes the file while you have it open, you are told before anything is overwritten.

### Fixed (Markdown rendering, everywhere it is used)

- **Headings below `###` printed their own hashes.** A file using `#####` for its sections rendered as a wall of text with `##### 4. Title blocks and document metadata` sitting in it literally. Affected the chat panel too, so any AI reply using `####` had the same problem.
- **Wrapped text lost its formatting and its shape.** Paragraphs broke at whatever column the file happened to wrap at; two-line bullets had their second half thrown to the left margin, outside the bullet; and a **bold** span split across a line break showed its asterisks instead of going bold.
- **Blocks ran together with no space between them**, so a heading was indistinguishable from the paragraph above it.

### Fixed (the settings fix, finished off everywhere else)

- **The same fix now covers the whole plugin, not just the panels.** Every remaining place that read or wrote settings on its own — the termbase editor, the term picker, the voice strip, the QuickLauncher, the update prompt, SuperSearch's mode and web-resource settings — goes through the one shared copy. Ten of those could previously lose a change outright: they read the file, altered one field and wrote the whole thing back, so anything saved by another part of the plugin in between was reverted.
- **Anonymous usage statistics could count one installation as two.** The anonymous id was read, checked and written in three separate steps, so two things starting at once could each find it missing and generate a different one. Only affects people who opted in, and only the accuracy of the totals — no additional information was ever collected.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
