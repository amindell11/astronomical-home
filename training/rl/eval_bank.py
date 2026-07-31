"""Banking CLI: promotion-grade statistical evidence for one candidate checkpoint.

Runs the calibration bundle's K_bank replicates of the candidate at the gate shape on the
canonical seeds (the pairing anchor) and, given an incumbent, the same replicates for it
plus the paired A/B reading: exact per-episode McNemar over (replicate, seed, opponent,
episodeIndex) pairs read from the episode JSONLs, and the mean difference over replicate
totals — the instrument that reads a 74-vs-67 single-draw gap as the noise it is.
Interval-reported throughout; banking informs a human and issues no gate verdict.

--draw-held-out is OPT-IN and spends the sealed held-out seed set (1001-1020, resolved by
the C# protocol authority via the "held-out" selector): one draw per banking event,
interval-reported only, never judged against the canonical thresholds. Every draw appends
to the usage registry (heldout_draws.jsonl beside this script) so exposure of the sealed
set stays auditable.

    cd training/rl
    .venv\\Scripts\\python eval_bank.py --candidate <ckpt.onnx> --incumbent <banked.onnx>
"""
import argparse
import json
import sys
import time
from pathlib import Path

from checkpoint_watch import discover_reps, rep_dir
from driver_common import default_unity_exe
from eval_bundle import BUNDLE_V1, load_bundle
from eval_gate import read_opponents
from eval_lane import HARNESS_CHILD, PROJECT, run_eval_lane, summaries_in
from eval_stats import mcnemar_exact_p, mean_difference, wilson_interval

RL_DIR = Path(__file__).resolve().parent
# Staging discipline: banking artifacts land in the primary tree, never slot-only.
BANK_ROOT = Path(r"D:\amind\git\astronomical-home") / "results" / "rl-eval" / "bank"
REGISTRY = RL_DIR / "heldout_draws.jsonl"
HELD_OUT_SELECTOR = "held-out"


def rep_wins(summary_path: Path) -> dict:
    """opponent label -> (wins, episodes) for one replicate, in summary order."""
    return {o["opponent"]: (int(o["wins"]), int(o["episodes"])) for o in read_opponents(summary_path)}


def side_stats(summaries) -> dict:
    """Pooled per-opponent cells with Wilson intervals plus the replicate totals for one side.
    Per-opponent only plus the gate total — deliberately no blended win rate."""
    order = []
    pooled = {}
    totals = []
    for path in summaries:
        wins = rep_wins(path)
        totals.append(sum(w for w, _ in wins.values()))
        for label, (w, n) in wins.items():
            if label not in pooled:
                pooled[label] = [0, 0]
                order.append(label)
            pooled[label][0] += w
            pooled[label][1] += n
    opponents = [{"opponent": label, "wins": pooled[label][0], "episodes": pooled[label][1],
                  "wilson95": list(wilson_interval(pooled[label][0], pooled[label][1]))}
                 for label in order]
    total_wins = sum(block[0] for block in pooled.values())
    total_episodes = sum(block[1] for block in pooled.values())
    return {"replicateTotals": totals,
            "opponents": opponents,
            "totalWins": total_wins,
            "totalEpisodes": total_episodes,
            "totalWilson95": list(wilson_interval(total_wins, total_episodes))}


def episode_outcomes(summary_path: Path) -> dict:
    """(seed, opponent, episodeIndex) -> won, from the episode JSONL the summary names.
    The JSONL is resolved beside the summary — the producer writes both into one session
    dir, so the pairing survives artifacts being staged elsewhere."""
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    jsonl = summary_path.parent / Path(summary["episodesJsonl"]).name
    if not jsonl.exists():
        sys.exit(f"FAIL: episode JSONL {jsonl} missing beside {summary_path}; paired A/B has no episodes to pair")
    outcomes = {}
    for line in jsonl.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        row = json.loads(line)
        key = (row["spec"]["runSeed"], row["opponent"]["archetype"], row["episodeIndex"])
        if key in outcomes:
            sys.exit(f"FAIL: duplicate episode identity {key} in {jsonl}; pairing has no unique key")
        outcomes[key] = row["outcome"] == "Win"
    return outcomes


def paired_discordance(candidate_summaries, incumbent_summaries) -> tuple:
    """(pairs, candidate-only wins, incumbent-only wins) over (replicate, seed, opponent,
    episodeIndex) pairs — the McNemar discordant counts."""
    if len(candidate_summaries) != len(incumbent_summaries):
        sys.exit(f"FAIL: paired A/B needs equal replicate counts; "
                 f"got {len(candidate_summaries)} vs {len(incumbent_summaries)}")
    pairs = candidate_only = incumbent_only = 0
    for rep, (a_path, b_path) in enumerate(zip(candidate_summaries, incumbent_summaries), start=1):
        a, b = episode_outcomes(a_path), episode_outcomes(b_path)
        if a.keys() != b.keys():
            sys.exit(f"FAIL: replicate {rep} episode identities differ between sides "
                     f"({a_path} vs {b_path}); paired A/B needs identical seeds, opponents, and episode counts")
        for key, a_won in a.items():
            pairs += 1
            b_won = b[key]
            if a_won and not b_won:
                candidate_only += 1
            elif b_won and not a_won:
                incumbent_only += 1
    return pairs, candidate_only, incumbent_only


def run_side(name: str, onnx: Path, side_dir: Path, bundle, launch) -> list:
    """K_bank replicate summaries for one side, ascending; finished replicates on disk
    replay instead of re-running (the gate's replay rule, so a crashed bank resumes)."""
    present = dict(discover_reps(side_dir))
    for k in sorted(present):
        print(f"[bank] {name}: replaying replicate {k}")
    for k in range(1, bundle.k_bank + 1):
        if k not in present:
            print(f"[bank] {name}: replicate {k}/{bundle.k_bank}")
            present[k] = launch(rep_dir(side_dir, k), onnx, None)
    return [present[k] for k in sorted(present)]


def record_draw(registry: Path, entry: dict) -> None:
    with registry.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry) + "\n")


def draw_held_out(candidate: Path, out_dir: Path, bundle, launch) -> dict:
    """One sealed-set draw, interval-reported ONLY — the held-out seeds answer
    generalization and are never judged against the canonical thresholds. Exposure is
    logged write-ahead, BEFORE the draw runs, so a crash or a rejected summary can never
    leave the sealed set spent but unrecorded; a draw already summarized on disk replays
    without a new registry entry (its exposure was logged when it launched)."""
    existing = summaries_in(out_dir) if out_dir.exists() else []
    if len(existing) > 1:
        sys.exit(f"FAIL: {out_dir} holds {len(existing)} summaries; one run dir is one sample")
    if existing:
        print(f"[bank] held-out: replaying {existing[0]}")
        summary_path = existing[0]
    else:
        record_draw(REGISTRY, {"timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"),
                               "candidate": str(candidate),
                               "bundleId": bundle.bundle_id,
                               "episodesPerSeed": bundle.episodes_per_seed,
                               "outDir": str(out_dir)})
        summary_path = launch(out_dir, candidate, HELD_OUT_SELECTOR)
    opponents = [{"opponent": o["opponent"], "wins": int(o["wins"]), "episodes": int(o["episodes"]),
                  "wilson95": list(wilson_interval(int(o["wins"]), int(o["episodes"])))}
                 for o in read_opponents(summary_path)]
    return {"summary": str(summary_path), "opponents": opponents, "registry": str(REGISTRY)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--candidate", type=Path, required=True, help="checkpoint .onnx to bank")
    parser.add_argument("--incumbent", type=Path, default=None,
                        help="banked checkpoint .onnx to compare against (paired A/B)")
    parser.add_argument("--draw-held-out", action="store_true",
                        help="OPT-IN: spend one draw of the sealed held-out seed set (appends to the usage registry)")
    parser.add_argument("--bundle", type=Path, default=BUNDLE_V1,
                        help="calibration bundle (seeds, episodes/seed, K_bank)")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the evals boot in (point at a free pool slot, not the tree you work in)")
    parser.add_argument("--unity", type=Path, default=None,
                        help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--out-root", type=Path, default=BANK_ROOT,
                        help="artifact root; each banking event gets its own dir")
    parser.add_argument("--event-dir", type=Path, default=None,
                        help="resume an existing banking event dir (replicates already summarized replay)")
    parser.add_argument("--lease", default="rl-eval-bank", help="unity-access lease name")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane per eval")
    args = parser.parse_args()
    if not args.candidate.exists():
        parser.error(f"--candidate {args.candidate} does not exist")
    if args.incumbent is not None and not args.incumbent.exists():
        parser.error(f"--incumbent {args.incumbent} does not exist")
    if not args.project.exists():
        parser.error(f"--project {args.project} does not exist")
    if not HARNESS_CHILD.exists():
        sys.exit(f"FAIL: batch child missing at {HARNESS_CHILD}")

    bundle = load_bundle(args.bundle)
    canonical_seeds = ",".join(str(s) for s in bundle.seeds)
    unity = args.unity or default_unity_exe(args.project)
    tag = f"{args.candidate.stem}-vs-{args.incumbent.stem}" if args.incumbent else args.candidate.stem
    out = args.event_dir or args.out_root / f"{tag}-{time.strftime('%Y%m%d-%H%M%S')}"
    print(f"[bank] bundle {bundle.bundle_id}  project {args.project}  artifacts {out}")

    def launch(out_dir, onnx, seeds):
        return run_eval_lane(project=args.project, unity=unity, lease=args.lease, out_dir=out_dir,
                             onnx=onnx, seeds=seeds or canonical_seeds,
                             episodes_per_seed=bundle.episodes_per_seed, lease_wait=args.lease_wait)

    candidate = run_side("candidate", args.candidate, out / "candidate", bundle, launch)
    result = {
        "bundleId": bundle.bundle_id,
        "candidate": str(args.candidate),
        "incumbent": str(args.incumbent) if args.incumbent else None,
        "seeds": canonical_seeds,
        "episodesPerSeed": bundle.episodes_per_seed,
        "kBank": bundle.k_bank,
        "candidateEvidence": [str(p) for p in candidate],
        "candidateStats": side_stats(candidate),
    }
    if args.incumbent:
        incumbent = run_side("incumbent", args.incumbent, out / "incumbent", bundle, launch)
        result["incumbentEvidence"] = [str(p) for p in incumbent]
        result["incumbentStats"] = side_stats(incumbent)
        pairs, candidate_only, incumbent_only = paired_discordance(candidate, incumbent)
        diff = mean_difference(result["candidateStats"]["replicateTotals"],
                               result["incumbentStats"]["replicateTotals"])
        result["paired"] = {
            "pairs": pairs,
            "candidateOnlyWins": candidate_only,
            "incumbentOnlyWins": incumbent_only,
            "mcnemarP": mcnemar_exact_p(candidate_only, incumbent_only),
            "candidateMeanTotal": diff.mean_a,
            "incumbentMeanTotal": diff.mean_b,
            "meanTotalDiff": diff.diff,
            "meanTotalDiffSE": diff.se,
        }
    if args.draw_held_out:
        result["heldOut"] = draw_held_out(args.candidate, out / "held-out" / "rep-1", bundle, launch)

    bank_path = out / "bank.json"
    bank_path.write_text(json.dumps(result, indent=2))
    for block in result["candidateStats"]["opponents"]:
        lb, ub = block["wilson95"]
        print(f"[bank] candidate vs {block['opponent']}: {block['wins']}/{block['episodes']} "
              f"Wilson95 [{lb:.2f}, {ub:.2f}]")
    if args.incumbent:
        paired = result["paired"]
        print(f"[bank] paired A/B over {paired['pairs']} episodes: candidate-only wins "
              f"{paired['candidateOnlyWins']} vs incumbent-only {paired['incumbentOnlyWins']} "
              f"(exact McNemar p={paired['mcnemarP']:.3f}); mean totals "
              f"{paired['candidateMeanTotal']:.1f} vs {paired['incumbentMeanTotal']:.1f} "
              f"(diff {paired['meanTotalDiff']:+.1f} +/- {paired['meanTotalDiffSE']:.1f} SE)")
    if args.draw_held_out:
        print(f"[bank] held-out draw recorded in {REGISTRY}")
    print(f"[bank] {bank_path}")


if __name__ == "__main__":
    main()
