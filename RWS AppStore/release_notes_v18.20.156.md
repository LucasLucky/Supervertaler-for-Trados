# RWS App Store Manager - v18.20.156

Two builds ship from this one release (identical feature set, distinct
version numbers so the App Store never sees a collision):

| Build | Version number | Min studio | Max studio | Checksum (SHA-256) |
|-------|----------------|------------|------------|--------------------|
| Studio 2024 | `18.20.156.0` | `18.0` | `18.9` | `eb5a7c6970280b51c9f40b31636b153c60878118eb402564884d1c2b3641f26f` |
| Studio 2026 | `19.20.156.0` | `19.0` | `19.0.9` | `c147222216a2cad340d4736ad1988f29226b795da5cf2dd077ff770ee1ac5a16` |

This release covers everything since **v18.20.134**, the last version actually
published for Studio 2024 — v18.20.144.0 was submitted but never cleared
review, so the Studio 2024 changelog below restates 135–144 as well as the
new work through 156. Studio 2026 users who already received 144 will see
some of this as a repeat; that's expected.

---

## Highlights

- **The App Store is now the only distribution channel.** GitHub releases keep the changelog, tags and MCP server files, but no longer carry the plugin itself. The update dialog's "Skip This Version" is now "Not Now" and quietens prompts for a week instead of one version, and legacy 4.x installs are finally offered updates again.
- **SuperSearch now searches your termbases too**, alongside project files and TMs — Supervertaler, MultiTerm (`.sdltb`) and Trados `.ttb` alike.
- **TermLens mirrors your editor selection in both directions**: select in the source and matching words highlight; select in the target and the matching term chips light up.
- **TermPicker is now available as a dockable pane**, not just the Alt+P popup, with its own "press I for details" view, a fixed Escape/Alt+P key-handling pass, and synonyms shown expanded by default.
- **`add_term` no longer writes reversed entries.** It takes explicit `sourceLang`/`targetLang`, can target specific termbases, and reports exactly what was stored; `lookup_term` now matches source and target columns alike. Both grew out of a real termbase-corruption incident.
- **A 2,889-segment real job turned up a cluster of MCP fixes in one go**: `compare_document_to_tm` (find where a translation departs from the reference TM), `check_nbsp` plus non-breaking-space-safe writes, `run_verification` now flags stale results, and large write batches no longer lose their confirmation.
- **Locked segments are never sent to the AI** by batch translate or batch proofread.
- **GPT-5.6 (Sol, Terra, Luna) is supported end to end** — selectable in Settings, correct timeouts across every GPT-5.x route, and a same-day fix for an instant chat error.
- **Translating a single segment (Alt+T) now gets the same document and SuperMemory context a batch run gets**, instead of translating in isolation.
- Clipboard Mode no longer truncates multi-paragraph translations, and SuperMemory is now reachable read-only from any MCP client.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases/tag/v18.20.156
