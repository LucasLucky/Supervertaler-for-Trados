# Settings: one source of truth

**Status:** design, not started
**Date:** 2026-08-16
**Prompted by:** defect D in `BUGS-supervertaler.md` — the active memory bank
does not stay switched — traced to a defect that is much wider than SuperMemory.

## The defect

`TermLensSettings.Save()` serialises the **whole object** to `settings.json`.
Five components each hold their own long-lived copy:

| Component | Copy | `Save()` calls |
|---|---|---|
| `AiAssistantViewPart` | `_settings` | 9 |
| `TermLensEditorViewPart` | `_settings` | 10 |
| `TermLensSettingsForm` | `_settings` (handed in) | 3 |
| `TermbaseEditorDialog` | `_settings` (handed in) | 1 |
| `TermPickerDialog` | `_settings` (handed in) | 1 |

Whichever copy saves last wins, and silently reverts every field another copy
changed since it was loaded. Across the codebase there are **56 `Load()` sites
and 29 `Save()` sites**; most loads are transient and harmless, but the
long-lived ones above are not.

### The observed sequence

1. `TermLensEditorViewPart` loads its copy at start-up: bank = **TRAX-005**.
2. The user switches to **BARI-001** in the Assistant pane. The Assistant's copy
   is updated and saved. `settings.json` is now correct.
3. TermLens's copy still says TRAX-005. Nothing told it.
4. The user opens Settings **from the TermLens pane** and clicks OK. The dialog
   was handed TermLens's stale object, so the save reverts the bank.

No second MCP client is needed to explain this. The handoff's more alarming
hypothesis — global state shared between MCP clients, one silently changing
another's context — **is not what happens.** The active bank lives in a
per-process settings object; a second client reporting the old bank was reading
the reverted value, not causing it.

### Same root cause as defect A

`AiAssistantViewPart.NotifySettingsChanged()` is documented as *"Called by
TermLensEditorViewPart after its settings dialog saves, so this ViewPart picks
up changes made there."* The synchronisation is hand-wired and one-directional:
TermLens tells the Assistant, and nothing tells TermLens. Defect A — "which pane
you opened Settings from decides whether a new prompt appears in the dropdown" —
is this same defect seen from the other end.

### Scope

This is not a SuperMemory bug. **Any** setting written by one component can be
reverted by another: API keys, termbase Read/AI ticks, batch size, provider
choice. The memory bank is simply where it was noticed, because that is where
the consequence is legible — the wrong terminology in a finished translation.

## Why "make every gear icon open the same dialog" is necessary but not sufficient

It is the right instinct and it is part of the fix. But the dialog is not the
only writer. These save without any dialog involved:

```
AiAssistantViewPart:8367   aiSettings.SelectedPromptPath = …; _settings.Save();
AiAssistantViewPart:8575   try { _settings.Save(); } catch { }
AiAssistantViewPart:9100   _settings.Save();
TermLensEditorViewPart:1305  _settings.Save();
TermLensEditorViewPart:1349  _settings.Save();
```

Unify every gear icon and TermLens still holds a stale object that clobbers the
file the moment it saves for its own reasons. **The shared thing has to be the
settings object, not the dialog.**

## Design

One owner. A `SettingsService` holding the single instance:

```
SettingsService.Current      // the one instance; never null
SettingsService.Save()       // writes it, then raises Changed
SettingsService.Reload()     // re-reads from disk, then raises Changed
SettingsService.Changed      // event; panes refresh instead of being hand-wired
```

Three properties matter:

**No component keeps its own copy.** `_settings` fields become
`SettingsService.Current`, so "stale copy" stops being expressible.

**`Changed` replaces the hand-wiring.** `NotifySettingsChanged()` exists because
one pane had to know about another. With an event, a pane subscribes and
refreshes itself, and adding a sixth consumer costs nothing.

**Saves are serialised.** The MCP bridge reads settings on `HttpListener`
threads while the UI writes on the UI thread. `Save()` takes a lock; reads of
whole subtrees that must be self-consistent take a snapshot.

### What this does not change

`TermLensSettings` itself, its shape, or the file format. This is about who owns
the instance.

## Staging

Each stage is independently shippable and testable.

1. **Introduce `SettingsService`.** Wraps a single `TermLensSettings`, exposes
   `Current` / `Save()` / `Reload()` / `Changed`. Nothing uses it yet.
2. **Convert the two ViewParts.** `AiAssistantViewPart` and
   `TermLensEditorViewPart` drop their `_settings` fields. **This alone fixes
   defects A and D**, and is where the risk concentrates — 19 of the 29 save
   sites are in these two files.
3. **Route the Settings dialog through the service** rather than being handed an
   object, so every gear icon genuinely opens the same thing.
4. **Convert the three dialogs** that are handed a settings object.
5. **Audit the transient load-modify-save sites** (`NewTermbaseDefaults`,
   `UsageStatistics`, `AppInitializer`, `SurveyState`, …). These are lower risk —
   they load, change one field and save immediately — but they can still lose a
   write that lands between their load and their save.
6. **Make the write path the only way in.** Once nothing needs a private copy,
   `TermLensSettings.Load()` becomes internal to the service.

Stages 1–3 carry nearly all the value. Stages 4–6 are cleanup.

## Risks

**A shared mutable object changes aliasing.** Today a component can mutate its
copy and only affect the file on `Save()`. Afterwards, a mutation is immediately
visible everywhere. Any code that mutates settings *speculatively* — to compute
something, or before a user cancels — becomes a live edit. **Stage 2 must audit
for this specifically**; a dialog that mutates on field-change and relies on
"don't save" as the cancel mechanism would start applying changes on Cancel.

**Do not make `Load()` return the shared instance as a shortcut.** It is
tempting — 56 call sites would migrate for free — but callers that expect a
private copy would silently start writing global state. Convert deliberately.

**Threading.** Bridge threads read settings today with no synchronisation and
get away with it because each holds its own copy. One shared instance makes
concurrent read-during-write real.

**Regression surface is wide and quiet.** These settings govern API keys,
termbase enablement and provider choice. A fault here is another silent-wrong
failure, which is the class of bug this whole exercise exists to remove. Worth
testing each stage against: switch a bank, change a TermLens setting, open
Settings from each pane in turn, confirm nothing reverts.

## Stage 2 aliasing audit

**Done 2026-08-16.** The question this had to answer: which code mutates the
settings object *before* the user commits, and would therefore start applying
changes on Cancel once the instance is shared?

Every write was checked — assignments *and* in-place collection mutations
(`.Add` / `.Remove` / `.Clear`), which a search for `_settings.X =` alone
misses and which are equally live under a shared instance.

### Safe to convert as-is

| Where | Why |
|---|---|
| `TermLensSettingsForm` lines 1874–1970 | One block immediately before `Save()` — the commit handler. Nothing is written until OK. |
| `TermLensSettingsForm` 2113–2114 | Window geometry, saved on close. Should persist regardless of Cancel. |
| `TermbaseEditorDialog`, `TermPickerDialog` | Geometry and column widths only, saved immediately. Same reasoning. |
| Both ViewParts | Panes, not dialogs. Their writes are committed user actions — there is no Cancel to respect. |

### Two genuine pre-commit writes

**`TermLensSettingsForm` 1115 / 1119 — remembered confirmation.**
Ticking Write on a termbase whose language pair does not match the project
raises a confirm prompt; answering Yes appends the termbase name to
`ConfirmedNonMatchingWriteTermbaseNames` so it is not asked again. Today,
pressing Cancel afterwards discards that. With a shared instance it would
persist.

Minor, and arguably an improvement — the user *did* answer the question — but
it is a behaviour change and should be a decision rather than a side effect.

**`TermLensSettingsForm` 1573 / 1575 — clearing references to a deleted termbase.**
Deleting a termbase in the dialog calls `TermbaseReader.DeleteTermbase`
immediately and irreversibly, then removes the id from `WriteTermbaseIds` and
clears `ProjectTermbaseId`. Today, Cancel discards *only the cleanup*, leaving
settings pointing at a termbase that no longer exists.

So here the shared instance is **more correct than the current behaviour**, and
converting fixes a latent bug rather than causing one.

### The workaround already in the tree

Both ViewParts contain a hand-rolled field-copy after the settings dialog
closes — ten named fields copied from a fresh `TermLensSettings.Load()`:

```
TermLensEditorViewPart:1152-1161
AiAssistantViewPart:532-541
TermLensEditorViewPart:2153, 3287   (further one-off syncs)
```

Somebody hit this staleness before and patched it by enumerating the fields
that mattered at the time. It cannot work: it runs *after* the dialog has
already saved the stale object, so it re-syncs the in-memory copy to a file
that has meanwhile been reverted. It is the mechanism that makes the bug hard
to see, because the pane's own display ends up agreeing with the wrong file.

All of it is deleted by stage 2, and its presence is the strongest argument
that the copies themselves are the problem.

### Conclusion

Stage 2 carries no blocking risk. Two pre-commit writes exist; one is trivial
and one is a fix. The audit's main finding is that the existing workaround must
be **removed**, not adapted — leaving it would re-introduce a second source of
truth inside the very change meant to remove it.

## Stage 3: one way in

**Done 2026-08-16.**

The stated goal was "every gear icon opens the same settings". The audit for
stage 2 had found three openers. There were **five** — the two panels, the About
box's licence link, the QuickLauncher menu header, and the settings form's own
construction. Two of the five had been missed because the call was
namespace-qualified and wrapped across lines, which a search for
`new TermLensSettingsForm` does not match.

That mattered, because the two missed ones were the worst:

| Opener | What it passed | Refresh afterwards |
|---|---|---|
| `AiAssistantViewPart` | its copy (shared since stage 2) | itself, then TermLens by hand |
| `TermLensEditorViewPart` | its copy (shared since stage 2) | itself, then the Assistant by hand |
| `AboutDialog` licence link | **a fresh `Load()`** | **none** |
| `QuickLauncherAction` header | **a fresh `Load()`** | **none** |

So opening Settings from the About box or the QuickLauncher took a private copy
of the file, saved the whole of it, and reverted anything either panel had
changed since — with nothing refreshed afterwards, so the panels went on
displaying values the file no longer held until Studio restarted. Stage 2 could
not have fixed these: it removed the panels' copies, and these two never used
them.

### What changed

`SettingsDialog.Show(owner, promptLibrary, defaultTab)` is now the only way in.
`TermLensSettingsForm`'s constructor no longer takes a settings object at all —
it reads `SettingsService.Current` — so passing a private copy is not something
a future call site can express. The tab index is the only thing a caller still
decides.

The refresh moved into `SettingsDialog` and now runs for **both** panels
whichever icon was clicked, replacing the hand-wired
`NotifySettingsChanged` cross-calls, which existed in only two of the five
openers.

### Two behaviour changes worth naming

**TermLens's post-settings termbase reload is now always in the background.**
The inline block and the static notify method had drifted: the inline one used
`Task.Run`, the static one ran on the UI thread. Whether Studio froze for ~2
minutes on a cold Studio 2026 cache therefore depended on which gear icon you
had used. The background version won.

**The AI Assistant's memory-bank dropdown now refreshes whichever icon was
used.** Its inline block omitted `RefreshMemoryBankDropdown`, so a bank created
inside the dialog appeared only if you had opened Settings from the *other*
panel.

### Why the refresh is not hung off `SettingsService.Changed`

`Changed` fires on every save — including the A+/A− font buttons and the chat
font slider. TermLens's refresh forces a full termbase reload. Subscribing would
make an idle font tweak trigger a two-minute reload. `Changed` stays for
refreshes cheap enough to run on an arbitrary write; the expensive one is asked
for explicitly.

### Left for stages 4-6

`TermbaseEditorDialog` and `TermPickerDialog` are still handed a settings object
(stage 4), and there are still **48** `TermLensSettings.Load()` call sites, most
of them transient load-modify-save (stage 5). Those can each still lose a write
that lands between their load and their save — a much narrower window than the
defect fixed here, but the same shape.

## Not in scope

Defect B — "active prompt" having two sources of truth (Settings pin vs the
Batch Operations selector) — is a *product* question about what "active" means,
not an artefact of this defect. It should be decided separately, though it will
be much easier to implement once there is one settings owner.
