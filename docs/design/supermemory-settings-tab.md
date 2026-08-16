# A SuperMemory tab in Settings

**Status:** design, not started
**Date:** 2026-08-16
**Prompted by:** needing somewhere to put the reference-images folder button
(step 1 of `HANDOFF-image-context.md`), which turned into a better question:
why is the memory bank the one thing you cannot see from inside the plugin?

## The gap

A memory bank is a folder of Markdown files. Today the only ways to look at one
are Explorer, Obsidian, or the **Open folder** button on the SuperMemory
toolbar, which just launches Explorer. Everything else about the bank is
inferred: the toolbar dropdown lists bank *names*, the Report button summarises
sizes and token counts, and the chat mentions what it loaded.

That is a legibility problem, and most of today's SuperMemory work was the same
shape:

- files in a bank were silently inert, because the reader used an allow-list
  (defect E)
- `get_supermemory_context` reported the bank it *used*, which read exactly like
  the bank you *asked for* (defect C)
- content was trimmed to fit a budget with nothing saying so
- Quick Add wrote to "the active bank", named only in the confirmation

Each was fixed at the source. But a user who could *see* the bank would have
caught every one of them in seconds, which is the argument for this tab beyond
any single feature.

It also closes a gap already recorded in `CLAUDE.md`: renaming and deleting
banks is listed as not implemented, with the workaround being "rename or delete
the folder under `memory-banks/` directly with Trados closed".

## Should this live in the Prompts tab instead?

**No.** Considered and rejected, along with renaming that tab to something like
"Library".

The two look alike — both are trees of Markdown files the user edits by hand —
and the temptation is to build the tree once. But they answer different
questions:

| | Prompts | Memory banks |
|---|---|---|
| Question | what instructions do I give the AI | what does the AI know about this client |
| Chosen | per run | per client, changes rarely |
| Lifecycle | one personal library across all jobs | one per client or filing, grows during a job |
| Shape | arbitrary nesting, categories in frontmatter | fixed skeleton + `reference/` |
| Operations | new, edit, delete, restore defaults, set active, assign QuickLauncher slot, reorder | new, rename, delete, open folder, switch active, harvest, report |

Only "edit this file" is common to both.

Two specific hazards in merging:

**Two actives in one panel.** Both have an "active" concept marked in the tree.
Defect B (2026-08-16) was a user unable to tell which prompt was active because
one control gave two answers. Adding a second, different "active" to the same
surface invites the same confusion.

**It buries the brand.** SuperMemory is the name used in chat banners, the
Reports tab, the help menu and the marketing. Making it a subfolder of a generic
"Library" tab demotes the product's most distinctive idea.

**What the instinct is right about** is the duplication. `PromptManagerPanel` is
1,864 lines, and writing a second one would be waste. The answer is to extract
the tree-plus-preview pattern as a shared control that both tabs host — same
interaction, same code, each tab still about one thing.

## Scope

### The tree

Two levels, which is simpler than the prompt library's arbitrary nesting:

```
_shared                    (always loaded underneath the active bank)
  brief.md
  terminology.md
  style.md
brants-bari-001-be-ep      (active - marked)
  brief.md
  terminology.md
  style.md
  figures.md               (optional; written by the reference-images feature)
  reference/               (audit trail - never read into a prompt)
    …
other-bank
  …
```

Two things the tree should say that nothing currently does:

- **which bank is active**, and that `_shared` is loaded alongside it rather
  than being an alternative to it
- **which files are read into prompts.** `reference/` is deliberately not, and a
  user who does not know that will keep putting things there and wondering why
  the AI ignores them. Show it greyed, or under a heading that says so.

### The detail pane

Content of the selected file, editable, saved on leaving the node or on an
explicit Save. Read-only would be the cheaper first cut and still worth
shipping — seeing the bank is most of the value — but hand-editing is how these
files are meant to be maintained, so editing is the target.

### Actions

| Action | Notes |
|---|---|
| New bank | exists already on the toolbar; reuse `UserDataPath.TryCreateMemoryBank` |
| Rename bank | **new** — currently requires closing Trados and renaming the folder |
| Delete bank | **new** — same. Needs a confirm naming the bank, and must refuse the active one or switch away first |
| Open folder | exists; keep, for Obsidian users |
| Refresh | re-read from disk; the files are edited outside the plugin |
| Set active | duplicates the toolbar dropdown, but is the obvious gesture here |

### The reference-images row

A **Reference images** row with a Browse button, which is what prompted this
note.

`ReferenceImages.Suggest()` supplies the browse dialog's starting folder; it
never applies one on its own, because a folder guessed by walking up the tree can
belong to a different job (see that class's remarks).

**Scoped to the project, not the bank, and labelled to say so** — e.g.
*"Reference images for BRANTS (BARI-001-BE-EP)"*. This is the one place the tab's
bank-centric framing and the setting disagree, and the label has to carry the
difference.

## The open question: per project or per bank?

Genuinely unresolved, and worth deciding before the browse button is wired.

**Per project** (what `ProjectSettings.ReferenceImagesFolder` does today) is
always correct, because drawings belong to a job.

**Per bank** would be simpler in this UI and survives the Studio project being
deleted and recreated — and it is right *if* a bank maps 1:1 to a filing.

The observed banks do not agree with each other. `brants-bari-001-be-ep` and
`brants-trax-005-be-ep` are per-filing, so per-bank would be right for them.
`impala_hbm-machines`, `mahatta_genpact` and `taya` read as per-client, and would
span many jobs with different drawings — per-bank would then attach one job's
figures to all of them.

So per project is the safer default: it is never wrong, only sometimes
repetitive. Revisit if it proves annoying in practice.

Related, from the handoff and still open: sibling BE/EP filings share drawings.
Per-project handles that naturally — both projects point at one folder — which is
another point in its favour.

## Build order

1. **Extract the shared tree-plus-preview control** from `PromptManagerPanel`.
   Behaviour-preserving; the Prompts tab must look and act identically after it.
2. **SuperMemory tab, read-only.** Tree, content preview, active-bank marking,
   the `reference/` distinction. Shippable and useful alone.
3. **Editing** in the detail pane.
4. **Rename and delete banks.** Closes the `CLAUDE.md` gap.
5. **The reference-images row.** Unblocks `HANDOFF-image-context.md` step 1.

Step 1 is the risky one, because it touches a working 1,864-line control that
had a defect this morning. Do it on its own, and check the Prompts tab against
its current behaviour before moving on.

Steps 2 and 5 are what the images feature needs; 3 and 4 are independently
valuable and can wait.

## Risks

**Editing files the AI reads.** The detail pane writes files that go straight
into prompts. A save that corrupts `terminology.md` degrades every subsequent
translation quietly. Straight text editing, no clever parsing on save.

**Two editors, one file.** These banks are shared with Obsidian and the Python
assistant, and the plugin does not lock them. Re-read on focus, and do not
assume the buffer is current.

**Extraction regressions.** Step 1's whole risk. The Prompts tab is
load-bearing — the QuickLauncher menu, Batch Operations and the active-prompt
selection all read from it.
