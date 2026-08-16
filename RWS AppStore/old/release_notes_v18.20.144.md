# RWS App Store Manager - v18.20.144

Two builds ship from this one release (identical feature set, distinct
version numbers so the App Store never sees a collision):

| Build | Version number | Min studio | Max studio | Checksum (SHA-256) |
|-------|----------------|------------|------------|--------------------|
| Studio 2024 | `18.20.144.0` | `18.0` | `18.9` | `2082bba02c73e7b34455d66ddf6db19033950a57a1b8572de587f56f6eb538ad` |
| Studio 2026 | `19.20.144.0` | `19.0` | `19.0.9` | `af09efb69a296663637205457b9df85d02a7025e7dfe4754745e35a9e2766ee6` |

---

## ⭐ Highlights

**TermPicker can now be docked.** The list view of your segment's terminology is no longer only a popup: open it from Studio's **View** tab and keep it permanently visible, wherever you like to put it. Both terminology views are now available in both placements – TermLens as a docked panel or at the cursor (tap **Ctrl**), TermPicker as a docked pane or at the cursor (**Alt+P**) – so you can choose the view you prefer independently of where you want it. TermPicker also gained a proper keyboard set (**I** for term details, **E** to edit, arrows, ←/→ for synonyms, Enter to insert), an amber dot marking terms that carry a definition or notes, and the same right-click menu as the TermLens chips.

**The GPT-5.6 family is available** (Sol, Terra and Luna, released 9 July 2026). GPT-5.6 Sol costs exactly the same as GPT-5.5 and supersedes it; **Terra offers GPT-5.5-class quality at half the price**, which makes it a strong everyday default.

**Two silent bugs fixed, both found by users on real jobs.** The Proofread prompt could report its verdicts against the wrong segment numbers, and AutoPrompt could time out on slower OpenAI models. Details below.

---

## Changelog

### Added
- **GPT-5.6 Sol, Terra and Luna are now selectable** (Settings → AI Settings). OpenAI released this three-tier family on 9 July 2026, all with a ~1M-token context window:
- **Sol** – the flagship, for complex translation and AutoPrompt. **$5/$30 per million tokens: the same price as GPT-5.5, which it supersedes**, so there is no reason to stay on 5.5 for quality work.
- **Terra** – GPT-5.5-class quality at **$2.50/$15**, half the price. A strong everyday default.
- **Luna** – **$1/$6**, for high-volume batch work.
- Pricing for all three is in the shared pricing list, so cost estimates and usage reports handle them out of the box. GPT-5.5 remains available and its prices stay listed, so existing projects and past usage logs still resolve.
- **Pressing `I` on a row in TermPicker shows the term's metadata** – the same popup, with the same content, as hovering a TermLens chip: forbidden / MultiTerm / non-translatable tags, and for every entry its synonyms, definition, domain, notes and URL. Press `I` again to dismiss it. Works in both the Alt+P popup and the dockable pane, and matches the `I` key that the TermLens popup has always had. TermPicker's keyboard set is now: arrows to navigate, ←/→ to collapse/expand synonyms, a term number to jump, **I** for details, **E** to edit, Enter to insert (and Esc to close the popup).
- **TermPicker can now be docked like TermLens**, for anyone who prefers a flat, sortable list as their permanent terminology display rather than TermLens's in-context chips. Open it from Studio's **View** tab (it is not pinned by default, so your existing layout doesn't change when you update). The pane updates on every segment change, in step with the TermLens panel, and inserting from it behaves exactly like the popup and the chips – same capitalisation adaptation, same keyboard grammar (arrows to navigate, Right/Left to expand/collapse synonyms, a term number to jump, Enter to insert).
- Both terminology views are now available in both placements: TermLens as a docked panel or at the cursor (tap **Ctrl**), TermPicker as a docked pane or at the cursor (**Alt+P**) – so you can choose the representation and the placement independently. **Alt+P still opens the popup** even when the pane is visible, mirroring how Ctrl-tap works alongside the docked TermLens panel.

### Changed
- **The pane now opens pinned**, i.e. permanently visible. Previously it arrived auto-hidden, sliding in and straight back out again, which looked like a glitch. Studio still remembers wherever you drag it afterwards.
- **Alt+P now moves focus into the pane when it is open**, instead of covering it with the popup: from there arrows navigate, ←/→ collapse/expand synonyms, a term number jumps to it, Enter inserts. With no pane in your layout, Alt+P opens the popup exactly as before.
- **Escape closes the TermPicker popup** (the list was swallowing the key).
- **Pressing E on a row opens the term editor**, matching the TermLens popup's key – in both the pane and the popup. MultiTerm entries are skipped, as those termbases are read-only.
- **TermPicker now opens with Alt+P** (was Ctrl+Shift+P). Ctrl+Shift+P is also Trados Studio's own *View Target*, so it appeared twice in Studio's keyboard-shortcut list and looked like a conflict. Alt+P is free. Note that Studio keeps your existing binding across plugin updates – if you had it on Ctrl+Shift+P, clear that and set Alt+P under **File > Options > Keyboard Shortcuts > Supervertaler for Trados**.
- **TermPicker opens with every synonym group already expanded**, so a single Alt+P shows all alternative translations at a glance instead of hiding them behind collapsed markers. Left/Right still collapse and re-expand individual groups.
- The **About** dialog's shortcut list now includes Alt+P (TermPicker) and Ctrl+Alt+V (voice commands), and its entry for *Translate active segment* is corrected to **Alt+T** – it still showed the old Ctrl+T, which was replaced in 20.119 because it collides with Trados's *Apply Translation Result*.

### Fixed
- **GPT-5.5 via OpenRouter** had the same problem as the direct OpenAI route and is now recognised as a reasoning model too.
- **Any GPT-5.x model ID** – including one you type in yourself as a custom model, and the GPT-5.6 family – now gets the long timeout automatically, instead of only the older o-series being recognised.
- **AutoPrompt failed with "The request timed out." on GPT-5.5** (reported by a user; GPT-5.4 Mini worked fine on the same job). AutoPrompt asks the model for a large amount of output, and the OpenAI request paths allowed a flat two minutes for it regardless of how much was requested – where the Claude paths have always allowed ten minutes for large generations. All OpenAI paths now scale the same way, based on the size of the request rather than on a list of known-slow models, so this keeps working for models released after this build.
- **GPT-5.5 is now recognised as a reasoning model**, so every request to it gets the longer timeout, not just large ones.
- **AI request timeouts are now recorded in the diagnostic log**, and the error message suggests trying a faster model or sending less context. Previously a timeout left no trace in the log at all, which made it impossible to diagnose from a bug report.
- **The clipboard Proofread prompt numbered its review list from 1 instead of using the real segment numbers**, so as soon as any segment was skipped – a tag-only segment, for instance – every number after it was wrong, and the AI's verdicts were reported against the wrong segments. Nothing looked amiss: the output was well-formed and only a manual comparison revealed the drift. Found on a real 949-segment job where three tag-only segments pushed 826 of 946 verdicts three segments out of place. The batch now uses the same `[SEGMENT NNNN]` document numbers as the document-context block (and as the API path, which was never affected), and states that the numbers are deliberately non-contiguous.
- **The prompt also specified its output format twice, in two different ways** (`[SEGMENT 0002] ISSUE` with `Issue:`/`Evidence:`/`Suggestion:` versus `Segment 2: ISSUE` with `Problem:`/`Suggestion:`). A model following the second one dropped the evidence citations the first one asks for. The format is now defined once.
- **Escape now closes the term-details window** – in the docked pane and in the Alt+P popup alike. (Windows treats Escape as a dialog key, so it never reached the list; it is handled a level up now.) In the popup, a second Escape closes the picker itself. The details window also closes when you move to another row, so it can no longer describe the previous term.
- **The top row no longer flashes when you press Alt+P.** The list was hiding its selection while the editor had focus and redrawing it on arrival; the selected row now stays visibly selected (grey when unfocused, blue when focused). The list is also double-buffered, so rebuilding it on each segment change doesn't flicker.
- **The pane no longer starts empty.** If you kept TermPicker visible with the TermLens panel collapsed to a tab, the pane stayed blank until you clicked that tab: Studio only starts a panel when it is first shown, so TermLens wasn't yet following the document that the picker takes its matches from. The pane now starts TermLens itself, so it is populated the moment you open it.
- **You can now see which terms have details.** Rows whose term carries a definition, domain, notes or a URL are marked with an amber dot – the same signal the TermLens chips give – so it's clear when pressing `I` will show you something.
- **Escape closes the details popup** (previously it stayed on screen). In the Alt+P popup, a second Escape then closes the picker itself.
- **The right-click menu is back**: Edit Term, Mark as Non-Translatable and Delete Term, matching the TermLens chips. It acts on the row you right-click, and is disabled for MultiTerm entries, which are read-only.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases