# SuperMemory A/B benchmark

Answers one question with a number instead of a feeling:

> **Does injecting the memory bank make Supervertaler's translations closer to what I actually confirmed?**

It translates the same segments twice — identical model, identical prompt, identical temperature — differing only in whether the memory-bank block is present, and scores both against the translator's own confirmed target.

Nothing is written back to Trados or to the memory bank. It is read-only.

## Running it

Trados Studio must be running with a **finished** file open in the editor (the confirmed targets are the reference), and `ANTHROPIC_API_KEY` must be set.

```bash
# verify the wiring, make no API calls, spend nothing
python benchmark.py --dry-run

# the real run - roughly $0.25 for 40 segments on Sonnet
python benchmark.py --limit 40 --yes
```

Outputs `benchmark-report.md` (summary, biggest wins and losses) and `benchmark-report.csv` (every segment, both arms, per-segment scores).

## The experiment worth running first

Domain selection and bank value are **two different questions**, and conflating them will mislead you. Run three arms:

```bash
python benchmark.py --limit 40 --yes --out no-bank-baseline      # note the auto-detected domain
python benchmark.py --limit 40 --yes --domain "EPO Patent Translation" --out forced-domain
```

- If the bank helps **only** with a forced domain, the bank is fine and **domain detection** is the problem.
- If it helps in neither, the bank content is not earning its tokens on this kind of job.
- If it helps in both, ship it and stop worrying.

On the first project this was run against (a Dutch→English patent application), auto-detection returned **"Chinese Technical Documentation"** — so the plugin was injecting tilde-and-`°C`-spacing conventions into a patent translation. That is exactly the kind of thing this harness exists to catch.

## What it measures

**Edit rate** — normalised character edit distance between the machine output and the confirmed target. Lower is better. It is a proxy for post-editing effort: `0.20` means roughly a fifth of the characters would need changing.

**Term compliance** — of the memory bank's terminology decisions whose source term appears in a segment, how many did the output actually honour? This is the most direct measure of whether the bank is being *obeyed* rather than merely *supplied*. Context-dependent decisions (`divider (machinery) / distributor (sales)`) are skipped, because string matching cannot tell which sense applies and guessing would fabricate a result.

**Paired bootstrap 95% interval** on the per-segment difference. If the interval crosses zero the verdict is "no measurable difference", however tempting the mean looks. With 40 segments, expect to need a real effect before it clears zero.

## Design choices worth knowing

- **It uses the plugin's own retrieval.** The bank block comes from `/v1/supermemory-context`, the same endpoint the MCP tools use, so the benchmark exercises the real `MemoryBankReader.LoadContext` selection logic rather than a reimplementation that might flatter it.
- **100% TM matches are excluded by default.** Those come from the TM regardless of what the model does, and would dilute the signal. `--include-tm-matches` overrides.
- **Short segments are skipped** (`--min-words`, default 6). Edit rate on a three-word segment is mostly noise.
- **A segment must parse in both arms to be scored**, so a dropped line never advantages one side.
- **No explicit temperature.** Newer models reject the parameter outright (`temperature is deprecated for this model`), so it is omitted unless you pass `--temperature`. Both arms are always sampled identically, which is what the comparison needs; it only means a re-run may differ slightly from the previous one.

## Caveats — read before quoting a number

1. **Reference contamination.** The reference is your confirmed target. If you translated that file *with* the bank switched on, the reference already contains the bank's influence and the comparison is biased in its favour. The cleanest test is a file finished before the bank existed, or one translated with SuperMemory off.
2. **Edit rate is not quality.** A better translation that happens to differ from your wording scores *worse*. Read the "biggest losses" section of the report before concluding the bank hurt — some of them will be improvements.
3. **One run per arm.** Re-run over a different slice (`--limit`, or a different file) before believing a small difference.
4. **One project is one data point.** A result on a patent file says nothing about medical UI work. Run it across three different domains before drawing conclusions about SuperMemory as such.
