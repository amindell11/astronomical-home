"""Automated scripted-eval gate: makes pursuit erosion visible DURING a run instead of after it.

Watches a training run's checkpoint exports (results/rl-training/<run-id>/ShipCombat/
ShipCombat-<step>.onnx) and, per new checkpoint, runs the standard scripted eval at the
gate shape through the unity-access coordinator on a pool slot. Verdicts follow the
calibration bundle (eval_bundle_v1.json — seed set, thresholds, replicate counts,
execution mode; the bundle id is recorded in every verdict):

    watch    one replicate per checkpoint (K_watch = 1)
    confirm  while ARMED, a degraded read (Evader cell at or under the alert threshold,
             or total under the minimum) triggers confirmation replicates of the SAME
             checkpoint — fresh boots, up to K_watch + K_confirm total — and the verdict
             is re-derived from the pooled replicate cells; confirmation stops early if
             the pooled read turns healthy
    ALERT    a CONFIRMED degraded checkpoint (pooled cells, never one draw)
    STOP     two consecutive confirmed checkpoints; a clear checkpoint resets the streak

Arming: the gate is report-only until a checkpoint first reads healthy, then it arms
(armed state + arming step recorded in verdict.json). A fresh run's untrained early
checkpoints can no longer produce a false STOP; --from-step stays as the manual window.

Replicates land in step-<N>/rep-<k>/ dirs (one run dir = one sample). On restart the
verdict is re-derived from every replicate present; missing confirmation replicates run
only while the pooled state is degraded-unconfirmed — replay and fresh-run follow the
same rule. Pooled Wilson intervals are recomputed here and quoted with every degraded
reason (episodes cluster within seeds; the thresholds are calibrated on that same
clustered structure, which absorbs the interval's mild understatement).

Resume convention: after a trainer --resume, restart the gate with --from-step set to
the last step already judged; pre-resume checkpoints keep their verdicts and are never
re-judged. This is a documented convention, deliberately not a mechanism.

The gate REPORTS; it does not kill the trainer. Terminating a multi-hour run is irreversible,
so auto-stop is opt-in: pass --auto-stop-pid <trainer pid>. On a STOP verdict the gate writes
its artifact and exits 2 either way (further evals would only burn pool slots on a policy
already judged).

Each replicate gets its own gate-named output dir; RL_HARNESS_OUT_DIR carries that name into
Unity (EpisodeJsonl.NewRunPath dirOverride), so the gate reads back exactly what it named.
The lane launcher (eval_lane) owns the launch; the checkpoint watch (checkpoint_watch) owns
discovery and replay; this file owns the verdict.

    cd training/rl
    .venv\\Scripts\\python eval_gate.py --run-id ship_combat_hybrid --project ../../../agent-2/src/Asteroids3D
    .venv\\Scripts\\python eval_gate.py --run-id ship_combat_hybrid --once   # drain the backlog and exit
"""
import argparse
import json
import sys
from pathlib import Path
from typing import NamedTuple

from checkpoint_watch import rep_dir, watch_checkpoints
from driver_common import default_unity_exe
from eval_bundle import BUNDLE_V1, load_bundle
from eval_lane import HARNESS_CHILD, run_eval_lane
from eval_stats import wilson_interval
from run_parallel import terminate_pid_tree

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
RESULTS = REPO_ROOT / "results" / "rl-training"
GATE_ROOT = REPO_ROOT / "results" / "rl-eval" / "gate"
PROJECT = REPO_ROOT / "src" / "Asteroids3D"

EVADER = "Evader"

CONTINUE = "CONTINUE"
ALERT = "ALERT"
STOP = "STOP"


class Score(NamedTuple):
    """Cell counts for one sample — a single replicate or a replicate pool."""
    step: int
    evader_wins: int
    evader_episodes: int
    total_wins: int
    total_episodes: int


class CheckpointRead(NamedTuple):
    """One checkpoint's judged evidence: the pooled score over its replicates."""
    step: int
    score: Score
    reps: int
    degraded: bool
    confirmed: bool


def degraded_reasons(score, thresholds) -> list:
    """The degradation predicate at replicate-pooled rates: the bundle's count thresholds
    generalize to win-rate comparisons (integer cross-products, no float equality), and each
    tripped reason quotes its pooled Wilson interval so the report carries its uncertainty."""
    reasons = []
    if score.evader_wins * thresholds.evader_episodes <= thresholds.alert_evader_wins * score.evader_episodes:
        lb, ub = wilson_interval(score.evader_wins, score.evader_episodes)
        reasons.append(f"Evader {score.evader_wins}/{score.evader_episodes} "
                       f"(Wilson95 [{lb:.2f}, {ub:.2f}]) <= {thresholds.alert_evader_wins}/{thresholds.evader_episodes}")
    if score.total_wins * thresholds.total_episodes < thresholds.min_total_wins * score.total_episodes:
        lb, ub = wilson_interval(score.total_wins, score.total_episodes)
        reasons.append(f"total {score.total_wins}/{score.total_episodes} "
                       f"(Wilson95 [{lb:.2f}, {ub:.2f}]) < {thresholds.min_total_wins}/{thresholds.total_episodes}")
    return reasons


def verdict(reads) -> str:
    """CONTINUE / ALERT / STOP over the ordered per-checkpoint reads; only CONFIRMED
    degradation counts, and a clear checkpoint resets the streak."""
    if not reads or not reads[-1].confirmed:
        return CONTINUE
    if len(reads) >= 2 and reads[-2].confirmed:
        return STOP
    return ALERT


SUMMARY_SCHEMA = "rl-eval-summary-v2"


def read_opponents(summary_path: Path) -> list:
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    schema = summary.get("schema")
    if schema != SUMMARY_SCHEMA:
        # A replayed pre-v2 step dir must die here, not be misread as zero wins.
        sys.exit(f"FAIL: {summary_path} has schema {schema!r}, expected {SUMMARY_SCHEMA!r}; "
                 f"a pre-v2 step dir cannot be replayed — restart the gate into a fresh --out-root")
    return summary["opponents"]


def read_score(summary_path: Path, step: int) -> Score:
    blocks = read_opponents(summary_path)
    by_name = {o["opponent"]: o for o in blocks}
    if EVADER not in by_name:
        sys.exit(f"FAIL: no {EVADER} block in {summary_path}; the gate rule has nothing to read")
    return Score(step=step,
                 evader_wins=int(by_name[EVADER]["wins"]),
                 evader_episodes=int(by_name[EVADER]["episodes"]),
                 total_wins=sum(int(o["wins"]) for o in blocks),
                 total_episodes=sum(int(o["episodes"]) for o in blocks))


def pool_scores(step: int, scores) -> Score:
    """One pooled sample per checkpoint: replicate cells sum (cross-boot replicates ARE the
    jitter being measured, so pooling never weights or drops a boot)."""
    scores = list(scores)
    return Score(step=step,
                 evader_wins=sum(s.evader_wins for s in scores),
                 evader_episodes=sum(s.evader_episodes for s in scores),
                 total_wins=sum(s.total_wins for s in scores),
                 total_episodes=sum(s.total_episodes for s in scores))


def judge_checkpoint(pending, bundle, armed, run_rep):
    """The adaptive-confirmation protocol for one checkpoint. run_rep(rep_dir, k) runs one
    replicate in a fresh boot and returns its summary path; replicates already on disk
    replay. Confirmation replicates run only while armed and the pooled read stays
    degraded-unconfirmed — the replay rule and the fresh-run rule are the same loop."""
    reps = dict(pending.reps)
    scores = {k: read_score(path, pending.step) for k, path in reps.items()}
    full = bundle.k_watch + bundle.k_confirm

    def run(k):
        reps[k] = run_rep(rep_dir(pending.out_dir, k), k)
        scores[k] = read_score(reps[k], pending.step)

    for k in range(1, bundle.k_watch + 1):
        if k not in reps:
            run(k)
    pooled = pool_scores(pending.step, scores.values())
    while armed and len(reps) < full and degraded_reasons(pooled, bundle.thresholds):
        k = next(i for i in range(1, full + 1) if i not in reps)
        print(f"[gate] step {pending.step}: degraded at {len(reps)} rep(s) — confirmation replicate {k}/{full}")
        run(k)
        pooled = pool_scores(pending.step, scores.values())
    degraded = bool(degraded_reasons(pooled, bundle.thresholds))
    confirmed = armed and degraded and len(reps) >= full
    return reps, scores, pooled, degraded, confirmed


class GateState:
    """Armed/streak state, rebuilt deterministically by replaying step dirs in order."""

    def __init__(self):
        self.armed_step = None
        self.reads = []


def process_step(pending, bundle, state, run_rep, run_id):
    """Judge one checkpoint, advance the gate state, and build its verdict.json payload."""
    armed = state.armed_step is not None
    if pending.reps:
        print(f"[gate] step {pending.step}: replaying {len(pending.reps)} replicate(s)")
    reps, scores, pooled, degraded, confirmed = judge_checkpoint(pending, bundle, armed, run_rep)
    if state.armed_step is None and not degraded:
        state.armed_step = pending.step
        print(f"[gate] armed at step {pending.step}: first healthy checkpoint — erosion detection live")
    read = CheckpointRead(pending.step, pooled, len(reps), degraded, confirmed)
    state.reads.append(read)
    current = verdict(state.reads)
    reasons = degraded_reasons(pooled, bundle.thresholds)
    payload = {
        "runId": run_id,
        "step": pending.step,
        "checkpoint": str(pending.checkpoint),
        "bundleId": bundle.bundle_id,
        "armed": state.armed_step is not None,
        "armingStep": state.armed_step,
        "evaderWins": pooled.evader_wins,
        "evaderEpisodes": pooled.evader_episodes,
        "totalWins": pooled.total_wins,
        "totalEpisodes": pooled.total_episodes,
        "evaderWilson95": list(wilson_interval(pooled.evader_wins, pooled.evader_episodes)),
        "totalWilson95": list(wilson_interval(pooled.total_wins, pooled.total_episodes)),
        "degraded": degraded,
        "confirmed": confirmed,
        "verdict": current,
        "reasons": reasons,
        # Evidence provenance: one run dir = one sample; each rep is one coordinator boot's dir.
        "evidence": {"reps": [{"rep": k, "summary": str(reps[k]),
                               "evaderWins": scores[k].evader_wins,
                               "totalWins": scores[k].total_wins}
                              for k in sorted(reps)]},
        "history": [{"step": r.step, "evaderWins": r.score.evader_wins,
                     "evaderEpisodes": r.score.evader_episodes, "totalWins": r.score.total_wins,
                     "totalEpisodes": r.score.total_episodes, "reps": r.reps,
                     "confirmed": r.confirmed} for r in state.reads],
    }
    return current, read, reasons, payload


def report(read, checkpoint: Path, current: str, reasons, armed: bool) -> None:
    tag = "" if armed else "  [report-only: unarmed]"
    print(f"[gate] {checkpoint.name}: Evader {read.score.evader_wins}/{read.score.evader_episodes}  "
          f"total {read.score.total_wins}/{read.score.total_episodes}  reps={read.reps}  -> {current}{tag}")
    if reasons and current == CONTINUE:
        print(f"[gate] step {read.step}: degraded but not confirmed: " + "; ".join(reasons))
    if current != CONTINUE:
        banner = "!" * 72
        print(banner)
        print(f"[gate] {current} at step {read.step}: " + "; ".join(reasons))
        if current == STOP:
            print("[gate] two consecutive CONFIRMED degraded checkpoints — pursuit is eroding, not noise")
        print(banner)
    sys.stdout.flush()


def bundle_protocol(bundle, seeds_arg=None, episodes_per_seed_arg=None):
    """The calibration bundle is the seed authority: the legacy flags may only restate its
    protocol — an off-bundle value would silently detach verdicts from the bundle id they
    record. Returns (seeds_csv, episodes_per_seed); the escape hatch is authoring a new
    bundle file, never a flag."""
    if seeds_arg:
        try:
            requested = sorted(int(s.strip()) for s in seeds_arg.split(",") if s.strip())
        except ValueError:
            sys.exit(f"FAIL: --seeds {seeds_arg!r} is not a comma-separated seed list")
        if requested != sorted(bundle.seeds):
            sys.exit(f"FAIL: --seeds {seeds_arg} differs from calibration bundle {bundle.bundle_id}'s "
                     f"seed set {','.join(str(s) for s in bundle.seeds)}; the bundle is the seed "
                     f"authority — author a new bundle instead of overriding the flag")
    if episodes_per_seed_arg is not None and episodes_per_seed_arg != bundle.episodes_per_seed:
        sys.exit(f"FAIL: --episodes-per-seed {episodes_per_seed_arg} differs from calibration bundle "
                 f"{bundle.bundle_id}'s {bundle.episodes_per_seed}; the bundle is the authority "
                 f"— author a new bundle instead of overriding the flag")
    return ",".join(str(s) for s in bundle.seeds), bundle.episodes_per_seed


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--run-id", required=True, help="mlagents run id under results/rl-training")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the eval boots in (point at a free pool slot, not the tree you work in)")
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--results-dir", type=Path, default=RESULTS, help="mlagents results root")
    parser.add_argument("--out-root", type=Path, default=GATE_ROOT, help="gate-owned artifact root")
    parser.add_argument("--bundle", type=Path, default=BUNDLE_V1,
                        help="calibration bundle judging every verdict (default: v1)")
    parser.add_argument("--seeds", default=None,
                        help="may only restate the bundle's seed set (compat; the bundle is the seed authority)")
    parser.add_argument("--episodes-per-seed", type=int, default=None,
                        help="may only restate the bundle's episodes/seed (compat; the bundle is the authority)")
    parser.add_argument("--poll-seconds", type=float, default=120.0, help="checkpoint-dir poll interval")
    parser.add_argument("--from-step", type=int, default=0, help="ignore checkpoints at or below this step")
    parser.add_argument("--max-checkpoints", type=int, default=0, help="stop after N checkpoints (0 = unbounded)")
    parser.add_argument("--once", action="store_true", help="evaluate the checkpoints present now, then exit")
    # Per-run by default: the coordinator keys ownership on the lease, so two gates sharing one
    # lease name would renew each other's ownership instead of queueing.
    parser.add_argument("--lease", default=None, help="unity-access lease name (default: rl-eval-gate-<run-id>)")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane per eval")
    parser.add_argument("--auto-stop-pid", type=int, default=None, metavar="PID",
                        help="OPT-IN: kill this trainer process tree on a STOP verdict (default: report only)")
    args = parser.parse_args()
    bundle = load_bundle(args.bundle)
    seeds, episodes_per_seed = bundle_protocol(bundle, args.seeds, args.episodes_per_seed)
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
    print(f"[gate] bundle {bundle.bundle_id}  eval project {args.project}  "
          f"seeds {seeds} x {episodes_per_seed}  artifacts {gate_dir}")

    state = GateState()
    for pending in watch_checkpoints(behavior_dir, gate_dir, from_step=args.from_step,
                                     poll_seconds=args.poll_seconds, once=args.once):
        def run_rep(out_dir, k, checkpoint=pending.checkpoint):
            return run_eval_lane(project=args.project, unity=unity, lease=args.lease,
                                 out_dir=out_dir, onnx=checkpoint, seeds=seeds,
                                 episodes_per_seed=episodes_per_seed, lease_wait=args.lease_wait)
        current, read, reasons, payload = process_step(pending, bundle, state, run_rep, args.run_id)
        (pending.out_dir / "verdict.json").write_text(json.dumps(payload, indent=2))
        report(read, pending.checkpoint, current, reasons, state.armed_step is not None)
        if current == STOP:
            if args.auto_stop_pid:
                print(f"[gate] --auto-stop-pid {args.auto_stop_pid}: killing the trainer tree")
                terminate_pid_tree(args.auto_stop_pid)
            sys.exit(2)
        if args.max_checkpoints and len(state.reads) >= args.max_checkpoints:
            print(f"[gate] reached --max-checkpoints {args.max_checkpoints}")
            return
    print(f"[gate] --once: evaluated {len(state.reads)} checkpoint(s)")


if __name__ == "__main__":
    main()
