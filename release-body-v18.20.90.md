Supervertaler for Trados **v18.20.90** (Studio 2024) / **v19.20.90** (Studio 2026) — unsigned builds are attached below. Covers 18.20.90.

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

## [18.20.90 / 19.20.90] – 2026-07-10

### Added (GroupShare · SuperSearch can now search your server-based GroupShare TMs)

- **SuperSearch now searches server-based (GroupShare) translation memories, not just local `.sdltm` files.** When your project uses a GroupShare TM, SuperSearch queries it alongside your project files and any local TMs and shows the hits inline. Server-TM results are badged **"GroupShare"** in the Status column so you can tell them apart from local files at a glance, and each appears under its own TM name (e.g. `en-US to nl-BE`) rather than a raw server address. This was the top request from institutional users running GroupShare.
- **New "GroupShare" tab in Supervertaler Settings, where you enter your server login once.** Trados Studio does not hand its stored server credentials to plugins, so you set the server URL, login provider, username and password here. The password is encrypted at rest with Windows DPAPI (current user) and is never written in clear text. It lives in Settings rather than inside SuperSearch because these credentials are meant to power more GroupShare-aware features over time.
- **Both GroupShare and Windows (AD) authentication are supported**, via a Login provider dropdown that mirrors GroupShare's own two options – for organisations that authenticate GroupShare against Active Directory.
- Works on both **Trados Studio 2024 and 2026**. (Under the bonnet, server concordance requests are capped to the GroupShare TM Server's limit so they are not rejected; local TMs are unaffected.)

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
