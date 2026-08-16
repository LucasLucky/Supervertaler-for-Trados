# RWS App Store Manager - v18.20.109

Two builds ship from this one release (identical feature set, distinct
version numbers so the App Store never sees a collision):

| Build | Version number | Min studio | Max studio | Checksum (SHA-256) |
|-------|----------------|------------|------------|--------------------|
| Studio 2024 | `18.20.109.0` | `18.0` | `18.9` | `5506dc9db5775b969b3632d1aea54c30b0fbdd7501ea5b3e15c202ab0a7c1a8e` |
| Studio 2026 | `19.20.109.0` | `19.0` | `19.0.9` | `7617aed42ce4f272882d49d091ccd36ddab221b02da5855664b4c2519a968f6b` |

---

## Changelog

### Added
- **New `get_prompt_context` tool** hands your AI assistant everything it needs to write a translation prompt tailored to the project open in Trados: source/target languages, the detected domain, the source text, the relevant termbase terms, a few confirmed TM example pairs, and your current Default Translation Prompt as a starting point. Ask *"look at my project and write me a tailored prompt,"* refine it together, then *"save it"* (via `save_prompt`). The plugin makes **no** prompt-engineering API calls of its own – the AI you're already chatting with does the work, which is what it's best at.
- **New AI Setting – "Prompt context – source segments"** (Settings → AI Settings, under External AI assistants): controls how much of the source document `get_prompt_context` sends. **0 = the whole document** (the default – ideal for large-context models like Claude and for high-value projects where you want the AI to see everything); a positive number caps it. The AI can also override it per request with `maxSegments`.

### Fixed
- **AutoPrompt's meta-prompt described segment delivery wrongly**, so every generated prompt told the translator AI it receives *"one segment at a time, in isolation"* – but Batch Translate/Proofread actually send **numbered batches** of segments (your *Batch size* setting, e.g. 75 per request). The generated prompts therefore forbade using context the AI could legitimately see, and left terminology "choices" open that can't stay consistent across batches. The template now describes batched delivery correctly: translate every delivered segment and keep count/order aligned; in-batch context (e.g. a nearby antecedent) **may** be used; batch boundaries are arbitrary, so document-wide checks belong to a QA pass; there is no memory between requests, so the prompt must **lock** every recurring term (no open "X or Y" choices); and ⟦TC: …⟧ correction markers stay attached to their own segment, never pooled at the end of a batch. Existing AutoPrompt-generated prompts in your library keep the old wording – regenerate (or hand-edit) the ones you rely on.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases