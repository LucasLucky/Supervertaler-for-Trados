> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.173** (Studio 2024) / **v19.20.173** (Studio 2026). Covers 18.20.172 → 18.20.173.

## 📦 How to install

**Supervertaler for Trados is published through the [RWS App Store](https://appstore.rws.com/plugin/432).** Install it from there, or from inside Studio via **Add-Ins → RWS App Store**. Those builds are signed by RWS, so Studio loads them without an "unsigned plug-in" prompt at every start, and the plugin's own update check keeps you current.

The plugin binary is **not attached to GitHub releases** – this page is the changelog and the record of what changed in each build. App Store updates go through RWS review, so a brand-new fix can take a day or two to appear; if you are waiting on something specific, email support@supervertaler.com and I will send you the build directly.

| Also attached | What it is |
|---|---|
| `Supervertaler-MCP-Server.mcpb` | AI assistant extension for Claude Desktop (optional, see below) |
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for other local MCP clients, e.g. Claude Code (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, run QA checks, have translations drafted into the document, all from Claude's own chat window. To install: download the file, then in Claude Desktop open **Settings → Extensions → Advanced settings** and click **Install extension…** (double-click on the file also works if your system associates `.mcpb` with Claude; drag-and-drop does not). Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. Other MCP clients that run local servers (e.g. **Claude Code**) can use `Supervertaler-MCP-Server-exe.zip` instead: unzip it somewhere permanent and point the client's MCP config at the exe – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made snippet. Note: **ChatGPT's desktop app is not supported** – it runs MCP servers in the cloud and cannot reach a local Trados session ([details](https://docs.supervertaler.com/trados/mcp-server/)). [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

## [18.20.173 / 19.20.173] – 2026-08-11

### Fixed (terminology was silently missing from AI prompts when the TermLens panel had not been opened)

- **If you never opened the TermLens panel in a session, every AI prompt went out with no terminology at all** — Batch Translate, Batch Proofread, AutoPrompt, QuickLauncher and the Assistant chat alike — and nothing said so. Studio only starts a panel when it is first shown, and the terminology the AI is given comes from that panel: no panel, no terms. Reported by a user who noticed it while chasing something else. The plugin now starts TermLens itself at Studio start, so it follows the document and loads your termbases whether or not the panel is ever on screen. (This is the same fix the TermPicker pane got in 20.139, applied to the AI side.)
- **If it ever happens again, the plugin log says so.** A prompt that quietly carries no terminology looks exactly like a prompt the AI ignored, which is what made this so hard to spot.

### Fixed (startup notices opened behind the Trados window, and arrived in a heap)

- **The survey, the update notice and one-off announcements could open *behind* Studio**, where — since none of them appear in the taskbar — there was no way to reach them until they resurfaced. They are now attached to the Studio window, which cannot cover a window it owns, and are brought to the front when they appear.
- **They no longer stack.** Each notice used to open independently, so two could land on top of each other — which is why a survey sometimes appeared alongside an update notice when there was no new version. They are now shown one at a time, in order.
- **A notice is no longer lost when the TermLens panel is closed.** Each one waited for that panel to exist and gave up after fifteen seconds if it did not, which would have suppressed every notice for exactly the users helped by the terminology fix above.

### Fixed (the survey's "Don't ask again" did not stick, and only silenced one question)

- **Ticking "Don't ask again" now means it.** The record was kept in the main settings file, which is written back whole from around 29 places, so anything holding a copy from startup would overwrite it on the next unrelated save. Two narrower fixes were tried first — reloading before writing in 20.147, merging on save in 20.169 — and users kept reporting the question coming back. It now lives in its own small file where nothing else can touch it, the same treatment announcements got in 20.169. Answers recorded under the old scheme are still honoured, so nothing you have already answered will be asked again.
- **"Don't ask again" now retires surveys altogether**, rather than just the question in front of you. The checkbox carries no qualifier, and a user who ticks it has not asked to be surveyed about something else next month.

### Fixed (SuperSearch · Alt+S put a target term in the source box, and kept the last search's terms)

- **Pressing Alt+S with a selection in the target now searches the target box.** It used to put the selected target text into the *source* box, where — a target term not being in the source — it reliably found nothing.
- **The other box is now cleared.** Source and target are combined, so a term left over from the previous search silently cut the new one to zero results. Alt+S starts a search rather than refining the last one. (Both reported by a user.)

## [18.20.172 / 19.20.172] – 2026-08-10

### Fixed (SuperMemory · the `_shared` bank was invisible to everything except the AI)

- **`_shared` reported "0 articles" while holding three files, could not be searched, and switching to it emptied your active bank.** All three came from one line: bank names are cleaned before being turned into a folder path, and that cleaning strips a leading underscore — which is exactly how `_shared` is kept un-createable from the New-bank dialog. Applied to a name read back off disk it turned `_shared` into `shared`, and the plugin then went looking for a bank that does not exist.
- **Nothing was ever lost, and nothing was withheld from the AI.** Prompt injection reaches the shared bank by a different route, so your house defaults have been in every prompt the whole time. What was broken was everything that *reported* on the bank — which is worse than it sounds: a bank that says it is empty is a bank you stop trusting, and `search_supermemory` answered "no matches" for rules you had written down and were being applied.
- **Search now covers the shared bank as well as the active one.** Every result says which bank it came from, results from the active bank win ties (it overrides the shared defaults, so it should be read first), and an empty answer now names the banks it actually searched instead of implying you never wrote it down.
- **`list_supermemory_banks` no longer presents `_shared` as an ordinary bank.** Each entry carries its role — a project bank, or the shared layer that is loaded on top of whichever bank is active — so an assistant can no longer read `active: false` as "this knowledge is not in play".
- **The memory-bank dropdown explains what `_shared` is.** It stays selectable, because selecting it is how you edit your house defaults, and the toolbar's Open folder button works on whichever bank is active.

### Fixed (`check_tags` reported a phantom tag on every segment you had commented)

- **A Trados comment was being counted as an inline tag, so every commented segment failed the tag check** as "source has 0 inline tag(s), target has 1" — pointing at a tag that is not in the source, not in the target, and not visible anywhere in the editor. Found on a 213-segment patent with 15 comments, which produced exactly 15 findings; the only clue that they were phantoms was that the two counts matched.
- **Why it happened.** A comment is markup wrapping the commented text, and Studio renders it as a coloured highlight rather than as a tag. The plugin's serialiser had no case for it and fell through to its catch-all, which turns any unrecognised wrapper into a paired tag.
- **The same phantom was being shown to the AI.** `get_segments` returned the comment as a `<t1>…</t1>` around target text the translator sees unmarked, which an assistant would then dutifully carry into its own translation. Comments are unaffected by the fix: they live only in the target and were already carried across a write separately.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
