Supervertaler for Trados **v18.20.163** (Studio 2024) / **v19.20.163** (Studio 2026). Covers 18.20.160 → 18.20.163.

## 📦 How to install

**Supervertaler for Trados is published through the [RWS App Store](https://appstore.rws.com/plugin/432).** Install it from there, or from inside Studio via **Add-Ins → RWS App Store**. Those builds are signed by RWS, so Studio loads them without an "unsigned plug-in" prompt at every start, and the plugin's own update check keeps you current.

The plugin binary is **not attached to GitHub releases** – this page is the changelog and the record of what changed in each build. App Store updates go through RWS review, so a brand-new fix can take a day or two to appear; if you are waiting on something specific, email support@supervertaler.com and I will send you the build directly.

| Also attached | What it is |
|---|---|
| `Supervertaler-MCP-Server.mcpb` | AI assistant extension for Claude Desktop (optional, see below) |
| `Supervertaler-MCP-Server-exe.zip` | AI assistant server for other local MCP clients, e.g. Claude Code (optional, see below) |

## 🤖 Supervertaler MCP Server (optional)

`Supervertaler-MCP-Server.mcpb` connects **Claude Desktop directly to your live Trados Studio session** – ask about the open project, search your TMs and termbases, run QA checks, have translations drafted into the document, all from Claude's own chat window. To install: download the file, then in Claude Desktop open **Settings → Extensions → Advanced settings** and click **Install extension…** (double-click on the file also works if your system associates `.mcpb` with Claude; drag-and-drop does not). Requires Supervertaler for Trados (this plugin) and works entirely on your own machine. Other MCP clients that run local servers (e.g. **Claude Code**) can use `Supervertaler-MCP-Server-exe.zip` instead: unzip it somewhere permanent and point the client's MCP config at the exe – the plugin's **Settings → AI Settings → Connect AI assistant…** dialog copies a ready-made snippet. Note: **ChatGPT's desktop app is not supported** – it runs MCP servers in the cloud and cannot reach a local Trados session ([details](https://docs.supervertaler.com/trados/mcp-server/)). [Documentation](https://docs.supervertaler.com/trados/mcp-server/).

## What's changed

## [18.20.163 / 19.20.163] – 2026-08-08

### Fixed (Supervertaler MCP Server · one slow request no longer blocks every other one)

- **The bridge served requests strictly one at a time, so a single slow call stalled everything queued behind it.** Measured on a large project: a request that answers in 0.4 seconds against an idle bridge took **84 seconds** when it happened to be issued behind two long-running ones. Because a client-side timeout does not cancel work already running inside Studio, an abandoned request kept the queue blocked — and retrying, the natural reaction, made it worse rather than better.
- The listener thread now only accepts connections and hands each request to a worker, so it is never blocked. Operations that touch the Trados editor still take their turn on the UI thread — that is required, the editor is not safe to drive from several threads — but everything that does not need the editor (termbase lookups, help, the tool list, `session_report`) now answers immediately instead of waiting behind them.
- **If you saw "the request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing", update the MCP server itself as well.** That message comes from the separately-installed MCP server, not the plugin, and its timeout was raised to 5 minutes in 20.148 — so seeing it means that component is older than the plugin. It does not update with Studio: reinstall the `.mcpb` in Claude Desktop (or re-unzip the exe for other clients) after a plugin update, then restart the client.
- **Anyone still on a pre-20.110 MCP server is now told so, once, in chat.** Those builds predate the timeout fix, so they report long saves and updates as failures that in fact succeeded — and an assistant that retries on a "failure" then writes twice. The plugin now recognises them and asks the assistant to relay a short explanation, including not to retry a write after a timeout without checking whether it landed. Nothing else is gated: the tool list is read from the plugin at runtime, so an older server still has the current tools and keeps working normally.

## [18.20.162 / 19.20.162] – 2026-08-08

### Fixed (Supervertaler MCP Server · find_and_replace refused every segment containing a tag)

- **`find_and_replace` skipped any segment carrying an inline tag, reporting it as a match that "straddles inline formatting/tags" even when it plainly did not.** On a tag-heavy document that is most of the file, which made the tool useless exactly where a consistency sweep is worth most. Confirmed live: the phrase *Overzicht aansluitingen*, sitting wholly inside a single `<t1>…</t1>` wrapper, was refused, while the same phrase in two untagged segments was replaced.
- **The safety check was comparing two different kinds of string.** It built the expected result by replacing across `Target.ToString()` — which renders the *markup* as well, `<cf size=8>` and the like — then compared that against a simulation built by replacing inside each text node, which has no markup in it. For any segment with a tag the two could never match, so the guard fired on every one of them.
- Both sides now start from the same basis: the segment's concatenated text nodes. The guard therefore tests what it was always meant to test — whether replacing across the whole text gives the same answer as replacing inside each node — so a match genuinely straddling a tag boundary is still refused, and one that merely sits near a tag is no longer punished for it. The `before`/`after` preview now shows the segment's text rather than raw internal markup.

## [18.20.161 / 19.20.161] – 2026-08-08

### Fixed (Supervertaler MCP Server · get_active_segment misrepresented every tag)

- **`get_active_segment` showed the current segment's target with all its inline tags stripped, and its source in raw internal markup.** A footer segment whose source and target both carry a page-number field was reported as source `Side <field name="Page" value="10"/>` and target `Pagina ` – so the target looked like it had lost the field. It had not: `get_segments` correctly reports the same segment as `Side <t1/>` → `Pagina <t1/>`. The same applied to every entry in `surroundingSegments`.
- **This invited exactly the wrong repair.** An AI reading the active segment or its neighbours would conclude that formatting had been dropped and "fix" segments that were never broken — writing real tag damage into a clean document. The raw source form (`<group name="Group 258">`, `<cf size=8>`) is also not a marker `update_segments` accepts, so copying it would fail.
- Both fields now go through the same serializer `get_segments` uses, so every tool describes a segment the same way and a marker copied from one tool is valid in another. The AI chat and prompt-context path deliberately keeps its previous plain-text rendering — an LLM writing prose is not helped by tag noise — so only the MCP surface changed.

## [18.20.160 / 19.20.160] – 2026-08-08

### Fixed (Supervertaler MCP Server · update_segments destroyed a segment's comments)

- **Writing a segment through `update_segments` deleted every Trados comment on it, silently, and reported success.** The write path clears the target and rebuilds it from the *source*'s tags — and a comment lives only in the target's markup, so it went with the clear and nothing put it back. Verified on a real job: the comment was gone from the saved SDLXLIFF, with `ok: true` and no warning.
- **This turned the normal delivery workflow into a shredder.** Read the comments, act on what they say, fix the segments they refer to — and each fix deletes the comment that prompted it, one segment at a time. Following the documented rule guaranteed the loss: tag markers are to be copied from the segment's *source* field, and the comment anchor exists only on the target side, so a correctly-behaving AI dropped it every time. Nothing surfaced this until someone re-parsed the whole document from disk.
- Comment markers are now captured before the rewrite and restored around the new text, on both the tagged and the plain-text write paths. Re-anchoring is deliberately coarse — a comment that covered part of a segment now covers all of it — because the span it pointed into no longer exists after a rewrite, and a comment attached to slightly too much text is far better than one silently deleted.
- If a comment cannot be preserved after all, the segment's result now carries a `warning` saying so, on the same channel as the tag-id audit. A destructive silent success is the worst of the available behaviours; an announced failure is recoverable.

### Fixed (Supervertaler MCP Server · two limits that hid real findings)

- **`find_inconsistencies` could not reach past its first 200 groups.** The cap was applied with no way to page, so on a document with 375 inconsistent groups the remaining 175 were unreachable at any `limit` — and on the job where this surfaced, those later groups were the cross-file terminology drift, i.e. the part worth finding. There is now an `offset` parameter, the cap is 500, and when the result is truncated the note gives the exact offset to pass for the next page.
- **`add_term` no longer strips trailing punctuation from terms.** `Rev.` → `Rev.` was being stored as `Rev` → `Rev`, an entry that no longer records the decision it was created for; `NOTE:`, `Doc.nr.`, `PO-nr.` and `SAFETY INFORMATION!` lost their final character the same way. Abbreviation-with-period is a legitimate term form. Stripping still happens for the in-Studio quick-add, where the term is captured from a selection in running text and a final `.` really is sentence punctuation — but a term named deliberately through the MCP server is now stored exactly as sent. Lookup is unaffected: the search trims the stored term as well as the query, so `Rev.` still matches a search for `Rev`.

## Links

- RWS App Store (signed): https://appstore.rws.com/plugin/432
- Full changelog: https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md
- Questions & discussion: https://github.com/orgs/Supervertaler/discussions
