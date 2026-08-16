> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.182** (Studio 2024) / **v19.20.182** (Studio 2026). Covers 18.20.181 → 18.20.182.

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

## [18.20.182 / 19.20.182] – 2026-08-15

### Added (connect ChatGPT desktop to your Trados session, in one click)

- **The MCP server works with ChatGPT desktop, and the plugin now sets it up for you.** Ask ChatGPT about the project open in Studio, search your TMs and termbases, run QA checks — the same live connection Claude Desktop has had. **Settings → AI Settings → Connect AI assistant…** now has a **Set up ChatGPT desktop** button that downloads the server, keeps it in your Supervertaler data folder and registers it, so there is no zip to unpack and no configuration file to edit. Quit ChatGPT from the notification area afterwards — closing the window is not enough — and start it again.
- **Your existing configuration is backed up first, and nothing else in it is touched.** Only Supervertaler's own entry is written; other MCP servers you have set up are left exactly as they are, and running the button again refreshes the server rather than adding a second copy.
- **Earlier versions of the documentation said ChatGPT could not be used at all.** That was true when written — it ran MCP servers in the cloud, with no route to a bridge that is local to your machine by design — and has since changed. What still cannot work is a client that runs the server in the cloud, which includes the claude.ai and chatgpt.com **websites**, as opposed to the desktop apps.

### Added (ProZ.com's new term search, as an option)

- **ProZ has rebuilt its term search, and both versions are now available.** The classic search stays switched on as before; the classic one is now labelled **ProZ.com (old)** and the rebuilt one **ProZ.com (new)**, sitting next to each other in the list. The new one ships **switched off**, so nothing about your setup changes unless you want it to. Tick it in the **Web** picker to try it, and keep whichever you prefer — or both.

### Fixed (legacy termbase entries whose stored languages contradict their termbase)

- **A term saved the wrong way round for its termbase is checked against nothing.** Every read path orients an entry by the *termbase's* declared direction, so a row whose source column holds the termbase's target language gets indexed under the wrong language: no source segment can ever match it. The entry still sits in the termbase, still answers `lookup_term`, still reads as locked — and `check_terminology` passes over it in silence, however badly the document violates it. The failure is an *absence* of checking, which is the one kind nothing on screen can show you.
- **What is now detectable is the legacy population**, where old write bugs corrupted an entry's stored language labels and its text together: the contradicting labels are the signal, and every such entry is now reported. **An entry typed into the wrong boxes today is not caught**, because its labels are correct — only the text is reversed, and telling that apart needs the text's actual language, which the plugin will not guess. A wrong silent answer there would be worse than none.
- **`lookup_term` hits now carry `directionMismatch`** when the entry's own stored languages contradict its termbase's declared pair. This matters more than it sounds: a reversed entry's output looks entirely sensible on inspection — Dutch text in the field reported as Dutch, English in the field reported as English — so a reviewer verifying orientation through `lookup_term` would confidently confirm that all was well, using an instrument that could not see the fault. Reported from a live job, where fifteen entries were "verified" exactly that way.
- **`check_terminology` now reports the same contradiction**, in a `directionMismatches` section listing the affected entries per termbase with sample pairs, and a note saying plainly that where such an entry is genuinely reversed, silence about it means "not looked at", not "not violated". A term is usually locked *because* it was a known defect source, so one sitting in that list is the worst case, not an edge case.
- **The flag says "inspect this", never "this is dead", because two different faults produce it.** Either the text is reversed — the entry then matches nothing — or only the language labels are wrong while the terms are correctly oriented, in which case the entry works perfectly, since the read path ignores those labels anyway. Telling them apart needs the text's actual language, which the plugin deliberately never guesses (the same refusal as in the write path: term pairs are routinely identical across languages, so a detector would guess, and a wrong silent answer is worse than an honest "check this"). On the termbase this was built against, 2 of the 40 flagged rows were the harmless kind, so wording that called them all broken would have been wrong twice.
- **Reported, not silently repaired.** The per-entry language tags are exactly the field the read path stopped trusting in v4.19.21, after legacy write bugs left them wrong on rows whose text was perfectly fine; auto-flipping on that signal would turn a cosmetic tagging slip into a genuinely broken entry. Repair means re-adding the pair the right way round, or `tools/repair_termbase_directions.py` for a whole termbase, which weighs the text as well as the tags.
- **An entry whose two terms are the same string is not reported**, even with contradicting tags. Reversing it changes nothing — the index key is identical either way, so it matches exactly as it should. That is brand names, units and chemical formulae, and on the database this was built against it was 68 of 108 candidate rows: reporting them would have buried the 40 that are genuinely broken.
- **New entries have not been able to acquire this shape since the strict write path landed.** `add_term` orients per termbase, or refuses rather than guessing, so this is legacy damage: on the reporting user's own database, 40 broken rows across two termbases, none newer than June.

## [18.20.181 / 19.20.181] – 2026-08-14

### Changed (the web-search shortcut is Alt+W, not Ctrl+Alt+L)

- **Ctrl+Alt+L was the wrong choice and never reached the App Store.** That combination belongs to **Supervertaler Workbench**, which registers it as a *global* hotkey for its own SuperLookup — global meaning it fires wherever you happen to be typing, including inside Trados. Anyone running Workbench alongside Studio, which is the normal way to use them, would have triggered both at once.
- **Alt+W is the shortcut instead**, pairing with SuperSearch's existing **Alt+S**: S searches your own material, W searches the web. Alt+S is unchanged, and both can be rebound in Studio's keyboard shortcut settings.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
