"""Experiment A table: per roster row and replicate, read the matrix probe rows; print markdown."""
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])


def read(rep, row):
    files = sorted((root / f"rep{rep}" / row).rglob("*-matrix.jsonl"))
    if len(files) != 1:
        return None
    rows = [json.loads(line) for line in files[0].read_text(encoding="utf-8").splitlines() if line.strip()]
    n = len(rows)
    sec = sum(r["simSeconds"] for r in rows)
    dmg_b = sum(r["tallyB"]["damageDealt"] for r in rows)
    dmg_a = sum(r["tallyA"]["damageDealt"] for r in rows)
    died_b = [r for r in rows if r["diedB"]]
    died_a = [r for r in rows if r["diedA"]]
    fired_b = sum(r["tallyB"]["fired"] for r in rows)
    hits_b = sum(r["tallyB"]["hits"] for r in rows)
    return {
        "n": n,
        "b_dpm": 60 * dmg_b / sec if sec else 0.0,
        "a_dpm": 60 * dmg_a / sec if sec else 0.0,
        "b_killed": len(died_b) / n if n else 0.0,
        "b_ttd": sum(r["simSeconds"] for r in died_b) / len(died_b) if died_b else None,
        "a_killed": len(died_a) / n if n else 0.0,
        "b_hit": 100 * hits_b / fired_b if fired_b else 0.0,
        "sec": sec / n if n else 0.0,
        "per_episode": [(r["seed"], r["episodeIndex"], r["winner"], round(r["simSeconds"], 2)) for r in rows],
    }


def cell(a, b, fmt="{:.0f}"):
    if a is None or b is None:
        return "-" if a is None and b is None else f"{fmt.format(a) if a is not None else '-'} / {fmt.format(b) if b is not None else '-'}"
    return f"{fmt.format((a + b) / 2)} (±{fmt.format(abs(a - b))})"


names = sorted({p.name for p in root.glob("rep*/*") if p.is_dir()})
print("| Roster ship | n/rep | dmg/min dealt | killed % | sec to die | ref dmg/min | ref killed % | hit % | identical episodes |")
print("|---|--:|--:|--:|--:|--:|--:|--:|--:|")
for name in names:
    r1, r2 = read(1, name), read(2, name)
    if not r1 or not r2:
        print(f"| {name} | missing rep ({'1' if not r1 else ''}{'2' if not r2 else ''}) |")
        continue
    same = sum(1 for x, y in zip(r1["per_episode"], r2["per_episode"]) if x == y)
    print(f"| {name} | {r1['n']} | {cell(r1['b_dpm'], r2['b_dpm'])} | {cell(100 * r1['b_killed'], 100 * r2['b_killed'])} | "
          f"{cell(r1['b_ttd'], r2['b_ttd'], '{:.1f}')} | {cell(r1['a_dpm'], r2['a_dpm'])} | "
          f"{cell(100 * r1['a_killed'], 100 * r2['a_killed'])} | {cell(r1['b_hit'], r2['b_hit'], '{:.1f}')} | "
          f"{same}/{r1['n']} |")
