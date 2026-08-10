Supervertaler for Trados **v18.20.89** (Studio 2024) / **v19.20.89** (Studio 2026 beta) — unsigned builds are attached below. Covers 18.20.88 → 18.20.89.

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
| `Supervertaler-for-Trados-Studio-2026-beta.zip` | Trados Studio 2026 (beta) |

## What's changed

## [18.20.89 / 19.20.89] – 2026-07-06

### Changed (AutoPrompt · AI-based context detection with a confirm step)

- **AutoPrompt now detects the document's context with the AI instead of a keyword heuristic, and lets you confirm or steer it before generating.** Clicking AutoPrompt sends a sample of the source to the model, which classifies the domain and describes the text type; a short "Reading the document…" window shows while it works. A confirm-context dialog then shows the detected domain with a dropdown to correct it and an optional briefing box (e.g. "creative marketing copy, playful tone"), which is fed to the generator as authoritative context. The default is one click straight through (**Generate**). This mirrors the Supervertaler Workbench AutoPrompt and fixes the cases where the old keyword detector misread a document (e.g. a creative text read as a patent). The keyword detector is kept for word/segment statistics and as an offline fallback if the AI call fails; its keyword "tone" read is dropped in favour of the AI's description. Each AutoPrompt run makes one small extra classification call (a few hundred tokens).

## [18.20.88 / 19.20.88] – 2026-07-06

### Fixed (Clipboard Mode · paste-back no longer crashes 32-bit Trados on large batches)

- **Pasting a large Clipboard-Mode batch back into Trados Studio 2024 no longer spikes memory and crashes.** After running a Batch Translate in Clipboard Mode and clicking "Paste from Clipboard" to write the LLM's response back, a large batch made the editor grid appear to loop endlessly and Trados closed with RAM up around 1.8 GB – far past what a 32-bit process can safely hold. The paste-back applied every parsed segment on the UI thread with no memory guard, no progress window, and no message pumping, so memory climbed unchecked and the grid never got a chance to repaint. It now uses the same safe writeback system as the bilingual re-import (added in 18.20.86):
  - **32-bit memory watchdog.** Every 20 segments the writeback compacts the heap when memory climbs (soft limit) and **stops gracefully with a clear message** before it can crash the host (hard limit), telling you to finish the remaining segments as a smaller batch or in Trados Studio 2026 (64-bit). A no-op on 64-bit.
  - **Responsive progress + Cancel.** A small progress window shows "Writing translations… N of M" with a **Cancel** button; the loop pumps the UI every 20 segments so the editor stays responsive instead of appearing frozen (the "looping" grid).
  - **Re-entrancy guard.** The paste button is disabled while a paste runs, and a second paste is refused until the first finishes.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
