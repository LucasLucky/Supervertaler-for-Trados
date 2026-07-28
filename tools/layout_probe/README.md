# Layout probe

Catches broken WinForms dialog layouts before users see them.

The plugin's dialogs display text that isn't known at build time — survey
questions are typed into the admin dashboard per question, and button labels
come with them. A layout that fits today's wording can clip tomorrow's. On top
of that, users run every combination of screen resolution, DPI scaling and
system font size, so "it looked right on my machine" proves very little.

This script loads the built assembly, constructs each dialog **off-screen** with
deliberately long realistic text, forces a real layout pass, then measures every
control and reports:

- **overlapping controls** — two controls occupying the same pixels
- **anything outside the form** — spilling past the bottom or right edge
- **clipped label text** — a fixed-size label whose text needs more height than it has

## Running it

Trados Studio does **not** need to be running.

```powershell
dotnet build src/Supervertaler.Trados/Supervertaler.Trados.csproj -c Release -p:TradosStudioVersion=18
pwsh -File tools/layout_probe/layout_probe.ps1
```

Studio 2026 build instead:

```powershell
pwsh -File tools/layout_probe/layout_probe.ps1 -StudioVersion 19
```

Exit code is 0 when every dialog passes and 1 otherwise, so it can gate a
release.

## Adding a dialog

Append to the `$cases` array. Give it the **longest realistic text** you can
imagine a user seeing — short strings pass trivially and prove nothing.

## Why the clipping check only looks at fixed-size labels

An `AutoSize` label grows to fit its text; `MaximumSize` caps its width but lets
its height grow, so it wraps rather than clips. Measuring such a label at its
own resulting width just re-wraps text it already fitted, reporting a problem
that doesn't exist. Only labels with a fixed `Size` can actually truncate.

## History

Written after the survey dialog shipped with its question cut off mid-sentence,
and then — after a first fix that hand-computed positions from measured heights —
with controls overlapping each other. Both were spotted from screenshots after
the fact.

The underlying lesson is in the dialog source, not here: **don't compute
coordinates**. Both dialogs now use `TableLayoutPanel` with auto-sizing rows and
`Margin` spacing, so WinForms measures them *after* the DPI scaling pass instead
of working from numbers frozen in beforehand. Mixing measured sizes with
hardcoded pixel offsets is what produced the overlap, and it gets worse the
further a user's display scaling is from the developer's.
