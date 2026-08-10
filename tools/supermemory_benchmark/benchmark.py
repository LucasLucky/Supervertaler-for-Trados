#!/usr/bin/env python3
"""
SuperMemory A/B benchmark.

Answers one question with a number instead of a feeling: does injecting the
SuperMemory memory bank make Supervertaler's translations closer to what the
translator actually confirmed?

Method
------
1.  Pull confirmed segments from the open Trados project via the Supervertaler
    bridge. The confirmed target is the reference - it is what the translator
    actually shipped.
2.  Pull the memory-bank context via the SAME retrieval path the plugin uses
    (/v1/supermemory-context), so we benchmark the real selection logic, not a
    reimplementation of it.
3.  Translate each segment TWICE with an identical prompt and model, differing
    only in whether the knowledge-base block is present.
4.  Score both against the reference:
      - edit rate: normalised character edit distance (lower = less post-editing)
      - term compliance: share of memory-bank term decisions actually honoured
5.  Report per-segment and aggregate, with a paired bootstrap interval so a
    difference of "2%" is not mistaken for a result.

Nothing is written back to Trados or to the memory bank. This is read-only.

Usage
-----
    python benchmark.py --dry-run              # verify wiring, no API calls, no cost
    python benchmark.py --limit 40 --yes       # the real run

Requires ANTHROPIC_API_KEY in the environment. Trados Studio must be running
with a project open in the editor.
"""

import argparse
import json
import os
import random
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API_URL = "https://api.anthropic.com/v1/messages"
DEFAULT_MODEL = "claude-sonnet-5"

# Rough Anthropic list prices (USD per million tokens) for the cost estimate
# only. Wrong prices make a wrong estimate, never a wrong measurement.
PRICE_IN, PRICE_OUT = 3.00, 15.00


# ─────────────────────────── bridge discovery ───────────────────────────
# Mirrors Supervertaler.McpServer/BridgeClient.cs so we hit the same instance
# the MCP tools do.

def resolve_user_data_root():
    """The shared Supervertaler user-data root, as the plugin resolves it."""
    root = Path.home() / "Supervertaler"
    cfg = Path(os.environ.get("APPDATA", "")) / "Supervertaler" / "config.json"
    if cfg.is_file():
        try:
            data = json.loads(cfg.read_text(encoding="utf-8"))
            if data.get("user_data_path"):
                root = Path(data["user_data_path"])
        except Exception:
            pass
    return root


def resolve_handshake_path():
    override = os.environ.get("SUPERVERTALER_BRIDGE_FILE")
    if override:
        return Path(override)
    return resolve_user_data_root() / "trados" / "runtime" / "bridge.json"


# ─────────────────────────── output location ────────────────────────────
# Reports quote a real client's source and confirmed target verbatim, and
# the CSV carries every scored segment. This script lives in a PUBLIC git
# repo, so the one place output must never default to is next to the
# script: `git add -A` after a run would publish the client's document.
# Default somewhere outside any checkout, and refuse to write into a git
# working tree even when explicitly pointed at one.

def default_output_dir():
    return resolve_user_data_root() / "trados" / "benchmarks"


def enclosing_git_worktree(path):
    """The nearest ancestor that is a git working tree, or None."""
    try:
        for parent in [path, *path.parents]:
            if (parent / ".git").exists():
                return parent
    except Exception:
        pass
    return None


def resolve_output_base(out, out_dir, allow_repo_output):
    """Absolute path stem for the .csv/.md pair. Exits rather than write
    client text into a repo."""
    # A caller who passes a path (not a bare name) means it; a bare basename
    # is joined to the safe default directory. Tested on the RAW STRING, not
    # Path.parts: pathlib normalises "./leak" down to a single part, so
    # `--out ./leak` would silently land in the default directory instead of
    # the current one - and the git guard below would never see it.
    written_as_path = any(sep in out for sep in (os.sep, os.altsep) if sep)
    target = Path(out) if Path(out).is_absolute() or written_as_path \
        else Path(out_dir or default_output_dir()) / out
    target = target.expanduser()
    try:
        target = target.resolve()
    except Exception:
        target = target.absolute()

    repo = enclosing_git_worktree(target.parent)
    if repo and not allow_repo_output:
        sys.exit(
            f"Refusing to write benchmark output into a git working tree:\n"
            f"  output: {target}.csv / .md\n"
            f"  repo:   {repo}\n\n"
            "These files quote the job's source and confirmed target verbatim.\n"
            f"Leave --out as a bare name (it lands in {default_output_dir()}),\n"
            "pass --out-dir to choose somewhere else, or --allow-repo-output if\n"
            "you have checked that this repo is private and stays that way."
        )

    target.parent.mkdir(parents=True, exist_ok=True)
    return target


def discover_bridge():
    path = resolve_handshake_path()
    if not path.is_file():
        sys.exit(
            f"Bridge handshake not found at {path}.\n"
            "Start Trados Studio, open a project in the editor, and make sure the "
            "Supervertaler plugin is enabled."
        )
    hs = json.loads(path.read_text(encoding="utf-8"))
    if not hs.get("port") or not hs.get("token"):
        sys.exit(f"Bridge handshake at {path} is malformed.")

    # A handshake file outlives the Studio session that wrote it, so a stale one
    # points at a dead port and every call fails with a bare connection-refused.
    # BridgeClient.cs checks this; so do we, and say what to actually do about it.
    if not pid_alive(hs.get("pid", 0)):
        sys.exit(
            f"Stale bridge handshake at {path}\n"
            f"  It was written by process {hs.get('pid')} at {hs.get('startedAt')}, "
            "which is no longer running.\n"
            "  Trados Studio may be open, but the Supervertaler bridge only starts once the "
            "plugin initialises.\n"
            "  Open a project document in the EDITOR view, then run this again."
        )
    return f"http://127.0.0.1:{hs['port']}", hs["token"]


def pid_alive(pid):
    if not pid or pid <= 0:
        return False
    if os.name == "nt":
        try:
            out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"],
                                 capture_output=True, text=True, timeout=15).stdout
            return str(pid) in out
        except Exception:
            return True  # can't tell - let the HTTP call be the judge
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def bridge_get(base, token, path, params=None):
    url = base + path
    if params:
        clean = {k: v for k, v in params.items() if v is not None}
        if clean:
            # quote_via=quote, not the default quote_plus: the bridge does not
            # decode '+' as a space, so "Patent Translation" would arrive as
            # "Patent+Translation" and match no article.
            url += "?" + urllib.parse.urlencode(clean, encoding="utf-8",
                                                quote_via=urllib.parse.quote)
    req = urllib.request.Request(url, headers={
        "Authorization": f"Bearer {token}",
        "X-Supervertaler-Mcp-Exe-Version": "2",
    })
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode("utf-8"))


# ───────────────────────────── scoring ─────────────────────────────

def levenshtein(a, b):
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1]


def edit_rate(candidate, reference):
    """0.0 = identical to what the translator confirmed, 1.0 = entirely different."""
    ref = (reference or "").strip()
    cand = (candidate or "").strip()
    if not ref:
        return None
    return levenshtein(cand, ref) / max(len(ref), 1)


def parse_frontmatter(text):
    """Tolerates the ```yaml / ```markdown fence wrapper some articles carry."""
    t = text.lstrip()
    if t.startswith("```"):
        nl = t.find("\n")
        if nl < 0:
            return {}
        t = t[nl + 1:].lstrip()
    if not t.startswith("---"):
        return {}
    end = t.find("\n---", 3)
    if end < 0:
        return {}
    fm = {}
    for line in t[3:end].splitlines():
        if ":" not in line or line.strip().startswith("#"):
            continue
        k, _, v = line.partition(":")
        v = v.strip().strip('"').strip("'")
        v = v.replace("[[", "").replace("]]", "")
        if v:
            fm[k.strip()] = v
    return fm


# Display names as Trados reports them ("Dutch (Netherlands)") mapped to the
# base codes the bank uses ("nl-BE"). Base code only: region is kept in the
# data but never matched on, so an nl-BE note applies to an nl-NL project.
LANG_BASE = {
    "dutch": "nl", "english": "en", "german": "de", "french": "fr",
    "spanish": "es", "italian": "it", "portuguese": "pt", "polish": "pl",
    "czech": "cs", "swedish": "sv", "danish": "da", "norwegian": "no",
    "finnish": "fi", "chinese": "zh", "japanese": "ja", "korean": "ko",
    "russian": "ru", "turkish": "tr", "arabic": "ar", "greek": "el",
    "hungarian": "hu", "romanian": "ro", "bulgarian": "bg", "croatian": "hr",
    "slovak": "sk", "slovenian": "sl", "estonian": "et", "latvian": "lv",
    "lithuanian": "lt", "ukrainian": "uk", "hebrew": "he", "hindi": "hi",
}


def base_lang(value):
    """'Dutch (Netherlands)' -> 'nl'; 'nl-BE' -> 'nl'. None if unrecognised."""
    if not value:
        return None
    v = value.strip().lower()
    head = re.split(r"[\s(]", v)[0]
    if head in LANG_BASE:
        return LANG_BASE[head]
    m = re.match(r"([a-z]{2,3})(?:[-_].*)?$", head)
    return m.group(1) if m else None


def note_direction(fm):
    """(source_base, target_base) for a term note, from either schema."""
    s = base_lang(fm.get("source_lang"))
    t = base_lang(fm.get("target_lang"))
    if s and t:
        return s, t
    pair = fm.get("language_pair") or fm.get("languages") or ""
    parts = re.split(r"->|\u2192|>", pair)
    if len(parts) == 2:
        return base_lang(parts[0]), base_lang(parts[1])
    return None, None


def load_term_pairs(bank_dir, src_base=None, tgt_base=None):
    """(pairs, skipped) - the bank's term decisions for THIS direction.

    Without the filter the bank's EN->NL notes (CAUTION -> VOORZICHTIG,
    claim -> conclusie) get applied to an NL->EN job. That is how the first
    run scored 0/4: almost nothing applicable, and what applied pointed the
    wrong way.
    """
    pairs, skipped_dir = [], 0
    tdir = Path(bank_dir) / "02_TERMINOLOGY"
    if not tdir.is_dir():
        return pairs, 0
    for f in sorted(tdir.glob("*.md")):
        if f.name.startswith("_EXAMPLE_"):
            continue
        try:
            fm = parse_frontmatter(f.read_text(encoding="utf-8", errors="replace"))
        except Exception:
            continue
        if src_base and tgt_base:
            ns, nt = note_direction(fm)
            # Keep notes that state no direction; drop ones stating the wrong one.
            if ns and nt and not (ns == src_base and nt == tgt_base):
                skipped_dir += 1
                continue
        src, tgt = fm.get("term_source"), fm.get("term_target")
        # Skip context-dependent decisions ("divider (machinery) / distributor
        # (sales)") - they cannot be scored by string matching without knowing
        # which sense applies, and guessing would fabricate a result.
        if src and tgt and "/" not in tgt and "(" not in tgt:
            pairs.append((src.strip(), tgt.strip()))
    return pairs, skipped_dir


def term_compliance(source, candidate, pairs):
    """(honoured, applicable) for the term decisions this segment could exercise."""
    s, c = source.lower(), (candidate or "").lower()
    applicable = honoured = 0
    for src, tgt in pairs:
        if len(src) < 3 or src.lower() not in s:
            continue
        applicable += 1
        if tgt.lower() in c:
            honoured += 1
    return honoured, applicable


def bootstrap_ci(deltas, iters=2000, seed=12345):
    """Paired bootstrap on per-segment deltas. Returns (mean, lo, hi) at 95%."""
    if not deltas:
        return (0.0, 0.0, 0.0)
    rng = random.Random(seed)
    n = len(deltas)
    means = []
    for _ in range(iters):
        means.append(sum(deltas[rng.randrange(n)] for _ in range(n)) / n)
    means.sort()
    return (sum(deltas) / n, means[int(0.025 * iters)], means[int(0.975 * iters)])


# ───────────────────────────── translation ─────────────────────────────

SYSTEM_BASE = (
    "You are a professional {src} to {tgt} translator working in a CAT tool.\n"
    "Translate each numbered source segment. Return ONLY the translations, one per "
    "line, in the form '<number>. <translation>'. No commentary, no source text, no "
    "blank lines between entries. Preserve any inline tags exactly as they appear."
)


def build_system(src_lang, tgt_lang, kb_context):
    prompt = SYSTEM_BASE.format(src=src_lang, tgt=tgt_lang)
    if kb_context:
        prompt += "\n\n" + kb_context
    return prompt


def call_model(api_key, model, system, user, max_tokens=4000, temperature=None):
    payload = {
        "model": model,
        "max_tokens": max_tokens,
        "system": system,
        "messages": [{"role": "user", "content": user}],
    }
    # Newer models reject `temperature` outright ("deprecated for this model"),
    # so it is omitted unless explicitly asked for. Both arms are sampled the
    # same way either way, which is what the comparison needs; it only means a
    # re-run may differ slightly from the last one.
    if temperature is not None:
        payload["temperature"] = temperature
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(API_URL, data=body, headers={
        "x-api-key": api_key,
        "anthropic-version": "2023-06-01",
        "content-type": "application/json",
    })
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            text = "".join(b.get("text", "") for b in data.get("content", []))
            usage = data.get("usage", {})
            return text, usage.get("input_tokens", 0), usage.get("output_tokens", 0)
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503, 529) and attempt < 3:
                time.sleep(2 ** attempt * 2)
                continue
            sys.exit(f"API error {e.code}: {e.read().decode('utf-8', 'replace')[:400]}")
        except urllib.error.URLError as e:
            if attempt < 3:
                time.sleep(2 ** attempt * 2)
                continue
            sys.exit(f"Network error: {e}")
    return "", 0, 0


def parse_numbered(text, expected):
    out = {}
    for line in text.splitlines():
        m = re.match(r"\s*(\d+)\s*[.)]\s*(.+?)\s*$", line)
        if m:
            out[int(m.group(1))] = m.group(2)
    return [out.get(i) for i in range(1, expected + 1)]


def translate_all(api_key, model, system, segments, batch_size, label, verbose, temperature=None):
    """Returns (list of translations aligned to segments, in_tokens, out_tokens)."""
    results, tin, tout = [], 0, 0
    for start in range(0, len(segments), batch_size):
        chunk = segments[start:start + batch_size]
        user = "\n".join(f"{i}. {s['source']}" for i, s in enumerate(chunk, 1))
        text, i_tok, o_tok = call_model(api_key, model, system, user, temperature=temperature)
        tin += i_tok
        tout += o_tok
        parsed = parse_numbered(text, len(chunk))
        results.extend(parsed)
        if verbose:
            got = sum(1 for p in parsed if p)
            print(f"  [{label}] segments {start + 1}-{start + len(chunk)}: {got}/{len(chunk)} parsed")
    return results, tin, tout


# ───────────────────────────── main ─────────────────────────────

def main():
    ap = argparse.ArgumentParser(description="A/B benchmark for the SuperMemory memory bank.")
    ap.add_argument("--limit", type=int, default=40, help="segments to test (default 40)")
    ap.add_argument("--batch-size", type=int, default=10, help="segments per API call (default 10)")
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--token-budget", type=int, default=6000, help="memory-bank token budget")
    ap.add_argument("--client", default=None, help="client name, if project-name detection fails")
    ap.add_argument("--domain", default=None,
                    help="override the auto-detected domain. Run once without and once with: "
                         "the difference separates 'is the bank useful?' from 'does domain "
                         "detection pick the right article?', which are different problems.")
    ap.add_argument("--min-words", type=int, default=6, help="skip segments shorter than this")
    ap.add_argument("--include-tm-matches", action="store_true",
                    help="include 100%% TM matches (excluded by default - they come from the TM, not the model)")
    ap.add_argument("--out", default="benchmark-report",
                    help="output basename. A bare name lands in --out-dir; pass a path "
                         "(absolute or with a separator) to place it yourself.")
    ap.add_argument("--out-dir", default=None,
                    help=f"where reports go (default: {default_output_dir()}). Never "
                         "defaults next to this script: the reports quote the job's "
                         "source and target verbatim and this repo is public.")
    ap.add_argument("--allow-repo-output", action="store_true",
                    help="permit writing into a git working tree. Only for a private repo.")
    ap.add_argument("--dry-run", action="store_true", help="check wiring, make no API calls")
    ap.add_argument("--yes", action="store_true", help="proceed without the cost prompt")
    ap.add_argument("--repeats", type=int, default=1,
                    help="runs per arm, averaged per segment (default 1). The first "
                         "benchmark showed the identical no-bank condition drifting "
                         "0.02 between runs, larger than the effect. Use 3+ for a verdict.")
    ap.add_argument("--temperature", type=float, default=None,
                    help="send an explicit temperature. Omitted by default: newer models "
                         "reject the parameter. Both arms are always sampled identically.")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    # Resolved before anything is spent: a run whose report would land in a
    # repo must fail now, not after the API bill and the translating.
    out_base = resolve_output_base(args.out, args.out_dir, args.allow_repo_output)

    base, token = discover_bridge()
    print(f"Bridge: {base}")

    ctx = bridge_get(base, token, "/v1/active-context")
    proj = (ctx.get("project") or {}) if ctx.get("available") else {}
    src_lang = proj.get("sourceLang") or "the source language"
    tgt_lang = proj.get("targetLang") or "the target language"
    print(f"Project: {proj.get('name', '?')} [{src_lang} -> {tgt_lang}]  file: {proj.get('fileName', '?')}")

    # ── reference segments ───────────────────────────────────────────
    raw = bridge_get(base, token, "/v1/segments",
                     {"status": "Confirmed", "limit": max(args.limit * 5, 200)})
    records = raw.get("segments") or []
    if not records:
        raw = bridge_get(base, token, "/v1/segments", {"limit": max(args.limit * 5, 200)})
        records = raw.get("segments") or []

    segments = []
    skipped_tm = 0
    for r in records:
        src, tgt = (r.get("source") or "").strip(), (r.get("target") or "").strip()
        if not src or not tgt:
            continue
        if len(src.split()) < args.min_words:
            continue
        if not args.include_tm_matches and (r.get("match") or 0) >= 100:
            skipped_tm += 1
            continue
        segments.append({"id": r.get("id"), "number": r.get("number"),
                         "source": src, "reference": tgt})
        if len(segments) >= args.limit:
            break

    if not segments:
        sys.exit("No usable confirmed segments found. Open a finished file in the editor.")
    print(f"Reference segments: {len(segments)}"
          + (f"  ({skipped_tm} 100% TM matches excluded)" if skipped_tm else ""))

    # ── memory-bank context, via the plugin's own retrieval ──────────
    sm = bridge_get(base, token, "/v1/supermemory-context",
                    {"tokenBudget": args.token_budget, "client": args.client,
                     "domain": args.domain})
    kb = sm.get("context") if sm.get("available") else None
    if kb:
        print(f"Memory bank: {sm.get('bank')} | domain: {sm.get('domain')} | "
              f"client: {sm.get('client') or 'none'} | {len(sm.get('sources') or [])} articles, "
              f"{len(kb):,} chars")
    else:
        print(f"Memory bank: NO CONTEXT RETURNED - {sm.get('note', 'unknown reason')}")
        print("Without a knowledge-base block both arms are identical; nothing to measure.")
        if not args.dry_run:
            sys.exit(1)

    bank_root = bridge_get(base, token, "/v1/supermemory-banks")
    active = next((b for b in bank_root.get("banks", []) if b.get("active")), None)
    bank_dir = Path(bank_root.get("root", "")) / (active or {}).get("name", "")
    src_base, tgt_base = base_lang(src_lang), base_lang(tgt_lang)
    pairs, skipped_dir = load_term_pairs(bank_dir, src_base, tgt_base)
    print(f"Scoreable term decisions for {src_base}->{tgt_base}: {len(pairs)}"
          + (f"  ({skipped_dir} skipped as wrong-direction)" if skipped_dir else ""))

    est_in = (len(segments) / args.batch_size) * (len(kb or "") / 3.5 + 400) * 2 * args.repeats
    est_out = len(segments) * 60 * 2 * args.repeats
    est = est_in / 1e6 * PRICE_IN + est_out / 1e6 * PRICE_OUT
    print(f"\nEstimated cost: ~${est:.2f} ({args.model}, both arms)")

    if args.dry_run:
        print("\nDry run - wiring verified, no API calls made.")
        return

    if not args.yes:
        if input("Proceed? [y/N] ").strip().lower() not in ("y", "yes"):
            return

    # ── the two arms ─────────────────────────────────────────────────
    sys_with = build_system(src_lang, tgt_lang, kb)
    sys_without = build_system(src_lang, tgt_lang, None)

    key = os.environ["ANTHROPIC_API_KEY"]
    runs_wo, runs_kb = [], []
    i1 = o1 = i2 = o2 = 0
    for rep in range(args.repeats):
        tag = f" (repeat {rep + 1}/{args.repeats})" if args.repeats > 1 else ""
        print(f"\nArm A: WITHOUT memory bank{tag}")
        r, a, b = translate_all(key, args.model, sys_without, segments,
                                args.batch_size, "without", args.verbose, args.temperature)
        runs_wo.append(r); i1 += a; o1 += b
        print(f"Arm B: WITH memory bank{tag}")
        r, a, b = translate_all(key, args.model, sys_with, segments,
                                args.batch_size, "with", args.verbose, args.temperature)
        runs_kb.append(r); i2 += a; o2 += b

    # Per-repeat baseline means are a direct read of the noise floor: if these
    # scatter as much as the with/without gap, the gap is not a result.
    baselines = []
    for r in runs_wo:
        vals = [edit_rate(c, s["reference"]) for c, s in zip(r, segments) if c]
        vals = [v for v in vals if v is not None]
        if vals:
            baselines.append(sum(vals) / len(vals))
    if len(baselines) > 1:
        print("\nNo-bank baseline per repeat: "
              + ", ".join(f"{x:.4f}" for x in baselines)
              + f"  (spread {max(baselines) - min(baselines):.4f})")

    def mean_of(runs, idx):
        vals = [edit_rate(run[idx], segments[idx]["reference"]) for run in runs if run[idx]]
        vals = [v for v in vals if v is not None]
        return (sum(vals) / len(vals)) if vals else None

    without = [next((run[i] for run in runs_wo if run[i]), None) for i in range(len(segments))]
    withkb = [next((run[i] for run in runs_kb if run[i]), None) for i in range(len(segments))]

    # ── score (a segment must succeed in BOTH arms to count) ─────────
    rows, deltas = [], []
    tc_with = tc_without = tc_total = 0
    for idx, (seg, a, b) in enumerate(zip(segments, without, withkb)):
        if not a or not b:
            continue
        # Averaged across repeats, so one unlucky sample cannot decide a segment.
        ra, rb = mean_of(runs_wo, idx), mean_of(runs_kb, idx)
        if ra is None or rb is None:
            continue
        hw, ap = term_compliance(seg["source"], b, pairs)
        ho, _ = term_compliance(seg["source"], a, pairs)
        tc_with += hw
        tc_without += ho
        tc_total += ap
        deltas.append(ra - rb)  # positive = the bank helped
        rows.append({"number": seg["number"], "id": seg["id"], "source": seg["source"],
                     "reference": seg["reference"], "without": a, "with": b,
                     "edit_without": round(ra, 4), "edit_with": round(rb, 4),
                     "delta": round(ra - rb, 4), "terms_applicable": ap,
                     "terms_ok_without": ho, "terms_ok_with": hw})

    if not rows:
        sys.exit("No segments scored in both arms - check the parse output with --verbose.")

    mean_wo = sum(r["edit_without"] for r in rows) / len(rows)
    mean_w = sum(r["edit_with"] for r in rows) / len(rows)
    mean_d, lo, hi = bootstrap_ci(deltas)
    better = sum(1 for d in deltas if d > 0.001)
    worse = sum(1 for d in deltas if d < -0.001)
    same = len(deltas) - better - worse

    verdict = ("memory bank HELPED" if lo > 0 else
               "memory bank HURT" if hi < 0 else
               "NO MEASURABLE DIFFERENCE (interval crosses zero)")

    print("\n" + "=" * 62)
    print(f"Segments scored : {len(rows)}")
    print(f"Edit rate without: {mean_wo:.3f}")
    print(f"Edit rate with   : {mean_w:.3f}")
    print(f"Mean improvement : {mean_d:+.4f}  (95% CI {lo:+.4f} .. {hi:+.4f})")
    print(f"Segments better/worse/unchanged: {better}/{worse}/{same}")
    if tc_total:
        print(f"Term compliance  : without {tc_without}/{tc_total} "
              f"({tc_without / tc_total:.0%}) | with {tc_with}/{tc_total} ({tc_with / tc_total:.0%})")
    else:
        print("Term compliance  : no bank term decisions applied to these segments")
    print(f"VERDICT: {verdict}")
    print(f"Tokens: {i1 + i2:,} in / {o1 + o2:,} out  "
          f"(~${(i1 + i2) / 1e6 * PRICE_IN + (o1 + o2) / 1e6 * PRICE_OUT:.2f})")
    print("=" * 62)

    # ── artefacts ────────────────────────────────────────────────────
    import csv
    # str(out_base) + suffix, not with_suffix: a basename containing a dot
    # ("run.v2") would otherwise lose everything after it.
    csv_path = Path(f"{out_base}.csv")
    with csv_path.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    md_path = Path(f"{out_base}.md")
    worst = sorted(rows, key=lambda r: r["delta"])[:5]
    best = sorted(rows, key=lambda r: -r["delta"])[:5]
    with md_path.open("w", encoding="utf-8") as f:
        f.write(f"# SuperMemory benchmark — {proj.get('name', '?')}\n\n")
        f.write(f"- Model: `{args.model}`"
                + (f", temperature {args.temperature}\n" if args.temperature is not None
                   else ", default sampling (both arms identical)\n"))
        f.write(f"- Repeats per arm: {args.repeats}"
                + ((" (no-bank baseline per repeat: "
                    + ", ".join(f"{x:.4f}" for x in baselines) + ")\n")
                   if len(baselines) > 1 else "\n"))
        f.write(f"- Segments scored: {len(rows)}"
                + (f" ({skipped_tm} 100% TM matches excluded)\n" if skipped_tm else "\n"))
        f.write(f"- Bank: `{sm.get('bank')}`, domain `{sm.get('domain')}`, "
                f"{len(sm.get('sources') or [])} articles, {len(kb or ''):,} chars\n\n")
        f.write("## Result\n\n")
        f.write("| Metric | Without bank | With bank |\n|---|---|---|\n")
        f.write(f"| Mean edit rate (lower is better) | {mean_wo:.3f} | {mean_w:.3f} |\n")
        if tc_total:
            f.write(f"| Term compliance | {tc_without}/{tc_total} ({tc_without / tc_total:.0%}) "
                    f"| {tc_with}/{tc_total} ({tc_with / tc_total:.0%}) |\n")
        f.write(f"\nMean improvement **{mean_d:+.4f}**, 95% CI {lo:+.4f} .. {hi:+.4f}. "
                f"Better/worse/unchanged: {better}/{worse}/{same}.\n\n")
        f.write(f"**{verdict}**\n\n")
        for title, items in (("Biggest wins", best), ("Biggest losses", worst)):
            f.write(f"## {title}\n\n")
            for r in items:
                f.write(f"**Segment {r['number']}** (delta {r['delta']:+.3f})\n\n")
                f.write(f"- Source: {r['source']}\n- Confirmed: {r['reference']}\n"
                        f"- Without: {r['without']}\n- With: {r['with']}\n\n")
        f.write("## Caveats\n\n"
                "- The reference is the translator's confirmed target. If those segments were "
                "themselves produced with the memory bank switched on, the comparison is biased "
                "towards the bank. Prefer a file translated before the bank existed.\n"
                "- Edit rate measures distance from the confirmed wording, not quality. A better "
                "translation that differs from the reference scores worse.\n"
                "- Term compliance skips context-dependent decisions, which cannot be scored by "
                "string matching.\n"
                "- One run per arm. Re-run with a different `--limit` slice before trusting a "
                "small difference.\n")

    print(f"\nWrote {md_path} and {csv_path}")


if __name__ == "__main__":
    main()
