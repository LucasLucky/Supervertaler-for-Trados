# Reference images: giving the AI what the pictures say

**Status:** design, not started. Supersedes the approach in
`HANDOFF-image-context.md` (2026-08-16) — that note's reasoning still holds for
patents, but its central assumption does not generalise. See "What changed".

**Date:** 2026-08-21

## The problem

A translator's source document contains pictures. The pictures carry
information the text does not, and sometimes that information decides a
translation. The question is how to get it to the model.

Two implementations exist, in two products, and **both assume the image has a
name that the text uses**:

| | What it does | Where |
|---|---|---|
| Reference-triggered attachment | when a segment says *"figure 3"*, attach `FIG. 3.png` to that request | Workbench, `figure_context_manager.py` |
| Numeral distillation | run vision once over the drawings, cache a numeral→part register as `figures.md`, inject the text | proposed in `HANDOFF-image-context.md`; not built anywhere |

For patents both work, because a patent is the ideal case: every figure is
numbered, every numeral is cited, and the citation is the link.

**For a document whose images have no names, both fail — not degrade, fail.**
A user manual with two unlabelled photos on page 3 offers nothing to trigger on
and nothing to put in a register. The feature is simply absent, and nothing says
so.

## The insight

There are three ways an image is linked to the text, and **only the first needs
a name**:

1. **Citation** — the text names it. *"as shown in figure 3"*. Patents, papers.
2. **Caption** — text attached to it, which is itself a segment being translated.
   Manuals, reports.
3. **Proximity** — it sits between these paragraphs, and nothing refers to it at
   all. Everything else.

So the unit of this feature should not be *"a folder of images with figure
numbers"*. It should be **an image with an anchor**:

```
image     the file
label     "FIG. 3", "Table 2", or none
caption   the caption text, or none
anchor    the source text it sits among  <- the part that always exists
```

Citation-based lookup then stops being the mechanism and becomes a special
case: when a label exists and the text cites it, use that; otherwise fall back
to the anchor, which is always there.

## This is closer than it looks

Workbench's `image_extractor.py` **already computes exactly this tuple.** It
extracts images from a DOCX and, for each, returns:

```python
(image_path, label_or_None, surrounding_text)
```

Labels are detected from document structure, not guesswork: Word's built-in
`Caption` paragraph style first, then the following paragraph, then the
preceding one, then a pattern match over a wide label vocabulary — `FIG.`,
`Figure`, `Table`, `Diagram`, `Chart`, `Photo`, `Scheme`, `Plate IV`,
`Exhibit A` — falling back to sequential naming when nothing is detectable.

That third slot is described in its own code as *"surrounding text available for
the AI step"*.

**And then it is discarded.** The extractor's output is files on disk with
names. The anchor is computed and thrown away, because the consumer downstream
is a folder.

## The architectural consequence

**A folder has already lost the anchor.** You cannot recover *"page 3, between
'Mount the bracket' and 'Tighten the screws'"* from `IMG_2094.png`. Any design
whose input is a folder is permanently limited to case 1.

So: **two modes, and the anchor is what both produce.**

- **Folder mode** (exists today). Pre-extracted images, matched by filename.
  Right for patents, where the figures often arrive as separate PNGs and never
  passed through a DOCX we can read. Keep it as the manual override.
- **Document mode** (new). Extract from the project's own source file, keeping
  label, caption and anchor. This is the one that serves every other document,
  and the only one that can serve an unnamed image.

The plugin can do this without a new dependency: `DocxImporter` already uses
`DocumentFormat.OpenXml.Packaging.WordprocessingDocument`.

## Where this meets `figures.md`

The handoff's argument for distilling to text — run vision once, cache, inject
— survives intact, and all four of its reasons still hold:

1. cost is one pass per project, not per segment
2. non-vision providers still get something
3. the artifact is text the user can read and correct
4. it persists to the next job on the same document family

What changes is **what gets distilled**. Not a numeral register — an image
manifest:

> **Image 3** — page 3, between *"Mount the bracket"* and *"Tighten the
> screws"*: photo of a wall bracket held by two screws, one partially driven.

A patent's numeral table is then the specialised form of the same artifact,
produced when the images turn out to be numbered figures whose numerals the
text cites. `figures.md` stays the filename; it stops being patent-shaped.

## What exists today

Worth stating precisely, because the gap is not where it looks.

**In the plugin:**
- `ReferenceImages` — resolve a configured folder, list images, parse figure
  labels, order numerically. No anchors.
- `NumeralInventory` — extract parenthesised numerals from source text with
  their citing sentences; reconcile text against drawings three ways. Built,
  **no UI**.
- `ProjectSettings.ReferenceImagesFolder` + the Library tab's Reference images
  row — records a folder. **Nothing reads it.**
- The bank loader reads any `*.md` at bank root, so a hand-written `figures.md`
  **does** reach every prompt. This half works.

**In Workbench:**
- `figure_context_manager.py` — attaches real images to requests whose segment
  cites a figure. Handles `figure` / `figuur` / `fig.`
- `image_extractor.py` — DOCX → images, with label detection and surrounding
  text.

**Nowhere:** anything that turns images into text. That is the missing piece,
and it is the piece that makes the folder setting mean something.

## Open questions

**Segment ↔ paragraph mapping.** The anchor is a DOCX paragraph; the thing being
translated is a Trados segment. Establishing that correspondence is real work
and is the main unknown here. It may be enough to anchor loosely — "these
images appear in the region of the document you are currently translating" —
rather than exactly.

**What about non-DOCX projects?** A project derived from a PDF has no
parseable source. Folder mode is the answer, and it means unnamed images stay
unsupported there. Worth being explicit rather than pretending otherwise.

**Per batch or per project?** Attaching anchored images to each batch costs
vision tokens per batch. Distilling once costs one pass. The manifest suits
distillation; the attachment path suits the segments that genuinely need the
picture. Probably both, as the handoff argued for its step 6.

**Does the user confirm the manifest?** The handoff insisted on review before
first use, and it was right: an occasionally-wrong description injected into
every prompt is exactly the silent-wrong failure this codebase keeps producing.

## Build order

1. **A button for `NumeralInventory`.** Built already, no vision needed, and a
   real QA check on its own: numerals in the drawings never cited in the text,
   and vice versa. Cheapest useful thing here.
2. **Document-mode extraction** — DOCX → `(image, label, caption, anchor)`,
   porting the detection logic from Workbench's extractor rather than
   reinventing it. No AI involved; testable on its own.
   **Built** (`Core/DocxImageExtractor.cs`), not yet wired to any UI.
3. **The distillation pass** — images + anchors + source text → `figures.md` as
   an image manifest. The piece nothing has.
4. **Review before first use.**
5. **Anchored attachment** for segments that genuinely need the picture — the
   handoff's step 6, and where Workbench's `figure_context_manager` is the
   reference implementation.

1 and 2 are independently useful and involve no model calls.

## Align with Workbench, or diverge deliberately

Workbench maps `"Figure 1.png" → '1'`; this plugin's `ReferenceImages` parses
`FIG. 1`, `Fig 1`, `Figure 1`, `FIG-1`, bare `12`, `FIG. 2A`. Same job, two
conventions. A user with one folder of drawings should not get different results
in the two products — whichever rule wins, it should be one rule.
