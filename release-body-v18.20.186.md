> [!WARNING]
> **Did the update prompt inside Trados send you here to download?**
> Then your plugin predates **v4.19.24** and is still checking GitHub for updates.
> There is no plugin to download on this page - install once from the
> **[RWS App Store](https://appstore.rws.com/plugin/432)** (or *Add-Ins -> RWS App Store*
> inside Studio) and it will check there from then on, and stop warning you about an
> unsigned plug-in at every start.

Supervertaler for Trados **v18.20.186** (Studio 2024) / **v19.20.186** (Studio 2026). Covers 18.20.185 → 18.20.186.

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

## [18.20.186 / 19.20.186] – 2026-08-31

### Changed (the TermLens popup has a key of its own)

- ★ **The floating TermLens popup opens with Alt+L. The Ctrl tap is retired.** Pressing and releasing Ctrl on its own was a pleasant gesture and an unreliable trigger: once any other program consumes the middle key of a Ctrl-modified shortcut, what reaches Studio is a bare Ctrl press and release — indistinguishable from a deliberate tap. The popup then opened by itself, took the focus, and broke whatever the other program was in the middle of doing. This is not hypothetical and it is not rare: Supervertaler's own voice commands hit it in 20.132 and needed a synthetic-keystroke guard written specially to work around it. Every keyboard tool, text expander and macro utility a translator runs alongside Studio hits the same thing, and none of them can reach that guard. Diagnosed against one such tool, where a single hotkey press left the popup open and the tool's own copy landing in a window with nothing selected in it.
- **Escape still closes the popup**, by the low-level hook it has always used — the tap detector was never what did that.
- **Alt+L rather than a three-key chord**, because this is a key you press constantly, and beside **Alt+P** for TermPicker, its sibling. Note that Alt is the ribbon's KeyTip prefix, so an Alt+letter can collide with a ribbon command that appears nowhere in Studio's keyboard settings; the letters already used this way — P, Q, S, T, W, and the digits — are known good.
- **An existing installation keeps whatever it had bound**, Studio storing shortcuts per user, so this default reaches fresh installs only. To take it on an existing one: **File → Options → Keyboard Shortcuts → Supervertaler for Trados**, find *TermLens: Show TermLens popup*, and press Alt+L. The row turns red if something in the same scope already holds the key.

### Fixed

- **The Import/Export status checkboxes no longer clip each other.** The six confirmation-status boxes sit in fixed 180px columns while sizing themselves to their own text, so "Approved (translation)" overran its column and left "Approved (sign-off)" with a half-drawn checkbox beside it. The column is now measured from the widest label plus the glyph, so it holds at any DPI scaling and whatever the labels are changed to.

### Changed (shared code, no change in behaviour)

- **The LLM client, the prompt library, the prompt and term models, the prompt generator and the document analyser now live in Supervertaler.Core**, a submodule shared with the forthcoming memoQ plugin, rather than being reimplemented per plugin. Nothing about the plugin behaves differently and nothing new ships inside it: the code is compiled in as source rather than referenced as a library, so the package contains the same twelve DLLs it did before. It is listed here because it is a large structural change, and because the point of it is that a fix to the prompt library now reaches every Supervertaler product at once instead of one of them.

## [18.20.185 / 19.20.185] – 2026-08-27

### Added (Supervertaler can look at your drawings)

- ★ **Supervertaler now reads the reference signs printed on your figures, and tells you which ones appear nowhere in the text.** On a patent, a reference sign carried in the drawings with no basis in the description is an Art. 84 / Rule 42 objection — the kind of thing an attorney wants raised before filing. Until now the only way to find one was to open every drawing and check it by eye. On the job this was built against, it found **ST 05**, printed on two figures and absent from all 354 segments. No amount of searching the text could ever have found it: it exists only as pixels inside the image.
- **Analyse images** on the Batch Operations tab does the whole thing in one go: pulls the images out of your Word documents, shows each one to the AI together with what the document says about it, and writes the result to `figures.md` in your active memory bank — where it is read into every prompt from then on. One AI request per image.
- **The AI is asked what the drawing shows and what is legible on it, not what the invention does.** A plausible-sounding summary of a mechanism is exactly the kind of wrong that survives review, so the question is deliberately narrow.
- **`figures.md` says which parts of it came from the document and which came from the AI**, and asks you to correct anything wrong — because from the moment it exists, it is in every prompt. A mistaken caption would otherwise be repeated into every request silently.
- **Extract images to folder** writes your document's images out as `Figure 01.png`, `Figure 02.png` and so on — zero-padded so they sort properly, original format preserved, and re-running simply overwrites. Verified against a set a translator had extracted and named by hand: fourteen files, byte-identical, same names.
- **Document images** reports what your project's Word files actually contain — each image, its figure label, and what the document says it shows — without calling the AI at all.
- **Reference numerals now finds more than `(12)`.** Lettered points such as `(A)`…`(W)` and label-series signs such as `ST 01` are recognised alongside parenthesised numerals, and `N°7` is understood as another way of writing `(7)` rather than counted as a separate part.

### Added (MCP server – bulk terminology, glossary files, and the fuzzy bands)

- **Your AI assistant can add many terms in one call.** `add_term` now takes an `entries` array of up to 40 term pairs, so locking a 48-term glossary into a project termbase is two calls rather than 48. Each pair is decided on its own: a duplicate or a failure on one does not stop the rest, and the response says what happened to every entry in order, including which termbase each reached and in which direction. Which termbases to write to, and whether to stay inside the project termbase, remain settings for the whole call – so a batch cannot leak into a background termbase 40 rows at a time.
- **Client glossaries that only exist as spreadsheets can now be imported.** `import_project_termbase` reads a `.csv`, `.tsv` or `.txt` export from memoQ or Excel, not just a Trados termbase. The delimiter is detected, and a column headed with a language name – `Dutch`, `English` – is recognised as the source or target side, with `<language> synonyms` columns read as alternative spellings. You must say which language is which, because a text file carries none of that and guessing would write every pair backwards. Always dry-run it first: for a file format with no rules, that report is the only place a wrong column can be caught before it is in your termbase – and it also flags invisible characters such as a non-breaking space inside a term, which would otherwise stop that term ever matching.
- **Analysis figures now come back as separate fuzzy bands**, not one lump. `get_project_statistics` still reports the total, and adds each band with its own range. The 95-99% band is the one that matters: a match that high reads fluent and plausible while differing from the source in exactly the load-bearing words – on a patent, an ordinal, a reference letter or a claim back-reference. Knowing there are seven such segments carrying 207 words is something you can act on; knowing there are 27 fuzzy segments somewhere between 50% and 99% is not.

### Fixed (the memory bank follows the project now)

- **Your active memory bank no longer follows you from one project into the next.** It was remembered per installation rather than per project, so opening a different client's job left you pointed at the previous client's bank – and because the bank feeds every prompt, that quietly supplied the wrong terminology and style to every request with nothing on screen to say so. The bank is now remembered per Trados project: choose one and it sticks to that job. A project with no bank recorded now uses **none**, and says so, rather than inheriting whichever one you had open last – no bank is better than another client's bank.
- **And it now actually sticks.** Three things stood between the setting and the behaviour, each silent on its own. The bank was realigned from one panel while the project it aligned to was tracked by *another*, on the same event with no ordering between them — so the bank lagged exactly one project behind on every switch. Only the SuperMemory dropdown recorded your choice: pick a bank from the Library tab's **Set as active** and it was forgotten the moment the project changed. And renaming a bank left every project still naming the old one, which resolves to no bank at all. The project is now read from the document itself, all three ways of choosing a bank record it, and a rename carries every project with it. A fourth, found by watching the files rather than reading the code: the choice was being written correctly and then blanked seconds later, because saving a project's settings rebuilds the whole file from your global settings and drops anything that belongs to the job rather than to the installation — and that save runs on every project switch. So choosing a bank and then leaving the project erased the choice between those two actions.

### Fixed (termbase direction, and a check that could hide its own findings)

- **The prompt dropdown on Batch Operations was hiding the prompts it shipped with.** Picking Proofread offered nothing but "(None – default)", even though the Proofread folder in your library was sitting there with prompts in it. The dropdown matched a prompt's folder name exactly, and the prompts that come with the plugin live one level down – in Proofread/Default – so none of them matched. Prompts in any subfolder now count, so everything under Proofread appears when you choose Proofread, and everything under Translate when you choose Translate. This affected Translate too: on a fresh install, where the only prompts are the ones supplied, both dropdowns were empty. Reported by a user who could see three prompts in the folder and none in the list.
- **The Studio 2026 build no longer stops working when Studio updates.** It declared that it needed a Studio version between 19.0 and 19.0.9, so the first 19.1 update would have taken it out of Studio's plugin list without warning and made every reinstall appear to succeed and change nothing – a failure with no error message anywhere. The Studio 2024 build always allowed the whole 18.x range; the 2026 build now allows the whole of 19.x to match.
- **The term editor could label a term's languages wrongly.** Opening a term that exists in several termbases and switching between them reloaded the terms but left the language labels describing the termbase you had just left – so on a Dutch-to-English job an English-to-Dutch termbase showed its English term under "Dutch" and its Dutch term under "English". Nothing was ever wrong in the termbase itself; only the labels were. That matters more than it sounds, because the natural response to seeing it is to swap the two fields and save, which would break an entry that was correct all along.
- **The two language columns now stay the same width, with each label over its own field.** They were laid out correctly and then drifted apart as soon as the dialog was resized, because the left column stretched, the right one only slid sideways, and the labels did not move at all.
- **A terminology check narrowed to one termbase could hide that termbase's own findings.** When a longer term from an excluded termbase overlapped a shorter one from the termbase you asked about, the longer one claimed the words first and the shorter one was never looked at – and the check then reported a clean result. Restricting a check to a client termbase is precisely when you are relying on it to be complete.
- **Your AI assistant can now edit a term named either way round.** Every place a term is shown to you – the terminology check, TermLens, the assistant's own context – presents it in your project's direction. A termbase whose declared languages are the reverse of the project's stores it the other way, so asking the assistant to change a term it had just shown you was refused as "no entry found". It now matches on both terms whichever column each sits in, and a rename you give in that same order is written in the termbase's own order, so correcting a term cannot silently reverse it. The reply says when an entry was stored the opposite way from how you named it.
- **An edited term now records when it was edited.** Changing a term's note through the AI assistant left its "modified" date at the day it was created, so an entry rewritten today read as untouched for months.

### Fixed (figure labels, and how the reports read)

- **Figures could be labelled wrongly, and the report said everything was fine.** Where several images shared a paragraph, all of them took that paragraph's label — so four figures came out as "FIG. 3", three figure numbers were never assigned to anything, and the summary called it "the easiest case there is". Images and labels are now paired by position and the pairing is *checked*: if the numbers do not line up, no labels are applied and the report says what it counted. A wrong figure label is invisible downstream and corrupts everything built on top of it, so refusing is better than guessing.
- **Word writes a floating text box twice**, once for modern Word and once for old versions, which made figure labels count double — 21 labels for 14 images on a real document, enough to make the check above refuse the whole file.
- **A figure's description is matched by its number, not by what sits next to it.** On a patent the plates are at the back and their descriptions are hundreds of paragraphs away in the body, so "the text around the image" is the figure label and some blank lines. All fourteen figures on the test document now carry the description that actually describes them.
- **The same description no longer appears twice** when the figure list and the detailed description differ only by a full stop — while genuinely different wordings are both kept, because the longer one usually names the parts.
- **Reference numerals: the citation shown for each numeral now contains that numeral.** The preview was cut from the start of the segment, so on a real patent 14 of 20 rows showed a snippet in which the row's own numeral had been cut off.
- **Reports read properly in the panel.** They were being laid out as tables and then flattened, repeating the column headings on every row; they are now written for the width they are shown at. `figures.md` keeps its table, because it is read full width.
- **The Reference images folder can be set from the tab that uses it.** The setting existed only in Settings → Library on a memory bank, four levels into a dialog, where two people looking for it failed to find it.
- **Saving a chat to a memory bank no longer strips its formatting.** A table arrived as loose pipe-separated lines, and headings and bold were flattened — in a file that is read as Markdown by Obsidian and by Supervertaler itself.
- **Saving a chat no longer tells you to run a command that does not exist.** The confirmation asked you to "run Process Inbox", which was removed long ago, to compile the note into a knowledge base that never reads that folder. It now names the file it wrote and says plainly that the `reference` folder is the audit trail and is not read into prompts.
- **Long AI replies keep their "Show full response" link** when they arrive while you are on another tab, and are cut at a line break rather than mid-word.
- **The bilingual review export links to supervertaler.com** rather than the Trados sub-page.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
