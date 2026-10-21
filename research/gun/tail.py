"""Side-by-side tables for the research-tail runs (2026-10-20).

Usage: python tail.py <results dir> marks <metric> <tag> [<tag> ...]
       python tail.py <results dir> matrix <tag> [<tag> ...]
       python tail.py <results dir> mirror <tag> [<tag> ...]
Marks metrics: pct (DPS % of ceiling), ttk, kills, aimerr, aimok, hit, selfdmg, selfhits, shots, dps.
"""
import json
import os
import sys
from collections import defaultdict

CEILING = {"Lasers": 37.5, "TurboLasers": 19.8, "Rippers": 36.9, "ChargeLasers": 42.9, "Railgun": 30.0,
           "Missiles": 4.7, "Grenades": 8.75}
WEAPONS = list(CEILING)


def load(d, tag):
    p = os.path.join(d, f"{tag}.jsonl")
    with open(p, encoding="utf-8-sig") as f:
        return [json.loads(l.strip().lstrip("﻿")) for l in f if l.strip()]


def marks_cells(rows):
    agg = defaultdict(lambda: defaultdict(float))
    for r in rows:
        w, t = r["pairing"].split("|")
        g = agg[(w, t)]
        a = r["a"]
        tal = a["tally"]
        g["n"] += 1
        if r["winner"] == "A":
            g["kills"] += 1
            g["ttk"] += r["duration"]
        g["time"] += r["duration"]
        g["dmg"] += tal["damageDealt"]
        g["fired"] += tal["fired"]
        g["hits"] += tal["hits"]
        g["steps"] += max(1, a["steps"])
        g["aimErr"] += a["meanAimErr"] * a["steps"]
        g["aimOk"] += a["aimOkFrac"] * a["steps"]
        g["selfHits"] += tal["selfHits"]
        g["selfDmg"] += a.get("selfDamage", 0.0)
        g["dist"] += a["meanDist"] * a["steps"]
    out = {}
    for k, g in agg.items():
        w = k[0]
        dps = g["dmg"] / g["time"] if g["time"] else 0.0
        out[k] = {
            "n": int(g["n"]), "kills": int(g["kills"]),
            "ttk": g["ttk"] / g["kills"] if g["kills"] else float("nan"),
            "dps": dps, "pct": 100 * dps / CEILING[w],
            "hit": 100 * g["hits"] / g["fired"] if g["fired"] else 0.0,
            "shots": g["fired"] / g["time"] if g["time"] else 0.0,
            "aimerr": g["aimErr"] / g["steps"], "aimok": 100 * g["aimOk"] / g["steps"],
            "selfhits": g["selfHits"] / g["n"], "selfdmg": g["selfDmg"] / g["n"],
            "dmg": g["dmg"] / g["n"], "dist": g["dist"] / g["steps"],
        }
    return out


def fmt(metric, c):
    if c is None:
        return "-"
    if metric == "kills":
        return f"{c['kills']}/{c['n']}"
    v = c[metric]
    if v != v:
        return "none"
    return f"{v:.0f}" if metric in ("pct", "aimok", "hit") else f"{v:.1f}" if metric != "shots" else f"{v:.2f}"


def marks_table(d, metric, tags):
    cells = {t: marks_cells(load(d, t)) for t in tags}
    keys = []
    for t in tags:
        for k in cells[t]:
            if k not in keys:
                keys.append(k)
    keys.sort(key=lambda k: (WEAPONS.index(k[0]) if k[0] in WEAPONS else 99, k[1]))
    print(f"\n### {metric}")
    print("| weapon | target | " + " | ".join(tags) + " |")
    print("|---|---|" + "---|" * len(tags))
    for k in keys:
        print(f"| {k[0]} | {k[1]} | " + " | ".join(fmt(metric, cells[t].get(k)) for t in tags) + " |")


def matrix_side(rows):
    """Per side-A weapon: win score vs others, % ceiling, DPS, self damage; plus per-cell A score."""
    agg = defaultdict(lambda: defaultdict(float))
    cell = defaultdict(lambda: [0.0, 0])
    both = defaultdict(lambda: defaultdict(float))
    for r in rows:
        a, b = r["a"], r["b"]
        wa, wb = a["weapon"], b["weapon"]
        w = r["winner"]
        sa = 1.0 if w == "A" else 0.0 if w == "B" else 0.5
        cell[(wa, wb)][0] += sa
        cell[(wa, wb)][1] += 1
        g = agg[wa]
        g["n"] += 1
        g["score"] += sa
        g["time"] += r["duration"]
        g["dmg"] += a["tally"]["damageDealt"]
        g["self"] += a.get("selfDamage", 0.0)
        g["taken"] += a.get("damageTaken", 0.0)
        g["fired"] += a["tally"]["fired"]
        g["hits"] += a["tally"]["hits"]
        if wa != wb:
            g["xn"] += 1
            g["xs"] += sa
        for side, opp in ((a, wb), (b, wa)):
            h = both[side["weapon"]]
            h["time"] += r["duration"]
            h["dmg"] += side["tally"]["damageDealt"]
            if opp != side["weapon"]:
                won = (w == "A" and side is a) or (w == "B" and side is b)
                h["xn"] += 1
                h["xs"] += 1.0 if won else 0.5 if w == "draw" else 0.0
    return agg, cell, both


def matrix_table(d, tags):
    print("\n### side A per weapon: score (all cells) / score vs others / % ceiling / self damage per fight")
    print("| weapon | " + " | ".join(tags) + " |")
    print("|---|" + "---|" * len(tags))
    res = {t: matrix_side(load(d, t)) for t in tags}
    ws = []
    for t in tags:
        for w in res[t][0]:
            if w not in ws:
                ws.append(w)
    ws.sort(key=lambda w: WEAPONS.index(w) if w in WEAPONS else 99)
    for w in ws:
        cells = []
        for t in tags:
            g = res[t][0].get(w)
            if not g:
                cells.append("-")
                continue
            pct = 100 * (g["dmg"] / g["time"]) / CEILING[w] if g["time"] else 0
            xs = g["xs"] / g["xn"] if g["xn"] else float("nan")
            cells.append(f"{g['score'] / g['n']:.2f} / {xs:.2f} / {pct:.0f}% / {g['self'] / g['n']:.0f}")
        print(f"| {w} | " + " | ".join(cells) + " |")
    print("\n### symmetric (both sides pooled): win vs others / % ceiling")
    print("| weapon | " + " | ".join(tags) + " |")
    print("|---|" + "---|" * len(tags))
    for w in ws:
        cells = []
        for t in tags:
            h = res[t][2].get(w)
            if not h:
                cells.append("-")
                continue
            pct = 100 * (h["dmg"] / h["time"]) / CEILING[w] if h["time"] else 0
            xs = h["xs"] / h["xn"] if h["xn"] else float("nan")
            cells.append(f"{xs:.2f} / {pct:.0f}%")
        print(f"| {w} | " + " | ".join(cells) + " |")
    for t in tags:
        cell = res[t][1]
        opps = []
        for (_, b) in cell:
            if b not in opps:
                opps.append(b)
        opps.sort(key=lambda w: WEAPONS.index(w) if w in WEAPONS else 99)
        print(f"\n### {t}: side A score by cell")
        print("| A \\ B | " + " | ".join(opps) + " |")
        print("|---|" + "---|" * len(opps))
        for w in ws:
            print(f"| {w} | " + " | ".join(
                f"{cell[(w, b)][0] / cell[(w, b)][1]:.2f}" if cell.get((w, b), [0, 0])[1] else "-" for b in opps) + " |")


def main():
    d, mode = sys.argv[1], sys.argv[2]
    if mode == "marks":
        marks_table(d, sys.argv[3], sys.argv[4:])
    else:
        matrix_table(d, sys.argv[3:])


if __name__ == "__main__":
    main()
