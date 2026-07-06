#!/usr/bin/env python3
"""Aggregate / compare chase-benchmark JSONL result sets (Track B1).

Each input is a JSONL file (one episode per line, schema chase-bench-v1) or a
directory (globs *.jsonl). Prints per-metric mean +/- std over the episodes.
With two inputs, prints baseline vs candidate side by side with deltas.

Usage:
  python compare_chase_benchmark.py results/chase-benchmark/baseline.jsonl
  python compare_chase_benchmark.py baseline.jsonl candidate.jsonl
"""
import json
import math
import sys
from pathlib import Path

SHIP_METRICS = [
    ("collisions", "collisions/ep"),
    ("impactImpulse", "impact impulse"),
    ("meanSpeed", "mean speed"),
    ("chatterThrust", "chatter thrust /s"),
    ("chatterStrafe", "chatter strafe /s"),
    ("chatterYaw", "chatter yaw /s"),
    ("meanSolveMs", "solve ms (mean)"),
    ("maxSolveMs", "solve ms (max)"),
]
CHASE_METRICS = [
    ("meanDistance", "mean distance"),
    ("minDistance", "min distance"),
    ("finalDistance", "final distance"),
    ("contactTime", "contact time s"),
]


def load(path_str):
    path = Path(path_str)
    files = sorted(path.glob("*.jsonl")) if path.is_dir() else [path]
    rows = []
    for f in files:
        for line in f.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            if row.get("schema") != "chase-bench-v1":
                print(f"warning: skipping row with schema {row.get('schema')!r} in {f}", file=sys.stderr)
                continue
            rows.append(row)
    if not rows:
        sys.exit(f"no chase-bench-v1 rows found in {path_str}")
    return rows


def mean_std(values):
    n = len(values)
    m = sum(values) / n
    var = sum((v - m) ** 2 for v in values) / (n - 1) if n > 1 else 0.0
    return m, math.sqrt(var)


def collect(rows):
    out = {}
    for ship in ("pursuer", "evader"):
        for key, label in SHIP_METRICS:
            out[f"{ship}.{key}"] = (f"{ship} {label}", mean_std([r[ship][key] for r in rows]))
    for key, label in CHASE_METRICS:
        out[f"chase.{key}"] = (f"chase {label} (secondary)", mean_std([r[key] for r in rows]))
    return out


def fmt(ms):
    m, s = ms
    return f"{m:9.3f} +/- {s:7.3f}"


def main(argv):
    if len(argv) not in (2, 3):
        sys.exit(__doc__)

    base_rows = load(argv[1])
    base = collect(base_rows)

    if len(argv) == 2:
        print(f"# {argv[1]}  ({len(base_rows)} episodes)")
        print(f"{'metric':38} {'mean +/- std':>22}")
        for _, (label, ms) in base.items():
            print(f"{label:38} {fmt(ms):>22}")
        return

    cand_rows = load(argv[2])
    cand = collect(cand_rows)
    print(f"# baseline: {argv[1]} ({len(base_rows)} eps) | candidate: {argv[2]} ({len(cand_rows)} eps)")
    print(f"{'metric':38} {'baseline':>22} {'candidate':>22} {'delta':>10}")
    for key, (label, bms) in base.items():
        cms = cand[key][1]
        delta = cms[0] - bms[0]
        print(f"{label:38} {fmt(bms):>22} {fmt(cms):>22} {delta:>+10.3f}")


if __name__ == "__main__":
    main(sys.argv)
