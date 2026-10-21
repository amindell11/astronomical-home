"""Survival mean +- standard error per tag and N. Usage: python surv.py <run.jsonl>..."""
import json
import statistics
import sys
from collections import defaultdict

groups = defaultdict(list)
order = []
for p in sys.argv[1:]:
    for line in open(p, encoding="utf-8"):
        if not line.strip():
            continue
        r = json.loads(line)
        k = r["tag"]
        if k not in groups:
            order.append(k)
        groups[k].append(r)

ns = (1, 2, 3, 5)
print("| Tag | " + " | ".join(f"N={n}" for n in ns) + " | Dmg/s N=1..5 |")
for k in order:
    cells = []
    dps = []
    for n in ns:
        rows = [r for r in groups[k] if r["n"] == n]
        if not rows:
            cells.append("")
            dps.append("")
            continue
        s = [r["survival"] for r in rows]
        se = statistics.stdev(s) / len(s) ** 0.5 if len(s) > 1 else 0
        cells.append(f"{statistics.mean(s):.0f} ± {se:.0f}")
        dps.append(f"{sum(r['damageTaken'] for r in rows) / sum(s):.1f}")
    print(f"| {k} | " + " | ".join(cells) + " | " + " / ".join(dps) + " |")
