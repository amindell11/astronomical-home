#!/usr/bin/env python3
"""
AI Decision Pattern Finder
===========================
Deeper pattern analysis on utility logs: finds decision boundaries,
factor correlations, and anomalous behavior.

Usage:
    python find_patterns.py <log_file_or_directory> [options]

Examples:
    python find_patterns.py ./AILogs/ --boundaries
    python find_patterns.py session.jsonl --anomalies --threshold 0.1
    python find_patterns.py ./AILogs/ --compare Ship_A1 Ship_B1
"""

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path


def load_logs(path: str, ship_filter: str = None) -> list[dict]:
    p = Path(path)
    files = list(p.glob("*.jsonl")) if p.is_dir() else [p]
    entries = []
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


def decision_boundaries(entries: list[dict]) -> None:
    """Find the context conditions at which state transitions happen.
    Groups transitions by (from, to) pair and shows the avg context."""
    print("\n=== Decision Boundaries ===")
    print("  Context values at the moment of each transition type:\n")

    transitions = [e for e in entries if "transition" in e]
    if not transitions:
        print("  No transitions found.")
        return

    # Group by transition pair
    pairs: dict[str, list[dict]] = defaultdict(list)
    for e in transitions:
        key = f"{e['transition']['from']} -> {e['transition']['to']}"
        pairs[key].append(e)

    ctx_fields = ["hp", "shield", "enemyDist", "enemyHp", "los", "enemies", "closing", "angle"]

    for pair, pair_entries in sorted(pairs.items(), key=lambda x: -len(x[1])):
        print(f"  {pair} (n={len(pair_entries)})")

        for field in ctx_fields:
            vals = [e.get("ctx", {}).get(field) for e in pair_entries]
            vals = [v for v in vals if v is not None and (isinstance(v, (int, float)) and v >= 0 or isinstance(v, bool))]
            if not vals:
                continue
            if isinstance(vals[0], bool):
                true_pct = sum(1 for v in vals if v) / len(vals) * 100
                print(f"    {field:<14} true={true_pct:.0f}%")
            else:
                avg = sum(vals) / len(vals)
                mn, mx = min(vals), max(vals)
                print(f"    {field:<14} avg={avg:.2f}  range=[{mn:.2f}, {mx:.2f}]")
        print()


def score_gaps(entries: list[dict]) -> None:
    """Find situations where the winning state barely won (close decisions)
    and situations where it dominated (obvious decisions)."""
    print("\n=== Score Gap Analysis ===")
    print("  How decisive are state selections?\n")

    close_decisions = []
    dominant_decisions = []

    for e in entries:
        scores = e.get("scores", {})
        if len(scores) < 2:
            continue

        sorted_scores = sorted(scores.values(), reverse=True)
        gap = sorted_scores[0] - sorted_scores[1]
        winner = max(scores, key=scores.get)

        record = {"t": e["t"], "ship": e.get("ship", "?"), "state": winner, "gap": gap, "scores": scores}

        if gap < 0.05:
            close_decisions.append(record)
        elif gap > 0.5:
            dominant_decisions.append(record)

    print(f"  Close decisions (gap < 0.05): {len(close_decisions)}")
    if close_decisions:
        # Show most common close-call pairs
        pairs = defaultdict(int)
        for d in close_decisions:
            top2 = sorted(d["scores"].items(), key=lambda x: -x[1])[:2]
            pair = f"{top2[0][0]} vs {top2[1][0]}"
            pairs[pair] += 1
        for pair, count in sorted(pairs.items(), key=lambda x: -x[1])[:5]:
            print(f"    {pair}: {count} close calls")

    print(f"\n  Dominant decisions (gap > 0.5): {len(dominant_decisions)}")
    if dominant_decisions:
        winners = defaultdict(int)
        for d in dominant_decisions:
            winners[d["state"]] += 1
        for state, count in sorted(winners.items(), key=lambda x: -x[1]):
            print(f"    {state}: dominates {count} times")


def state_duration_stats(entries: list[dict]) -> None:
    """How long does each state last on average before switching?"""
    print("\n=== State Duration Stats ===")

    by_ship: dict[str, list[dict]] = defaultdict(list)
    for e in entries:
        by_ship[e.get("ship", "?")].append(e)

    for ship, ship_entries in sorted(by_ship.items()):
        print(f"\n  Ship: {ship}")

        transitions = [e for e in ship_entries if "transition" in e]
        if len(transitions) < 2:
            print("    Not enough transitions for duration analysis")
            continue

        durations: dict[str, list[float]] = defaultdict(list)
        for i in range(1, len(transitions)):
            prev = transitions[i - 1]
            curr = transitions[i]
            duration = curr["t"] - prev["t"]
            state = prev["transition"]["to"]
            durations[state].append(duration)

        for state, durs in sorted(durations.items()):
            if not durs:
                continue
            avg = sum(durs) / len(durs)
            mn, mx = min(durs), max(durs)
            median = sorted(durs)[len(durs) // 2]
            print(f"    {state:<12}  avg={avg:.2f}s  med={median:.2f}s  range=[{mn:.2f}, {mx:.2f}]s  n={len(durs)}")


def compare_ships(entries: list[dict], ship_a: str, ship_b: str) -> None:
    """Side-by-side comparison of two ships' decision patterns."""
    print(f"\n=== Ship Comparison: {ship_a} vs {ship_b} ===")

    a_entries = [e for e in entries if e.get("ship") == ship_a]
    b_entries = [e for e in entries if e.get("ship") == ship_b]

    if not a_entries or not b_entries:
        print(f"  Need entries for both ships. Found: {ship_a}={len(a_entries)}, {ship_b}={len(b_entries)}")
        return

    def ship_stats(ship_entries):
        from collections import Counter
        state_counts = Counter(e.get("state") for e in ship_entries)
        total = sum(state_counts.values())
        state_pcts = {s: c / total * 100 for s, c in state_counts.items()}

        avg_scores = defaultdict(list)
        for e in ship_entries:
            for s, v in e.get("scores", {}).items():
                avg_scores[s].append(v)
        avg_scores = {s: sum(v) / len(v) for s, v in avg_scores.items()}

        trans_count = sum(1 for e in ship_entries if "transition" in e)
        duration = ship_entries[-1]["t"] - ship_entries[0]["t"] if len(ship_entries) > 1 else 0
        trans_rate = trans_count / duration * 60 if duration > 0 else 0

        return state_pcts, avg_scores, trans_rate

    a_pcts, a_scores, a_rate = ship_stats(a_entries)
    b_pcts, b_scores, b_rate = ship_stats(b_entries)

    all_states = sorted(set(list(a_pcts.keys()) + list(b_pcts.keys())))

    print(f"\n  {'State':<12} {'%':>8} {'%':>8}    {'AvgUtil':>8} {'AvgUtil':>8}")
    print(f"  {'':12} {ship_a:>8} {ship_b:>8}    {ship_a:>8} {ship_b:>8}")
    print(f"  {'-'*60}")

    for state in all_states:
        ap = a_pcts.get(state, 0)
        bp = b_pcts.get(state, 0)
        au = a_scores.get(state, 0)
        bu = b_scores.get(state, 0)
        print(f"  {state:<12} {ap:7.1f}% {bp:7.1f}%    {au:8.3f} {bu:8.3f}")

    print(f"\n  Transitions/min:  {ship_a}={a_rate:.1f}  {ship_b}={b_rate:.1f}")


def factor_at_state(entries: list[dict], target_state: str) -> None:
    """Show detailed factor values when a specific state is active."""
    print(f"\n=== Factor Deep-Dive: {target_state} state ===")

    state_entries = [e for e in entries if e.get("state") == target_state and "factors" in e]
    if not state_entries:
        print(f"  No entries found for state: {target_state}")
        return

    factor_vals: dict[str, list[float]] = defaultdict(list)
    for e in state_entries:
        factors = e.get("factors", {}).get(target_state, {})
        for name, val in factors.items():
            factor_vals[name].append(val)

    print(f"  {len(state_entries)} samples while in {target_state}\n")
    for name, vals in sorted(factor_vals.items()):
        avg = sum(vals) / len(vals)
        mn, mx = min(vals), max(vals)
        # Flag factors that deviate far from 1.0 (neutral)
        flag = " <<< SUPPRESSING" if avg < 0.5 else " <<< BOOSTING" if avg > 1.3 else ""
        print(f"    {name:<20}  avg={avg:.3f}  range=[{mn:.2f}, {mx:.2f}]{flag}")


def main():
    parser = argparse.ArgumentParser(description="Find patterns in AI utility logs")
    parser.add_argument("path", help="JSONL log file or directory")
    parser.add_argument("--ship", help="Filter to a specific ship")
    parser.add_argument("--boundaries", action="store_true", help="Show decision boundaries")
    parser.add_argument("--gaps", action="store_true", help="Show score gap analysis")
    parser.add_argument("--durations", action="store_true", help="Show state duration stats")
    parser.add_argument("--compare", nargs=2, metavar=("SHIP_A", "SHIP_B"), help="Compare two ships")
    parser.add_argument("--state", help="Deep-dive on a specific state's factors")
    args = parser.parse_args()

    entries = load_logs(args.path, args.ship)
    if not entries:
        print(f"No log entries found at: {args.path}")
        sys.exit(1)

    ships = set(e.get("ship", "?") for e in entries)
    duration = entries[-1].get("t", 0) - entries[0].get("t", 0) if len(entries) > 1 else 0
    print(f"Loaded {len(entries)} entries across {len(ships)} ship(s), spanning {duration:.1f}s")

    specific = args.boundaries or args.gaps or args.durations or args.compare or args.state

    if not specific or args.boundaries:
        decision_boundaries(entries)
    if not specific or args.gaps:
        score_gaps(entries)
    if not specific or args.durations:
        state_duration_stats(entries)
    if args.compare:
        compare_ships(entries, args.compare[0], args.compare[1])
    if args.state:
        factor_at_state(entries, args.state)


if __name__ == "__main__":
    main()
