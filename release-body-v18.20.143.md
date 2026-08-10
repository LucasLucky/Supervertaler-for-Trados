Supervertaler for Trados **v18.20.143** (Studio 2024) / **v19.20.143** (Studio 2026) — unsigned builds are attached below. Covers 18.20.135 → 18.20.143.

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

## [18.20.143 / 19.20.143] – 2026-07-27

### Fixed (AI · the timeout fix now covers every GPT-5.x route)

- **GPT-5.5 via OpenRouter** had the same problem as the direct OpenAI route and is now recognised as a reasoning model too.
- **Any GPT-5.x model ID** – including one you type in yourself as a custom model, and the GPT-5.6 family – now gets the long timeout automatically, instead of only the older o-series being recognised.

## [18.20.142 / 19.20.142] – 2026-07-27

### Fixed (AI · AutoPrompt timed out on GPT-5.5 and other slow OpenAI models)

- **AutoPrompt failed with "The request timed out." on GPT-5.5** (reported by a user; GPT-5.4 Mini worked fine on the same job). AutoPrompt asks the model for a large amount of output, and the OpenAI request paths allowed a flat two minutes for it regardless of how much was requested – where the Claude paths have always allowed ten minutes for large generations. All OpenAI paths now scale the same way, based on the size of the request rather than on a list of known-slow models, so this keeps working for models released after this build.
- **GPT-5.5 is now recognised as a reasoning model**, so every request to it gets the longer timeout, not just large ones.
- **AI request timeouts are now recorded in the diagnostic log**, and the error message suggests trying a faster model or sending less context. Previously a timeout left no trace in the log at all, which made it impossible to diagnose from a bug report.

## [18.20.141 / 19.20.141] – 2026-07-27

### Fixed (Batch Operations · Proofread prompt put verdicts on the wrong segments) — [#50](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/50)

- **The clipboard Proofread prompt numbered its review list from 1 instead of using the real segment numbers**, so as soon as any segment was skipped – a tag-only segment, for instance – every number after it was wrong, and the AI's verdicts were reported against the wrong segments. Nothing looked amiss: the output was well-formed and only a manual comparison revealed the drift. Found on a real 949-segment job where three tag-only segments pushed 826 of 946 verdicts three segments out of place. The batch now uses the same `[SEGMENT NNNN]` document numbers as the document-context block (and as the API path, which was never affected), and states that the numbers are deliberately non-contiguous.
- **The prompt also specified its output format twice, in two different ways** (`[SEGMENT 0002] ISSUE` with `Issue:`/`Evidence:`/`Suggestion:` versus `Segment 2: ISSUE` with `Problem:`/`Suggestion:`). A model following the second one dropped the evidence citations the first one asks for. The format is now defined once.

## [18.20.140 / 19.20.140] – 2026-07-27

### Fixed (TermPicker)

- **Escape now closes the term-details window** – in the docked pane and in the Alt+P popup alike. (Windows treats Escape as a dialog key, so it never reached the list; it is handled a level up now.) In the popup, a second Escape closes the picker itself. The details window also closes when you move to another row, so it can no longer describe the previous term.
- **The top row no longer flashes when you press Alt+P.** The list was hiding its selection while the editor had focus and redrawing it on arrival; the selected row now stays visibly selected (grey when unfocused, blue when focused). The list is also double-buffered, so rebuilding it on each segment change doesn't flicker.

## [18.20.139 / 19.20.139] – 2026-07-27

### Fixed (TermPicker pane)

- **The pane no longer starts empty.** If you kept TermPicker visible with the TermLens panel collapsed to a tab, the pane stayed blank until you clicked that tab: Studio only starts a panel when it is first shown, so TermLens wasn't yet following the document that the picker takes its matches from. The pane now starts TermLens itself, so it is populated the moment you open it.
- **You can now see which terms have details.** Rows whose term carries a definition, domain, notes or a URL are marked with an amber dot – the same signal the TermLens chips give – so it's clear when pressing `I` will show you something.
- **Escape closes the details popup** (previously it stayed on screen). In the Alt+P popup, a second Escape then closes the picker itself.
- **The right-click menu is back**: Edit Term, Mark as Non-Translatable and Delete Term, matching the TermLens chips. It acts on the row you right-click, and is disabled for MultiTerm entries, which are read-only.

## [18.20.138 / 19.20.138] – 2026-07-27

### Added (TermPicker · press I for term details)

- **Pressing `I` on a row in TermPicker shows the term's metadata** – the same popup, with the same content, as hovering a TermLens chip: forbidden / MultiTerm / non-translatable tags, and for every entry its synonyms, definition, domain, notes and URL. Press `I` again to dismiss it. Works in both the Alt+P popup and the dockable pane, and matches the `I` key that the TermLens popup has always had. TermPicker's keyboard set is now: arrows to navigate, ←/→ to collapse/expand synonyms, a term number to jump, **I** for details, **E** to edit, Enter to insert (and Esc to close the popup).

## [18.20.137 / 19.20.137] – 2026-07-27

### Changed (TermPicker pane · polish from first use)

- **The pane now opens pinned**, i.e. permanently visible. Previously it arrived auto-hidden, sliding in and straight back out again, which looked like a glitch. Studio still remembers wherever you drag it afterwards.
- **Alt+P now moves focus into the pane when it is open**, instead of covering it with the popup: from there arrows navigate, ←/→ collapse/expand synonyms, a term number jumps to it, Enter inserts. With no pane in your layout, Alt+P opens the popup exactly as before.
- **Escape closes the TermPicker popup** (the list was swallowing the key).
- **Pressing E on a row opens the term editor**, matching the TermLens popup's key – in both the pane and the popup. MultiTerm entries are skipped, as those termbases are read-only.

## [18.20.136 / 19.20.136] – 2026-07-27

### Added (TermPicker · now available as a dockable pane)

- **TermPicker can now be docked like TermLens**, for anyone who prefers a flat, sortable list as their permanent terminology display rather than TermLens's in-context chips. Open it from Studio's **View** tab (it is not pinned by default, so your existing layout doesn't change when you update). The pane updates on every segment change, in step with the TermLens panel, and inserting from it behaves exactly like the popup and the chips – same capitalisation adaptation, same keyboard grammar (arrows to navigate, Right/Left to expand/collapse synonyms, a term number to jump, Enter to insert).
- Both terminology views are now available in both placements: TermLens as a docked panel or at the cursor (tap **Ctrl**), TermPicker as a docked pane or at the cursor (**Alt+P**) – so you can choose the representation and the placement independently. **Alt+P still opens the popup** even when the pane is visible, mirroring how Ctrl-tap works alongside the docked TermLens panel.

## [18.20.135 / 19.20.135] – 2026-07-27

### Changed (TermPicker · new shortcut, synonyms shown up front)

- **TermPicker now opens with Alt+P** (was Ctrl+Shift+P). Ctrl+Shift+P is also Trados Studio's own *View Target*, so it appeared twice in Studio's keyboard-shortcut list and looked like a conflict. Alt+P is free. Note that Studio keeps your existing binding across plugin updates – if you had it on Ctrl+Shift+P, clear that and set Alt+P under **File > Options > Keyboard Shortcuts > Supervertaler for Trados**.
- **TermPicker opens with every synonym group already expanded**, so a single Alt+P shows all alternative translations at a glance instead of hiding them behind collapsed markers. Left/Right still collapse and re-expand individual groups.
- The **About** dialog's shortcut list now includes Alt+P (TermPicker) and Ctrl+Alt+V (voice commands), and its entry for *Translate active segment* is corrected to **Alt+T** – it still showed the old Ctrl+T, which was replaced in 20.119 because it collides with Trados's *Apply Translation Result*.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
