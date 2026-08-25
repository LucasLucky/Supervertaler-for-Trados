"""Reverse 31 specifically identified term entries that are stored back-to-front.

WHY AN EXPLICIT ID LIST, and not a heuristic
--------------------------------------------
These 31 rows were found by flagging entries whose own `source_lang` /
`target_lang` contradict their termbase's declared direction, and then reading
every pair by hand. That reading was necessary, because the contradiction alone
does not say what is wrong:

  * most of them really are reversed — the TEXT sits in the wrong columns, so
    the entry is indexed under the wrong language and matches no source segment
    in either project direction. Silent, total loss of that term.
  * some have correct text and only wrong labels. Those match perfectly today,
    because every read path ignores the per-entry labels and orients by the
    termbase's declaration. Swapping one would BREAK an entry that works.
  * some are language-neutral (brand names, units, chemical formulae) or are
    not translation pairs at all — one is Dutch-to-Dutch, evidently a typo
    correction; another is an abbreviation and its expansion.

Of the 40 flagged rows on the database this was written for, 31 were the first
kind, 2 the second, and 7 the third. Only the 31 are listed here. The other 9
are deliberately left alone: they need a human decision, not a rule, and two of
them are working entries that a blanket swap would have destroyed.

The two general-purpose repair tools both decline this job for good reasons.
`repair_termbase_directions.py` classifies by stopword heuristic and returned
Category C (ambiguous) for all 108 candidates — single technical terms carry no
stopwords. `ai_repair_termbase_directions.py` would classify them with an LLM,
which is the right instrument when the list is long and unread; here the list is
short and has been read, so an explicit, reviewable list of ids beats a
classifier whose output nobody checks.

WHAT IT DOES
------------
Mirrors TermbaseReader.ReverseTermDirection (the plugin's own swap), per row:
  * source_term ↔ target_term
  * source_lang ↔ target_lang
  * source_abbreviation ↔ target_abbreviation
  * every linked synonym's language tag flipped ('source' ↔ 'target')
then rebuilds the termbase_terms_fts index, which is external-content with no
triggers and would otherwise keep serving the pre-swap text.

SAFETY
------
  * Dry run by default. `--apply` writes.
  * Each row carries its expected source and target text. If a row has been
    edited or deleted since this list was compiled, the script REFUSES THE
    WHOLE RUN rather than swapping something it no longer recognises.
  * Each row is re-checked at run time for the direction contradiction; if one
    has already been repaired, the run is refused rather than un-repairing it.
  * `--apply` copies the .db to a timestamped .bak first, then does all the
    work in a single transaction.

CLOSE SUPERVERTALER AND TRADOS FIRST. Both hold this database open, and the
plugin caches the term index in memory — a swap underneath a running Studio
would be invisible until the next reload.

Usage:
    python repair_reversed_entries_by_id.py <db_path>            # dry run
    python repair_reversed_entries_by_id.py <db_path> --apply
"""

from __future__ import annotations

import argparse
import os
import shutil
import sqlite3
import sys
import time

sys.stdout.reconfigure(errors="replace")
sys.stderr.reconfigure(errors="replace")


# (term id, expected source_term, expected target_term, termbase id)
#
# BEIJER is declared en → nl, so its source column should hold ENGLISH; every
# row below holds Dutch there. Acme (PROJ-001) is declared nl → en, so
# its source column should hold DUTCH; the three rows below hold English there.
ROWS = [
    # --- BEIJER (13), declared en → nl ---
    (21412, "proces", "process", 13),
    (21501, "bezinksel", "sediment", 13),
    (21556, "suspensie", "suspension", 13),
    (21639, "potlife", "pot life", 13),
    (21745, "methyl-ethylketoxime", "methyl ethyl ketoxime", 13),
    (21831, "ca.", "approx.", 13),
    (21858, "RV", "RH", 13),                       # relatieve vochtigheid → relative humidity
    (21868, "A-component", "A component", 13),
    (21869, "B-component", "B component", 13),
    (21881, "VOC-emissie", "VOC emission", 13),
    (21884, "n.b.", "unkn.", 13),                  # niet bekend
    (21885, "aluminium", "aluminum", 13),
    (21891, "oxime-crosslinker", "oxime crosslinker", 13),
    (21892, "2-butanonoxime", "2-butanone oxime", 13),
    (21901, "n.b.", "n.r.", 13),
    (21902, "n.b.", "n.r. (not reported)", 13),
    (21952, "alkylbenzols", "alkylbenzenes", 13),
    (22186, "ATV’s", "ATVs", 13),                  # Dutch plural apostrophe
    (22288, "AI-model", "AI model", 13),
    (22575, "m³/u", "m³/h", 13),                   # uur → hour
    (23216, "opsplitsing", "split", 13),
    (23493, "gras", "grass", 13),
    (23552, "kap", "cap", 13),
    (85495, "subcomponenten", "subcomponents", 13),
    (85512, "systeem", "system", 13),
    (93142, "antioxidatieve", "antioxidative", 13),
    (93143, "ontstekingsremmende", "anti-inflammatory", 13),
    (93144, "antimicrobiële", "antimicrobial", 13),
    # --- Acme (PROJ-001) (23), declared nl → en ---
    (21609, "Mashup applications", "mashup-applicaties", 23),
    (21611, "information processing systems", "informatieverwerkingssystemen", 23),
    (21612, "methodology", "methodologie", 23),
]

# Rows deliberately NOT in the list above, recorded so the decision is not lost
# and nobody "completes" the set later without re-reading them:
#
#   85677  bond → verbinding        text correct, labels wrong — WORKS TODAY
#   85854  xylene → xyleen          text correct, labels wrong — WORKS TODAY
#   21180  “DNV” → DNV              brand; differs only in quote glyphs
#   21476  3b” → 3b″                typographic
#   21477  3a” → 3a″                typographic
#   21672  aminoethyl-… → …         chemical name, language-neutral
#   21676  α,ω-… → …                chemical name, language-neutral
#   22501  MVR → mechanical vapor recompression   abbreviation and expansion
#   23169  luchtdoorvoerkleppen → luchttoevoerkleppen   both Dutch; a typo fix


def normalize_lang(lang):
    """Collapse variants ('nl-BE', 'Dutch (Belgium)') → 'nl' / 'en'."""
    if not lang:
        return None
    lower = lang.strip().lower()
    if lower.startswith(("nl", "dutch", "flemish")):
        return "nl"
    if lower.startswith(("en", "english")):
        return "en"
    return lower


def has_column(conn, table, column):
    return any(r[1] == column for r in conn.execute(f"PRAGMA table_info({table})"))


def verify(conn):
    """Check every listed row is present, unchanged, and still reversed.

    Returns (plan, problems). `plan` is only usable when `problems` is empty.
    """
    plan, problems = [], []

    termbases = {
        r["id"]: (r["name"], r["source_lang"], r["target_lang"])
        for r in conn.execute("SELECT id, name, source_lang, target_lang FROM termbases")
    }

    for term_id, want_src, want_tgt, want_tb in ROWS:
        row = conn.execute(
            "SELECT id, source_term, target_term, source_lang, target_lang, termbase_id "
            "FROM termbase_terms WHERE id = ?",
            (term_id,),
        ).fetchone()

        if row is None:
            problems.append(f"{term_id}: row no longer exists")
            continue
        if int(row["termbase_id"]) != want_tb:
            problems.append(
                f"{term_id}: now in termbase {row['termbase_id']}, expected {want_tb}")
            continue
        if row["source_term"] != want_src or row["target_term"] != want_tgt:
            problems.append(
                f"{term_id}: text has changed since this list was compiled\n"
                f"          expected: {want_src!r} -> {want_tgt!r}\n"
                f"          found:    {row['source_term']!r} -> {row['target_term']!r}")
            continue

        tb = termbases.get(want_tb)
        if not tb:
            problems.append(f"{term_id}: termbase {want_tb} is missing")
            continue
        tb_name, tb_src, tb_tgt = tb
        e_src, e_tgt = normalize_lang(row["source_lang"]), normalize_lang(row["target_lang"])
        n_src, n_tgt = normalize_lang(tb_src), normalize_lang(tb_tgt)
        if not (e_src == n_tgt and e_tgt == n_src):
            problems.append(
                f"{term_id}: no longer contradicts its termbase "
                f"(entry {row['source_lang']}->{row['target_lang']}, "
                f"termbase {tb_src}->{tb_tgt}) — already repaired?")
            continue

        syns = conn.execute(
            "SELECT COUNT(*) FROM termbase_synonyms WHERE term_id = ?", (term_id,)
        ).fetchone()[0]

        plan.append({
            "id": term_id,
            "termbase": tb_name,
            "old_source": row["source_term"], "old_target": row["target_term"],
            "old_src_lang": row["source_lang"], "old_tgt_lang": row["target_lang"],
            "new_src_lang": tb_src, "new_tgt_lang": tb_tgt,
            "synonyms": syns,
        })

    return plan, problems


def report(plan):
    width = max((len(p["old_source"]) for p in plan), default=10)
    width = min(max(width, 12), 46)
    current_tb = None
    for p in plan:
        if p["termbase"] != current_tb:
            current_tb = p["termbase"]
            print(f"\n  {current_tb}")
        syn = f"  (+{p['synonyms']} synonym tag(s) flipped)" if p["synonyms"] else ""
        print(f"    {p['id']:>6}  {p['old_source'][:width]:<{width}} -> {p['old_target']}")
        print(f"    {'':>6}  becomes: {p['old_target']} -> {p['old_source']}"
              f"   [{p['old_src_lang']}->{p['old_tgt_lang']} becomes "
              f"{p['new_src_lang']}->{p['new_tgt_lang']}]{syn}")


def apply_swaps(conn, plan):
    has_abbr = (has_column(conn, "termbase_terms", "source_abbreviation")
                and has_column(conn, "termbase_terms", "target_abbreviation"))

    term_sql = """
        UPDATE termbase_terms
           SET source_term = target_term,
               target_term = source_term,
               source_lang = ?,
               target_lang = ?"""
    if has_abbr:
        term_sql += """,
               source_abbreviation = target_abbreviation,
               target_abbreviation = source_abbreviation"""
    term_sql += """,
               modified_date = CURRENT_TIMESTAMP
         WHERE id = ?"""

    syn_sql = """
        UPDATE termbase_synonyms
           SET language = CASE language
                            WHEN 'source' THEN 'target'
                            WHEN 'target' THEN 'source'
                            ELSE language
                          END
         WHERE term_id = ?"""

    written = 0
    conn.execute("BEGIN")
    try:
        for p in plan:
            # The lang columns are set to the TERMBASE's declared pair rather
            # than swapped, so a row whose tags were odd in some other way still
            # lands on the canonical direction.
            conn.execute(term_sql, (p["new_src_lang"], p["new_tgt_lang"], p["id"]))
            if p["synonyms"]:
                conn.execute(syn_sql, (p["id"],))
            written += 1
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    return written


def main():
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("db_path", help="Path to supervertaler.db")
    parser.add_argument("--apply", action="store_true",
                        help="Write the changes (default is a dry run).")
    parser.add_argument("--no-fts-rebuild", action="store_true",
                        help="Skip the termbase_terms_fts rebuild. Only if you know "
                             "the index is maintained some other way — otherwise "
                             "full-text search keeps serving the pre-swap text.")
    args = parser.parse_args()

    if not os.path.exists(args.db_path):
        print(f"Error: database not found: {args.db_path}")
        return 1

    conn = sqlite3.connect(args.db_path)
    conn.row_factory = sqlite3.Row

    plan, problems = verify(conn)

    print(f"Listed for repair: {len(ROWS)} entries")
    print(f"Verified and ready: {len(plan)}")

    if problems:
        print(f"\nREFUSING TO RUN — {len(problems)} row(s) are not as recorded:\n")
        for p in problems:
            print(f"  {p}")
        print("\nNothing was changed. The list in this script describes a database "
              "state that no longer holds; re-check those rows by hand before "
              "editing the list.")
        conn.close()
        return 2

    report(plan)

    if not args.apply:
        print(f"\nDRY RUN — nothing written. {len(plan)} entries would be swapped.")
        print("Re-run with --apply to write (Supervertaler and Trados must be closed).")
        conn.close()
        return 0

    conn.close()
    backup = f"{args.db_path}.pre_direction_repair_{time.strftime('%Y%m%d-%H%M%S')}.bak"
    shutil.copy2(args.db_path, backup)
    print(f"\nBackup written: {backup}")

    conn = sqlite3.connect(args.db_path)
    conn.row_factory = sqlite3.Row
    written = apply_swaps(conn, plan)
    print(f"Swapped {written} entries.")

    if not args.no_fts_rebuild:
        # termbase_terms_fts is FTS5 external-content over termbase_terms with
        # no sync triggers, so the UPDATEs above leave it holding the old text.
        conn.execute("INSERT INTO termbase_terms_fts(termbase_terms_fts) VALUES('rebuild')")
        conn.commit()
        print("Rebuilt termbase_terms_fts.")

    conn.close()
    print("\nDone. Restart Supervertaler / Trados so the in-memory term index reloads.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
