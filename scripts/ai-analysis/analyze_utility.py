#!/usr/bin/env python3
"""
AI Utility Log Analyzer
=======================
Parses JSONL logs from UtilityLogger and produces analysis reports.

Usage:
    python analyze_utility.py <log_file_or_directory> [--ship SHIP] [--top N]

Examples:
    python analyze_utility.py session.jsonl
    python analyze_utility.py ./AILogs/                    # analyze all files
    python analyze_utility.py ./AILogs/ --ship Ship_A1     # filter to one ship
    python analyze_utility.py session.jsonl --transitions   # transition analysis only
"""

import argparse
import json
import os
import sys
from collections import Counter, defaultdict
from pathlib import Path


def load_logs(path: str, ship_filter: str = None) -> list[dict]:
    """Load JSONL log entries from a file or directory."""
    entries = []
    p = Path(path)
    files = list(p.glob("*.jsonl")) if p.is_dir() else [p]

    for f in files:
        with open(f, "r") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    entry = json.loads(line)
                    if ship_filter and entry.get("ship") != ship_filter:
                        continue
                    entries.append(entry)
                except json.JSONDecodeError:
                    continue

    entries.sort(key=lambda e: e.get("t", 0))
    return entries


def state_distribution(entries: list[dict]) -> None:
    """Show how much time each state was active."""
    print("\n=== State Distribution ===")

    # Group by ship
    by_ship: dict[str, list[dict]] = defaultdict(list)
    for e in entries:
        by_ship[e.get("ship", "?")].append(e)

    for ship, ship_entries in sorted(by_ship.items()):
        print(f"\n  Ship: {ship} ({len(ship_entries)} samples)")

        state_counts = Counter(e.get("state", "?") for e in ship_entries)
        total = sum(state_counts.values())

        for state, count in state_counts.most_common():
            pct = count / total * 100
            bar = "#" * int(pct / 2)
            print(f"    {state:<12} {count:>5} ({pct:5.1f}%) {bar}")


def transition_analysis(entries: list[dict]) -> None:
    """Analyze state transitions — frequency, patterns, and context at transition."""
    print("\n=== Transition Analysis ===")

    transitions = [e for e in entries if "transition" in e]
    if not transitions:
        print("  No transitions found in logs.")
        return

    by_ship: dict[str, list[dict]] = defaultdict(list)
    for e in transitions:
        by_ship[e.get("ship", "?")].append(e)

    for ship, ship_trans in sorted(by_ship.items()):
        print(f"\n  Ship: {ship} ({len(ship_trans)} transitions)")

        # Transition frequency matrix
        matrix: dict[str, Counter] = defaultdict(Counter)
        for e in ship_trans:
            t = e["transition"]
            matrix[t["from"]][t["to"]] += 1

        print("    From -> To:")
        for from_state, targets in sorted(matrix.items()):
            for to_state, count in targets.most_common():
                print(f"      {from_state:<12} -> {to_state:<12} x{count}")

        # Context at transitions
        print("\n    Context at transitions (avg):")
        ctx_sums: dict[str, dict[str, list[float]]] = defaultdict(lambda: defaultdict(list))
        for e in ship_trans:
            to_state = e["transition"]["to"]
            ctx = e.get("ctx", {})
            for k, v in ctx.items():
                if isinstance(v, (int, float)) and v >= 0:
                    ctx_sums[to_state][k].append(v)

        for state, fields in sorted(ctx_sums.items()):
            vals = {k: sum(v) / len(v) for k, v in fields.items() if v}
            relevant = {k: f"{v:.2f}" for k, v in vals.items() if k in ("hp", "shield", "enemyDist", "enemies", "closing")}
            print(f"      -> {state:<12} {relevant}")


def utility_score_summary(entries: list[dict]) -> None:
    """Show average utility scores per state across all samples."""
    print("\n=== Average Utility Scores ===")

    by_ship: dict[str, dict[str, list[float]]] = defaultdict(lambda: defaultdict(list))
    for e in entries:
        ship = e.get("ship", "?")
        scores = e.get("scores", {})
        for state, score in scores.items():
            by_ship[ship][state].append(score)

    for ship, state_scores in sorted(by_ship.items()):
        print(f"\n  Ship: {ship}")
        avgs = {s: sum(v) / len(v) for s, v in state_scores.items() if v}
        for state, avg in sorted(avgs.items(), key=lambda x: -x[1]):
            vals = state_scores[state]
            mn, mx = min(vals), max(vals)
            print(f"    {state:<12}  avg={avg:.3f}  min={mn:.3f}  max={mx:.3f}  n={len(vals)}")


def factor_analysis(entries: list[dict], top_n: int = 5) -> None:
    """Identify which utility factors most influence state selection."""
    print(f"\n=== Factor Analysis (top {top_n} influential per state) ===")

    # Collect factor values per state
    factor_vals: dict[str, dict[str, list[float]]] = defaultdict(lambda: defaultdict(list))
    for e in entries:
        factors = e.get("factors", {})
        for state, state_factors in factors.items():
            for factor_name, value in state_factors.items():
                factor_vals[state][factor_name].append(value)

    for state, factors in sorted(factor_vals.items()):
        print(f"\n  {state}:")
        # Rank by variance (high variance = high influence on decision)
        stats = {}
        for name, vals in factors.items():
            avg = sum(vals) / len(vals)
            variance = sum((v - avg) ** 2 for v in vals) / len(vals) if len(vals) > 1 else 0
            stats[name] = {"avg": avg, "var": variance, "min": min(vals), "max": max(vals)}

        ranked = sorted(stats.items(), key=lambda x: -x[1]["var"])[:top_n]
        for name, s in ranked:
            print(f"    {name:<20}  avg={s['avg']:.3f}  range=[{s['min']:.2f}, {s['max']:.2f}]  var={s['var']:.4f}")


def rapid_switching_detection(entries: list[dict], window_sec: float = 5.0) -> None:
    """Detect periods of rapid state switching (oscillation)."""
    print(f"\n=== Rapid Switching Detection (window={window_sec}s) ===")

    transitions = [e for e in entries if "transition" in e]
    if not transitions:
        print("  No transitions to analyze.")
        return

    by_ship: dict[str, list[dict]] = defaultdict(list)
    for e in transitions:
        by_ship[e.get("ship", "?")].append(e)

    for ship, ship_trans in sorted(by_ship.items()):
        # Sliding window: count transitions within window_sec
        rapid_periods = []
        for i, e in enumerate(ship_trans):
            t = e["t"]
            count = sum(1 for j in range(i, len(ship_trans)) if ship_trans[j]["t"] - t <= window_sec)
            if count >= 4:
                rapid_periods.append((t, count, e["transition"]["from"], e["transition"]["to"]))

        if rapid_periods:
            print(f"\n  Ship: {ship}")
            # Deduplicate overlapping windows
            seen_times = set()
            for t, count, fr, to in rapid_periods:
                bucket = round(t)
                if bucket in seen_times:
                    continue
                seen_times.add(bucket)
                print(f"    t={t:6.1f}s  {count} transitions in {window_sec}s window  (starting {fr}->{to})")
        else:
            print(f"\n  Ship: {ship} - no rapid switching detected")


def combat_engagement_timeline(entries: list[dict]) -> None:
    """Show combat engagement timeline with state choices."""
    print("\n=== Combat Engagement Timeline ===")

    by_ship: dict[str, list[dict]] = defaultdict(list)
    for e in entries:
        by_ship[e.get("ship", "?")].append(e)

    for ship, ship_entries in sorted(by_ship.items()):
        print(f"\n  Ship: {ship}")

        # Find combat segments
        in_combat = False
        combat_start = 0
        segments = []

        for e in ship_entries:
            is_combat = e.get("ctx", {}).get("inCombat", False)
            if is_combat and not in_combat:
                combat_start = e["t"]
                in_combat = True
            elif not is_combat and in_combat:
                segments.append((combat_start, e["t"]))
                in_combat = False

        if in_combat:
            segments.append((combat_start, ship_entries[-1]["t"]))

        if not segments:
            print("    No combat segments found")
            continue

        for start, end in segments[:10]:  # limit output
            duration = end - start
            segment_entries = [e for e in ship_entries if start <= e["t"] <= end]
            states_used = Counter(e["state"] for e in segment_entries)
            trans_count = sum(1 for e in segment_entries if "transition" in e)

            state_str = ", ".join(f"{s}:{c}" for s, c in states_used.most_common(3))
            print(f"    t={start:6.1f}-{end:6.1f}s ({duration:5.1f}s)  transitions={trans_count}  states=[{state_str}]")


def main():
    parser = argparse.ArgumentParser(description="Analyze AI utility decision logs")
    parser.add_argument("path", help="JSONL log file or directory of log files")
    parser.add_argument("--ship", help="Filter to a specific ship name")
    parser.add_argument("--top", type=int, default=5, help="Number of top factors to show")
    parser.add_argument("--transitions", action="store_true", help="Only show transition analysis")
    parser.add_argument("--factors", action="store_true", help="Only show factor analysis")
    parser.add_argument("--switching", action="store_true", help="Only show rapid switching detection")
    parser.add_argument("--timeline", action="store_true", help="Only show combat timeline")
    args = parser.parse_args()

    entries = load_logs(args.path, args.ship)
    if not entries:
        print(f"No log entries found at: {args.path}")
        sys.exit(1)

    ships = set(e.get("ship", "?") for e in entries)
    duration = entries[-1].get("t", 0) - entries[0].get("t", 0) if len(entries) > 1 else 0
    print(f"Loaded {len(entries)} entries across {len(ships)} ship(s), spanning {duration:.1f}s")

    specific = args.transitions or args.factors or args.switching or args.timeline

    if not specific or args.transitions:
        transition_analysis(entries)
    if not specific:
        state_distribution(entries)
        utility_score_summary(entries)
    if not specific or args.factors:
        factor_analysis(entries, args.top)
    if not specific or args.switching:
        rapid_switching_detection(entries)
    if not specific or args.timeline:
        combat_engagement_timeline(entries)


if __name__ == "__main__":
    main()
