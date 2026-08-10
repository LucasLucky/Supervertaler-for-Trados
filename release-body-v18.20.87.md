Supervertaler for Trados **v18.20.87** (Studio 2024) / **v19.20.87** (Studio 2026 beta) — unsigned builds are attached below. Covers 18.20.87.

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

## [18.20.87 / 19.20.87] – 2026-07-04

### Fixed (Auto-updater · no longer offers the wrong Studio generation)

- **The update check no longer offers a Studio 2026 build to Studio 2024 users, or vice-versa.** Under the new versioning scheme the version major encodes the target Studio (18.x = Studio 2024, 19.x = Studio 2026), and the RWS App Store lists both generations' builds side by side. The updater was picking the numerically-highest published version regardless of generation, so a Studio 2024 user on `18.20.86` was shown the `19.20.86` build meant for Studio 2026. It now filters the App Store's version list to the **same major as the installed build** and offers the newest match within that generation only – 18.x installs only ever see 18.x updates, 19.x installs only 19.x. (Trados's own `RequiredProduct` gate would have refused to load the mismatched build, so nothing broke, but the prompt was wrong and confusing.)

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
