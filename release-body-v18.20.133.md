Supervertaler for Trados **v18.20.133** (Studio 2024) / **v19.20.133** (Studio 2026) — unsigned builds are attached below. Covers 18.20.123 → 18.20.133.

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

## [18.20.133 / 19.20.133] – 2026-07-26

### Added (MCP Server · filter segments by TM match rate) — [#44](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/44)

- **`get_segments` can now filter by TM match percentage** – requested by a user after a real 10K-segment job. Pass `matchMin`/`matchMax` (0–100): *"list the fuzzy matches between 75% and 94%"* or *"which segments have no match at all?"* (`matchMax=0`) now just work. Every returned segment also carries its `match` percentage and `origin` type (TM, MT, auto-propagated…). Tool definitions are served live from the plugin, so the new filter appears in Claude for Desktop automatically – no extension update needed.

### Fixed (Voice commands)

- Alias lists in Voice command settings now accept semicolons as well as commas as separators – `a;b` used to be silently stored as one unmatchable phrase.

## [18.20.132 / 19.20.132] – 2026-07-26

### Changed (Voice commands · naming) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- The command editor dialog is now called **Voice command settings** (was "Voice commands – advanced") – in its title bar, the TermLens mic right-click menu, and the tooltips.

## [18.20.131 / 19.20.131] – 2026-07-26

### Changed (Voice commands · contextual help) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- The Advanced voice-commands dialog now has a **?** help button in the title bar (and **F1**) opening the [Voice Commands help page](https://docs.supervertaler.com/trados/voice-commands/), and its title uses an en dash like the rest of the UI.

## [18.20.130 / 19.20.130] – 2026-07-26

### Fixed (Voice commands · two field-testing bugs) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- **Saying "zoom in"/"zoom out" no longer opens the TermLens popup as a side effect.** Voice keystroke commands with a Ctrl modifier synthesise a Ctrl press/release pair; when Studio consumed the key in the middle (a bound accelerator), the pair looked exactly like a Ctrl-tap – the popup gesture. The Ctrl-tap detector now ignores taps that coincide with a synthetic voice keystroke. Physical Ctrl-taps are unaffected.
- **The 🎤 mic button in the TermLens header now responds to the first click** when the panel was inactive (the Studio 2026 first-click-eaten quirk – same fix the other header buttons already had).

## [18.20.129 / 19.20.129] – 2026-07-26

### Fixed (Voice commands · new defaults now reach customised command sets) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- **Saving custom voice commands no longer hides default commands added in later updates.** A `voice_commands.json` saved in the Advanced dialog used to replace the built-in list entirely, so newly shipped defaults (e.g. "zoom in"/"zoom out") never appeared for anyone with a customised set – and *Restore defaults* was the only remedy, at the cost of your customisations. Saved command sets now carry a generation marker: on load, only the **new** default commands are appended, and your existing rows, custom phrases/aliases and deletions of old defaults stay exactly as you left them. To hide a default command you don't want, untick it rather than delete it.

## [18.20.128 / 19.20.128] – 2026-07-26

### Changed (Voice commands · polish) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- The Advanced voice-commands dialog now carries the Supervertaler icon instead of the generic form icon.
- **New default commands "zoom in" / "zoom out"** (aliases "bigger font" / "smaller font") mapped to Ctrl+Alt+PgUp / Ctrl+Alt+PgDn. Trados Studio's *Adapt font sizes* actions ship with no default shortcut, so bind those two chords once under **File > Options > Keyboard Shortcuts > Editor** – scroll to the actions named simply *Increase* and *Decrease* (that page has no search box) – and the voice commands control the editor font size hands-free.

## [18.20.127 / 19.20.127] – 2026-07-26

### Changed (Voice commands · integrated indicator + more default commands) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- **The voice indicator now lives in the TermLens header** as a permanent 🎤 button next to ↻ – grey when off (click to start), orange while starting, green while listening; heard commands flash briefly in the panel's status label, and right-clicking the mic opens the Advanced command editor. The floating strip no longer covers any part of the UI when TermLens is open; it remains only as a fallback for sessions without the TermLens panel, and is now draggable with its position remembered.
- **New default commands**: "match one"–"match nine" (apply Translation Results match N, Ctrl+1–9), "escape" (close the focused popup/dialog – term popup, TermPicker…), "go to the top" / "go to the bottom" (Ctrl+Home / Ctrl+End), and "add term" (Alt+Down, write termbases) is now distinct from "add project term" (Alt+Up, project termbase).
- If you saved custom commands in the Advanced dialog on an earlier build, click **Restore defaults** there to pick up the new command set.

## [18.20.126 / 19.20.126] – 2026-07-26

### Added (Voice commands · hands-free Studio control) — [#48](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/48)

- **One-button voice commands.** Press **Ctrl+Alt+V** (or use the editor right-click menu) and control Studio hands-free with a ready-made command set – no configuration needed: "confirm", "next/previous segment", "copy source", "clear target", "term one" … "term nine" (inserts the numbered TermLens match, with capitalisation adaptation), "term picker", "term popup", "add term", "translate", "concordance" and "stop listening". A small status strip shows the listening state and each command as it is heard, with stop and Advanced buttons.
- **Offline and private.** Recognition runs locally via the Vosk engine in grammar mode – it listens *only* for the command phrases, which makes commands fast and reliable, and no audio ever leaves your machine. The first activation downloads the engine and a small English model (~50 MB, one-time, with progress shown); the plugin package itself stays the same size. Commands only execute while Trados Studio is the foreground window, so speech in other apps can't trigger anything.
- **Advanced dialog** (the gear on the status strip) for those who want to go deeper: edit phrases and aliases, enable/disable commands, and map new spoken phrases to any Studio keyboard shortcut or plugin action. The command file is compatible with Supervertaler Workbench's voice-command JSON, so command sets can be exchanged between the two products. Designed to pair with dictation tools (e.g. Wispr Flow) – they dictate, Supervertaler handles the hands-free commands.

## [18.20.125 / 19.20.125] – 2026-07-26

### Fixed (TermLens · adding terms via the dialog and merge-as-synonym is now instant)

- **The add-term dialog (Ctrl+Alt+T) and the "add as synonym?" prompt no longer trigger a full reload on save.** Both paths used to re-read the settings, reload the entire termbase database, re-read every attached MultiTerm termbase and rebuild the display after each save – which made them feel noticeably slower than the Alt+↑/Alt+↓ quick-adds. They now update the in-memory index incrementally, the same way the quick-adds always have, so saving a term or merging a synonym is effectively instant. A newly added source synonym also becomes a live match immediately (previously it wouldn't match until the next full reload). The full reload still runs where it is genuinely needed – editing an existing entry and "Add & Edit".

## [18.20.124 / 19.20.124] – 2026-07-26

### Added (TermLens · term capitalisation now follows the segment)

- **Displayed and inserted terms now adapt their capitalisation to the source occurrence in the segment.** A term stored as "More preferably" ↦ "Meer bij voorkeur" used to show and insert with the stored capital even when the segment contained it lower-case mid-sentence; the chip and every insertion path (chip click, Alt+digit shortcuts, the TermLens popup and the TermPicker dialog) now follow the segment: lower-case occurrences show a lower-case term, sentence-initial occurrences are capitalised, and ALL-CAPS occurrences (headings) upper-case the whole term. The rules are deliberately conservative – acronyms and mixed-case terms (MRI, pH) are never altered, and abbreviation or suffix-tolerant (Korean/Japanese) matches are left untouched. Can be switched off with the new **Adapt term capitalisation to the segment** option in TermLens settings (on by default).

## [18.20.123 / 19.20.123] – 2026-07-25

### Fixed (Terminology · MultiTerm termbases now reliably reach the AI and the Termbases tab) — [#38](https://github.com/Supervertaler/Supervertaler-for-Trados/issues/38)

- **A MultiTerm/Trados termbase’s terms could silently miss the AI prompt in batch jobs.** When a `.sdltb`/`.ttb` can’t be read directly (no ACE/JET driver, 64-bit host, Studio 2026), Supervertaler falls back to Trados’s own terminology provider – which only answers **one segment at a time**. TermLens queried it for the segment you were looking at, so its terms appeared on screen *and* reached the prompt for that segment – but a Batch Translate, Proofread or clipboard run covers segments you never visited, and those were never queried, so their terms were silently absent from the prompt. Because TermLens kept showing hits, it looked as though terminology was being sent. Batch Translate, Batch Proofread, the clipboard and preview paths, and both single-segment (Alt+T) paths now query the fallback provider for exactly the segments being processed before assembling the term list. Results are cached per document, so a repeat run costs nothing, and the bridge log records how many lookups were pre-warmed. Termbases read directly (the normal case) were never affected.
- **An attached termbase could be missing entirely from Settings → Termbases.** The grid was built from a snapshot of the editor’s loaded termbase list, with a fallback that only kicked in when that snapshot was **completely** empty. If it held some termbases but was missing one (for example taken before that one finished loading), the missing termbase had no row at all – not even “Failed to load” – so its **AI** tick box was unreachable even while TermLens was showing its terms. The grid now reconciles the snapshot against the termbases actually attached to the project and adds a row for anything missing, so every attached termbase is always listed and tickable.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
