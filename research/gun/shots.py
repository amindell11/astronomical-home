"""Per-shot Railgun analysis: hit rate by aim error at fire, distance and crossing speed.

Usage: python shots.py <run.jsonl> [...]
"""
import json
import sys
from collections import defaultdict


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return [json.loads(l.strip().lstrip("﻿")) for l in f if l.strip()]


def bins(shots, key, edges):
    out = defaultdict(lambda: [0, 0])
    for s in shots:
        v = s[key]
        for lo, hi in zip(edges, edges[1:]):
            if lo <= v < hi:
                out[(lo, hi)][0] += s["hit"]
                out[(lo, hi)][1] += 1
                break
    return out


for path in sys.argv[1:]:
    rows = load(path)
    shots = []
    for r in rows:
        for side in (r["a"], r["b"]):
            if side.get("weapon") == "Railgun":
                for s in side.get("shots", []):
                    if s["hit"] < 0:
                        continue
                    s["absTargetBank"] = abs(s.get("targetBank", 0))
                    s["absSelfBank"] = abs(s.get("selfBank", 0))
                    shots.append(s)
    if not shots:
        print(f"{path}: no railgun shots")
        continue
    hits = sum(s["hit"] for s in shots)
    print(f"\n## {rows[0]['tag']}: {len(shots)} railgun shots, {100 * hits / len(shots):.0f}% hit")
    for key, edges in (("err", [0, 0.5, 1, 1.5, 2, 3, 5, 90]),
                       ("dist", [0, 10, 15, 20, 25, 30, 40, 60]),
                       ("cross", [0, 2, 5, 8, 12, 20, 40]),
                       ("absTargetBank", [0, 5, 10, 15, 20, 25, 30, 40]),
                       ("absSelfBank", [0, 5, 10, 15, 20, 25, 30, 40])):
        b = bins(shots, key, edges)
        cells = " | ".join(f"{lo}-{hi}: {h}/{n} ({100 * h / n:.0f}%)" for (lo, hi), (h, n) in sorted(b.items()))
        print(f"- {key}: {cells}")
