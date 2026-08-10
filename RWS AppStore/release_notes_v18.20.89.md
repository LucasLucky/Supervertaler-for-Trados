# RWS App Store Manager - v18.20.89

Two builds ship from this one release (identical feature set, distinct
version numbers so the App Store never sees a collision):

| Build | Version number | Min studio | Max studio | Checksum (SHA-256) |
|-------|----------------|------------|------------|--------------------|
| Studio 2024 | `18.20.89.0` | `18.0` | `18.9` | `ea76a27c550ed37a468c021b89aac077fc52c95ca1d20d8d5380c9d5fa961835` |
| Studio 2026 | `19.20.89.0` | `19.0` | `19.0.9` | `34c8ab50d11b0154cab6f2e8844436ebea7dc2539ecc1079f0feee4409a52672` |

---

## Changelog

### Changed
- **AutoPrompt now detects the document's context with the AI instead of a keyword heuristic, and lets you confirm or steer it before generating.** Clicking AutoPrompt sends a sample of the source to the model, which classifies the domain and describes the text type; a short "Reading the document…" window shows while it works. A confirm-context dialog then shows the detected domain with a dropdown to correct it and an optional briefing box (e.g. "creative marketing copy, playful tone"), which is fed to the generator as authoritative context. The default is one click straight through (**Generate**). This mirrors the Supervertaler Workbench AutoPrompt and fixes the cases where the old keyword detector misread a document (e.g. a creative text read as a patent). The keyword detector is kept for word/segment statistics and as an offline fallback if the AI call fails; its keyword "tone" read is dropped in favour of the AI's description. Each AutoPrompt run makes one small extra classification call (a few hundred tokens).

### Fixed
- **Pasting a large Clipboard-Mode batch back into Trados Studio 2024 no longer spikes memory and crashes.** After running a Batch Translate in Clipboard Mode and clicking "Paste from Clipboard" to write the LLM's response back, a large batch made the editor grid appear to loop endlessly and Trados closed with RAM up around 1.8 GB – far past what a 32-bit process can safely hold. The paste-back applied every parsed segment on the UI thread with no memory guard, no progress window, and no message pumping, so memory climbed unchecked and the grid never got a chance to repaint. It now uses the same safe writeback system as the bilingual re-import (added in 18.20.86):
- **32-bit memory watchdog.** Every 20 segments the writeback compacts the heap when memory climbs (soft limit) and **stops gracefully with a clear message** before it can crash the host (hard limit), telling you to finish the remaining segments as a smaller batch or in Trados Studio 2026 (64-bit). A no-op on 64-bit.
- **Responsive progress + Cancel.** A small progress window shows "Writing translations… N of M" with a **Cancel** button; the loop pumps the UI every 20 segments so the editor stays responsive instead of appearing frozen (the "looping" grid).
- **Re-entrancy guard.** The paste button is disabled while a paste runs, and a second paste is refused until the first finishes.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases