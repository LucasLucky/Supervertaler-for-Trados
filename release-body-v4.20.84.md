Supervertaler for Trados **v4.20.84** — unsigned builds for Trados Studio 2024 and 2026 (beta) are attached below. Covers 4.20.84.

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

## [4.20.84] – 2026-07-01

### Added (Claude Sonnet 5)

- **Claude Sonnet 5 (`claude-sonnet-5`) is now the default Claude model.** Anthropic's newest Sonnet (released June 30, 2026) gives near-Opus quality – with substantial gains in reasoning, tool use, and knowledge work over Sonnet 4.6 – at the same Sonnet price tier. It's added to the Claude model list, the cost ledger (`pricing.json` + `PricingTable`), and is selected by default for new setups.
- **Sonnet 4.6 is kept as a selectable fallback**, so existing per-project model choices keep working.
- **Pricing note:** the ledger uses Sonnet 5's standard rate of **$3 / M input, $15 / M output** (same as 4.6). Anthropic's introductory pricing ($2 / $10) runs through Aug 31, 2026, so during that window the cost estimate is slightly *higher* than actual; from Sep 1 it matches.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
