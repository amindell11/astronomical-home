"""Automated scripted-eval gate: makes pursuit erosion visible DURING a run instead of after it.

Watches a training run's checkpoint exports (results/rl-training/<run-id>/ShipCombat/
ShipCombat-<step>.onnx) and, per new checkpoint, runs the standard 75-episode scripted eval
at the gate shape (5 archetypes x 5 seeds x 3 episodes) through the unity-access coordinator
on a pool slot, then applies the two-consecutive-checkpoints rule:

    ALERT  one checkpoint with Evader <= 10/15 or total < 55/75
    STOP   two CONSECUTIVE such checkpoints (~30 Evader episodes carry the decision)

Baseline for context: seed-class policies score Evader 12-14/15, total ~70-74; the eval noise
floor is +/-4/75, which is why one bad checkpoint is never a stop.

The gate REPORTS; it does not kill the trainer. Terminating a multi-hour run is irreversible and
the underlying signal is noisy, so auto-stop is opt-in: pass --auto-stop-pid <trainer pid>. On a
STOP verdict the gate writes its artifact and exits 2 either way (further evals would only burn
pool slots on a policy already judged).

Each eval gets its own gate-named output dir; RL_HARNESS_OUT_DIR carries that name into Unity
(EpisodeJsonl.NewRunPath dirOverride), so the gate reads back exactly what it named rather than
reconstructing where CheckpointEvaluator wrote. The lane launcher (eval_lane) owns the launch;
the checkpoint watch (checkpoint_watch) owns discovery and replay; this file owns the verdict.

    cd training/rl
    .venv\\Scripts\\python eval_gate.py --run-id ship_combat_hybrid --project ../../../agent-2/src/Asteroids3D
    .venv\\Scripts\\python eval_gate.py --run-id ship_combat_hybrid --once   # drain the backlog and exit
"""
import argparse
import json
import sys
from pathlib import Path
from typing import NamedTuple

from checkpoint_watch import watch_checkpoints
from driver_common import default_unity_exe
from eval_lane import HARNESS_CHILD, run_eval_lane
from run_parallel import terminate_pid_tree

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
RESULTS = REPO_ROOT / "results" / "rl-training"
GATE_ROOT = REPO_ROOT / "results" / "rl-eval" / "gate"
PROJECT = REPO_ROOT / "src" / "Asteroids3D"

EVADER = "Evader"
EVADER_EPISODES = 15
TOTAL_EPISODES = 75
ALERT_EVADER_WINS = 10
MIN_TOTAL_WINS = 55

CONTINUE = "CONTINUE"
ALERT = "ALERT"
STOP = "STOP"


class Score(NamedTuple):
    step: int
    evader_wins: int
    total_wins: int


def degraded_reasons(score: Score) -> list:
    """The degradation predicate, spelled out so the report names what tripped."""
    reasons = []
    if score.evader_wins <= ALERT_EVADER_WINS:
        reasons.append(f"Evader {score.evader_wins}/{EVADER_EPISODES} <= {ALERT_EVADER_WINS}")
    if score.total_wins < MIN_TOTAL_WINS:
        reasons.append(f"total {score.total_wins}/{TOTAL_EPISODES} < {MIN_TOTAL_WINS}")
    return reasons


def verdict(scores) -> str:
    """CONTINUE / ALERT / STOP over the ordered per-checkpoint scores; a healthy checkpoint resets the streak."""
    if not scores or not degraded_reasons(scores[-1]):
        return CONTINUE
    if len(scores) >= 2 and degraded_reasons(scores[-2]):
        return STOP
    return ALERT


SUMMARY_SCHEMA = "rl-eval-summary-v2"


def read_score(summary_path: Path, step: int) -> Score:
    summary = json.loads(summary_path.read_text())
    schema = summary.get("schema")
    if schema != SUMMARY_SCHEMA:
        # A replayed pre-v2 step dir must die here, not be misread as zero wins.
        sys.exit(f"FAIL: {summary_path} has schema {schema!r}, expected {SUMMARY_SCHEMA!r}; "
                 f"a pre-v2 step dir cannot be replayed — restart the gate into a fresh --out-root")
    wins = {o["opponent"]: int(o["wins"]) for o in summary["opponents"]}
    if EVADER not in wins:
        sys.exit(f"FAIL: no {EVADER} block in {summary_path}; the gate rule has nothing to read")
    return Score(step=step, evader_wins=wins[EVADER], total_wins=sum(wins.values()))


def run_eval(args, unity: Path, checkpoint: Path, out_dir: Path) -> Path:
    return run_eval_lane(project=args.project, unity=unity, lease=args.lease, out_dir=out_dir,
                         onnx=checkpoint, seeds=args.seeds,
                         episodes_per_seed=args.episodes_per_seed, lease_wait=args.lease_wait)


def report(score: Score, checkpoint: Path, current: str, reasons) -> None:
    print(f"[gate] {checkpoint.name}: Evader {score.evader_wins}/{EVADER_EPISODES}  "
          f"total {score.total_wins}/{TOTAL_EPISODES}  -> {current}")
    if current != CONTINUE:
        banner = "!" * 72
        print(banner)
        print(f"[gate] {current} at step {score.step}: " + "; ".join(reasons))
        if current == STOP:
            print("[gate] two consecutive degraded checkpoints — pursuit is eroding, not noise")
        print(banner)
    sys.stdout.flush()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--run-id", required=True, help="mlagents run id under results/rl-training")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the eval boots in (point at a free pool slot, not the tree you work in)")
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--results-dir", type=Path, default=RESULTS, help="mlagents results root")
    parser.add_argument("--out-root", type=Path, default=GATE_ROOT, help="gate-owned artifact root")
    parser.add_argument("--seeds", default="2001,2002,2003,2004,2005", help="RL_HARNESS_SEEDS selection")
    parser.add_argument("--episodes-per-seed", type=int, default=3, help="RL_HARNESS_EPISODES_PER_SEED")
    parser.add_argument("--poll-seconds", type=float, default=120.0, help="checkpoint-dir poll interval")
    parser.add_argument("--from-step", type=int, default=0, help="ignore checkpoints at or below this step")
    parser.add_argument("--max-checkpoints", type=int, default=0, help="stop after N evals (0 = unbounded)")
    parser.add_argument("--once", action="store_true", help="evaluate the checkpoints present now, then exit")
    # Per-run by default: the coordinator keys ownership on the lease, so two gates sharing one
    # lease name would renew each other's ownership instead of queueing.
    parser.add_argument("--lease", default=None, help="unity-access lease name (default: rl-eval-gate-<run-id>)")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane per eval")
    parser.add_argument("--auto-stop-pid", type=int, default=None, metavar="PID",
                        help="OPT-IN: kill this trainer process tree on a STOP verdict (default: report only)")
    args = parser.parse_args()
    if args.episodes_per_seed < 1:
        parser.error("--episodes-per-seed must be >= 1")
    seed_count = len([s for s in args.seeds.split(",") if s.strip()])
    if seed_count * args.episodes_per_seed != EVADER_EPISODES:
        parser.error(f"--seeds x --episodes-per-seed must be {EVADER_EPISODES} episodes per archetype "
                     f"({TOTAL_EPISODES} total) for the gate thresholds to mean anything; "
                     f"got {seed_count} x {args.episodes_per_seed}")
    if args.poll_seconds <= 0:
        parser.error("--poll-seconds must be > 0")
    if args.max_checkpoints < 0:
        parser.error("--max-checkpoints must be >= 0")
    if args.auto_stop_pid is not None and args.auto_stop_pid <= 0:
        parser.error("--auto-stop-pid must be a live trainer pid")
    if not args.project.exists():
        parser.error(f"--project {args.project} does not exist")
    if not HARNESS_CHILD.exists():
        sys.exit(f"FAIL: batch child missing at {HARNESS_CHILD}")

    args.lease = args.lease or f"rl-eval-gate-{args.run_id}"
    unity = args.unity or default_unity_exe(args.project)
    behavior_dir = args.results_dir / args.run_id / "ShipCombat"
    gate_dir = args.out_root / args.run_id
    print(f"[gate] watching {behavior_dir}")
    print(f"[gate] eval project {args.project}  seeds {args.seeds} x {args.episodes_per_seed}  artifacts {gate_dir}")

    scores = []
    for pending in watch_checkpoints(behavior_dir, gate_dir, from_step=args.from_step,
                                     poll_seconds=args.poll_seconds, once=args.once):
        if pending.replay:
            print(f"[gate] step {pending.step}: replaying {pending.replay.name}")
        summary_path = pending.replay or run_eval(args, unity, pending.checkpoint, pending.out_dir)
        score = read_score(summary_path, pending.step)
        scores.append(score)
        current = verdict(scores)
        reasons = degraded_reasons(score)
        (pending.out_dir / "verdict.json").write_text(json.dumps({
            "runId": args.run_id,
            "step": pending.step,
            "checkpoint": str(pending.checkpoint),
            "summary": str(summary_path),
            "evaderWins": score.evader_wins,
            "evaderEpisodes": EVADER_EPISODES,
            "totalWins": score.total_wins,
            "totalEpisodes": TOTAL_EPISODES,
            "verdict": current,
            "reasons": reasons,
            "history": [s._asdict() for s in scores],
        }, indent=2))
        report(score, pending.checkpoint, current, reasons)
        if current == STOP:
            if args.auto_stop_pid:
                print(f"[gate] --auto-stop-pid {args.auto_stop_pid}: killing the trainer tree")
                terminate_pid_tree(args.auto_stop_pid)
            sys.exit(2)
        if args.max_checkpoints and len(scores) >= args.max_checkpoints:
            print(f"[gate] reached --max-checkpoints {args.max_checkpoints}")
            return
    print(f"[gate] --once: evaluated {len(scores)} checkpoint(s)")


if __name__ == "__main__":
    main()
