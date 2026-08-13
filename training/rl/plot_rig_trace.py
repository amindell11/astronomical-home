"""Render a solver-rig trace CSV (RigTraceCsv) to a PNG: yaw/facing, range, and per-term costs vs t.

Usage: python plot_rig_trace.py trace.csv [more.csv ...] [--out-dir DIR]
       python plot_rig_trace.py --in-dir ../../results/mpc-rig/bingo
"""
import argparse
import csv
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

COST_COLUMNS = [
    ("costFacing", "facing"),
    ("costFacingPrior", "facing prior"),
    ("costPos", "pos"),
    ("costVelocityTrack", "vel track"),
    ("costObstacle", "obstacle"),
    ("costCollision", "collision"),
    ("costYawRate", "yaw rate"),
    ("costMomentum", "momentum"),
    ("costEffort", "effort"),
    ("costSmoothness", "smoothness"),
]


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("csvs", nargs="*", type=Path, help="trace CSVs written by RigTraceCsv")
    parser.add_argument("--in-dir", type=Path, help="plot every *.csv in this directory")
    parser.add_argument("--out-dir", type=Path, help="PNG destination (default: beside each CSV)")
    args = parser.parse_args()
    if args.in_dir:
        args.csvs.extend(sorted(args.in_dir.glob("*.csv")))
    if not args.csvs:
        parser.error("no trace CSVs given (pass paths or --in-dir)")
    return args


def read_trace(path: Path) -> dict:
    with path.open(newline="") as f:
        rows = list(csv.DictReader(f))
    if not rows:
        raise SystemExit(f"{path}: empty trace")
    return {key: [float(r[key]) for r in rows] for key in rows[0]}


def finite(ts, vals):
    """Drop NaN samples (anchorless / rangeless ticks) so lines break cleanly."""
    pairs = [(t, v) for t, v in zip(ts, vals) if math.isfinite(v)]
    return [p[0] for p in pairs], [p[1] for p in pairs]


def plot_trace(path: Path, out_dir: Path | None):
    data = read_trace(path)
    t = data["t"]

    fig, axes = plt.subplots(4, 1, figsize=(12, 14), sharex=True)
    fig.suptitle(path.stem)

    ax = axes[0]
    ax.plot(t, data["yawDeg"], label="yaw", linewidth=0.9)
    ax.plot(*finite(t, data["anchorYawDeg"]), label="anchor yaw", linewidth=0.9, alpha=0.8)
    ax.set_ylabel("deg")
    ax.legend(loc="upper right", fontsize=8)

    ax = axes[1]
    ax.plot(*finite(t, data["facingErrorDeg"]), color="tab:red", linewidth=0.9)
    ax.set_ylabel("facing error (deg)")

    ax = axes[2]
    ax.plot(*finite(t, data["range"]), color="tab:green", linewidth=0.9)
    ax.set_ylabel("range (m)")
    if any(v > 0 for v in data["underThreat"]):
        threat_t = [ti for ti, u in zip(t, data["underThreat"]) if u > 0]
        ax.plot(threat_t, [0] * len(threat_t), "|", color="tab:orange", markersize=4, label="under threat")
        ax.legend(loc="upper right", fontsize=8)

    ax = axes[3]
    for key, label in COST_COLUMNS:
        vals = data.get(key)
        if vals and any(v != 0 for v in vals):
            ax.plot(t, vals, label=label, linewidth=0.8)
    ax.set_ylabel("per-term cost")
    ax.set_xlabel("t (s)")
    ax.set_yscale("symlog", linthresh=0.01)
    ax.legend(loc="upper right", fontsize=8, ncol=2)

    out = (out_dir or path.parent) / f"{path.stem}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out, dpi=110, bbox_inches="tight")
    plt.close(fig)
    print(f"wrote {out}")


def main():
    args = parse_args()
    for path in args.csvs:
        plot_trace(path, args.out_dir)


if __name__ == "__main__":
    main()
