"""Smart (side A) vs baseline (side B) cross: A's score per (A weapon, B weapon), draws 0.5.

Usage: python h2h.py <h2h.jsonl>
"""
import json
import sys
from collections import defaultdict

W = ["Lasers", "TurboLasers", "Rippers", "ChargeLasers", "Railgun", "Missiles", "Grenades"]
rows = [json.loads(l.strip().lstrip("﻿")) for l in open(sys.argv[1], encoding="utf-8-sig") if l.strip()]
score = defaultdict(lambda: [0.0, 0])
dps = defaultdict(lambda: [0.0, 0.0])
for r in rows:
    wa, wb = r["a"]["weapon"], r["b"]["weapon"]
    w = r["winner"]
    score[(wa, wb)][0] += 1.0 if w == "A" else 0.0 if w == "B" else 0.5
    score[(wa, wb)][1] += 1
    dps[wa][0] += r["a"]["tally"]["damageDealt"]
    dps[wa][1] += r["duration"]
print("| smart \\ baseline | " + " | ".join(W) + " | mean |")
print("|" + "---|" * (len(W) + 2))
for a in W:
    cells, tot, n = [], 0.0, 0
    for b in W:
        s, k = score.get((a, b), (0, 0))
        cells.append(f"{s / k:.2f}" if k else "-")
        if k:
            tot += s / k
            n += 1
    print(f"| {a} | " + " | ".join(cells) + f" | {tot / n if n else 0:.2f} |")
