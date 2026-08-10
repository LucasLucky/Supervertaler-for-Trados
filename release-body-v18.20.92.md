Supervertaler for Trados **v18.20.92** (Studio 2024) / **v19.20.92** (Studio 2026) — unsigned builds are attached below. Covers 18.20.91 → 18.20.92.

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
| `Supervertaler-for-Trados-Studio-2026.zip` | Trados Studio 2026 |

## What's changed

## [18.20.92 / 19.20.92] – 2026-07-13

### Fixed (SuperSearch · dialog text no longer clips on high-resolution screens)

- **The "Select translation memories / files to include" pickers now scale their text properly on high-DPI displays.** The instruction line and the buttons had fixed pixel sizes, so on a high-resolution screen the heading was cut off and "Select None" was truncated to "Select". The label and buttons now auto-size to their (scaled) text, and the buttons sit in a proper layout bar, so everything stays readable at any display scaling. Applies to both the Select-TMs and Select-Files dialogs.

## [18.20.91 / 19.20.91] – 2026-07-13

### Fixed (SuperSearch · now searches your TMs out of the box)

- **SuperSearch now searches your translation memories by default, not just the project files.** The search-scope dropdown shipped defaulting to **"Project files"** (SDLXLIFF files only), which silently skipped every TM – so on a fresh install, TM and GroupShare hits never appeared even though the TMs were ticked in the list, and there was no error to explain why. The default is now **"Files + TMs"**, and that option is listed first in the dropdown as the recommended scope. If you had previously left the scope on "Project files", just switch it to "Files + TMs" once (your choice is remembered). "Project files" and "TMs only" remain available for when you want to narrow the search.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
