"""Throughput bench for the parallel training path: answers "how many policy steps per second
does this configuration sustain, and at what CPU cost".

Runs a short training job through run_parallel.py (the launcher stays the sole owner of the
trainer invocation) with max_steps cut to --steps, then reports steady-state steps/s from
consecutive trainer-summary deltas, plus cores and peak RSS sampled from the worker processes.

The first summary interval is DISCARDED: it carries environment boot and the trainer's first
policy update, and including it was the single largest source of disagreement between
hand-rolled measurements. Steady state is the mean of the remaining intervals.

Comparisons are only meaningful between rows that share a config, --steps, and
--initialize-from: a warm-started policy ends episodes differently than a random one, which
changes the per-episode work mix. Hold those constant across an A/B.

--trainer-runtime picks the arm: 'owned' reads run_manifest.json + summaries.jsonl; the
'ml-agents' stock reference arm reads (step, elapsed) from the stock console summaries in
the launcher's trainer log, because stock deliberately emits no owned telemetry files.

Resolution: repeat runs of one config land within ~4% on a loaded desktop, so this resolves
effects above roughly 10% and cannot adjudicate small ones — repeat both arms if a result
lands inside the noise. The trailing interval also reads systematically low (it abuts the
final update and model export); it is kept because both arms of a comparison pay it equally.

    python bench_throughput.py --num-envs 4 --label baseline
    python bench_throughput.py --num-envs 4 --label no-dev-build
    python bench_throughput.py --report
"""
import argparse
import json
import re
import statistics
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

import yaml

from run_parallel import REPO_ROOT, RESULTS, RL_DIR, TRAINER_RUNTIMES, trainer_log_path
from trainer_runtime.contract import (config_sha256, manifest_path, read_manifest,
                                      read_summaries, summaries_path)

BENCH_DIR = REPO_ROOT / "results" / "rl-bench"
RESULTS_JSONL = BENCH_DIR / "bench.jsonl"
# Enough summaries that discarding the first still leaves several intervals to average.
TARGET_SUMMARIES = 6


def git_sha() -> str:
    proc = subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=REPO_ROOT,
                          capture_output=True, text=True)
    return proc.stdout.strip() or "unknown"


def sample_workers() -> tuple:
    """(process count, summed CPU seconds, summed RSS bytes) for the player workers.

    Shells out to PowerShell because psutil is not in the pinned lock and a bench does not
    justify moving it.
    """
    script = ("$p=@(Get-Process RLTraining -ErrorAction SilentlyContinue);"
              "$c=0.0;$m=0.0;foreach($x in $p){$c+=$x.TotalProcessorTime.TotalSeconds;$m+=$x.WorkingSet64};"
              "'{0} {1} {2}' -f $p.Count,$c,$m")
    proc = subprocess.run(["powershell", "-NoProfile", "-Command", script],
                          capture_output=True, text=True)
    parts = proc.stdout.split()
    if len(parts) != 3:
        return 0, 0.0, 0.0
    return int(parts[0]), float(parts[1]), float(parts[2])


class CpuSampler(threading.Thread):
    """Samples worker CPU/RSS on an interval until stopped."""

    def __init__(self, interval: float = 10.0):
        super().__init__(daemon=True)
        self.interval = interval
        self.samples = []  # (wall_time, cpu_seconds, rss_bytes, n_procs)
        self._halt = threading.Event()

    def run(self) -> None:
        while not self._halt.is_set():
            n, cpu, rss = sample_workers()
            if n:
                self.samples.append((time.monotonic(), cpu, rss, n))
            self._halt.wait(self.interval)

    def stop(self) -> None:
        self._halt.set()

    def steady_cores(self) -> tuple:
        """(mean cores across all workers, peak worker count) over the steady window.

        Drops the leading samples so worker boot does not depress the average.
        """
        usable = self.samples[2:] if len(self.samples) > 4 else self.samples
        if len(usable) < 2:
            return 0.0, max((s[3] for s in self.samples), default=0)
        span = usable[-1][0] - usable[0][0]
        cores = (usable[-1][1] - usable[0][1]) / span if span > 0 else 0.0
        return cores, max(s[3] for s in usable)

    def peak_rss_mb(self) -> float:
        return max((s[2] for s in self.samples), default=0.0) / (1024 * 1024)


def bench_config(source: Path, steps: int, run_id: str) -> Path:
    """Copy the trainer config with max_steps cut to the bench budget and summaries tightened.

    summary_freq only controls logging cadence, so tightening it buys measurement resolution
    without touching learning semantics.
    """
    cfg = yaml.safe_load(source.read_text())
    behaviors = cfg.get("behaviors")
    if not behaviors:
        sys.exit(f"FAIL: no behaviors block in {source}")
    for behavior in behaviors.values():
        behavior["max_steps"] = steps
        behavior["summary_freq"] = max(1000, steps // TARGET_SUMMARIES)
    cfg.setdefault("checkpoint_settings", {})["run_id"] = run_id
    BENCH_DIR.mkdir(parents=True, exist_ok=True)
    out = BENCH_DIR / f"{run_id}.yaml"
    out.write_text(yaml.safe_dump(cfg, sort_keys=False))
    return out


def parse_summaries(path: Path) -> list:
    return [(summary.step, summary.elapsed_seconds) for summary in read_summaries(path)]


# Stock console summary: "ShipCombat. Step: N. Time Elapsed: T s. ..." — both the reward and
# the "No episode was completed" variants carry the Step/Time Elapsed pair.
STOCK_SUMMARY = re.compile(r"Step: (\d+)\. Time Elapsed: ([\d.]+) s")


def parse_stock_summaries(log_path: Path) -> list:
    """(step, elapsed) pairs from stock stdout summaries in the launcher's trainer log.

    The stock reference arm has no summaries.jsonl by design — the owned stats writer must
    never ride `--trainer-runtime ml-agents` runs (wire contract §8 ruling).
    """
    if not log_path.exists():
        return []
    text = log_path.read_text(encoding="utf-8", errors="replace")
    return [(int(m.group(1)), float(m.group(2))) for m in STOCK_SUMMARY.finditer(text)]


def collect_run_metrics(runtime: str, run_id: str, config: Path) -> tuple:
    """((step, elapsed) list, config hash) from the arm's native telemetry surface."""
    if runtime == "owned":
        manifest = read_manifest(manifest_path(RESULTS, run_id))
        return parse_summaries(summaries_path(manifest.run_dir)), manifest.config_hash
    return parse_stock_summaries(trainer_log_path(run_id)), config_sha256(config)


MICROBATCH_TIMER_FIELDS = (
    "microbatch_requested_worker_cap",
    "microbatch_effective_worker_cap",
    "microbatch_window_micros",
    "microbatch_policy_forward_count",
    "microbatch_worker_request_count",
    "microbatch_agent_row_count",
    "microbatch_mean_workers_per_forward",
    "microbatch_max_workers_per_forward",
)


def collect_microbatch_metrics(runtime: str, run_id: str) -> dict:
    if runtime != "owned":
        return {key: None for key in MICROBATCH_TIMER_FIELDS}
    path = RESULTS / run_id / "run_logs" / "timers.json"
    timers = json.loads(path.read_text(encoding="utf-8"))
    metadata = timers.get("metadata", {})
    missing = [key for key in MICROBATCH_TIMER_FIELDS if key not in metadata]
    if missing:
        sys.exit(f"FAIL: timers metadata missing microbatch fields: {missing}")
    return {
        key: float(metadata[key]) if "mean" in key else int(metadata[key])
        for key in MICROBATCH_TIMER_FIELDS
    }


def launch_command(args, config: Path) -> list:
    cmd = [sys.executable, str(RL_DIR / "run_parallel.py"),
           "--config", str(config),
           "--num-envs", str(args.num_envs),
           "--num-arenas", str(args.num_arenas),
           "--trainer-runtime", args.trainer_runtime,
           "--force"]
    if args.self_play:
        cmd.append("--self-play")
    if args.initialize_from:
        cmd += ["--initialize-from", args.initialize_from]
    if args.env:
        cmd += ["--env", str(args.env)]
    if args.trainer_runtime == "owned":
        cmd += ["--microbatch-worker-cap", str(args.microbatch_worker_cap)]
    return cmd


def steady_steps_per_second(summaries: list) -> tuple:
    """(mean steps/s, spread as max-min over intervals, interval count), first interval dropped."""
    if len(summaries) < 3:
        return 0.0, 0.0, 0
    rates = []
    for (s0, t0), (s1, t1) in zip(summaries, summaries[1:]):
        if t1 > t0:
            rates.append((s1 - s0) / (t1 - t0))
    rates = rates[1:]  # the first interval carries env boot + first update
    if not rates:
        return 0.0, 0.0, 0
    return statistics.fmean(rates), max(rates) - min(rates), len(rates)


def report() -> None:
    if not RESULTS_JSONL.exists():
        sys.exit(f"no bench rows yet at {RESULTS_JSONL}")
    rows = [json.loads(line) for line in RESULTS_JSONL.read_text().splitlines() if line.strip()]
    print(
        f"{'label':<22}{'runtime':<10}{'N':>3}{'M':>3}{'cap req/eff':>12}"
        f"{'win us':>8}{'workers/fwd':>14}{'forwards':>10}{'requests':>10}"
        f"{'rows':>10}{'steps/s':>10}{'spread':>8}{'cores':>8}{'RSS MB':>9}  sha"
    )
    for row in rows:
        requested_cap = row.get("microbatch_requested_worker_cap")
        effective_cap = row.get("microbatch_effective_worker_cap")
        cap_text = (
            "-"
            if requested_cap is None or effective_cap is None
            else f"{requested_cap}/{effective_cap}"
        )
        window = row.get("microbatch_window_micros")
        window_text = "-" if window is None else str(window)
        mean_workers = row.get("microbatch_mean_workers_per_forward")
        max_workers = row.get("microbatch_max_workers_per_forward")
        fill_text = "-" if mean_workers is None else f"{mean_workers:.2f}/{max_workers}"
        forwards = row.get("microbatch_policy_forward_count")
        requests = row.get("microbatch_worker_request_count")
        agent_rows = row.get("microbatch_agent_row_count")
        forwards_text = "-" if forwards is None else str(forwards)
        requests_text = "-" if requests is None else str(requests)
        rows_text = "-" if agent_rows is None else str(agent_rows)
        print(f"{row['label']:<22}{row.get('trainer_runtime', '?'):<10}{row['num_envs']:>3}{row['num_arenas']:>3}"
              f"{cap_text:>12}{window_text:>8}{fill_text:>14}{forwards_text:>10}"
              f"{requests_text:>10}{rows_text:>10}"
              f"{row['steps_per_second']:>10.1f}{row['spread']:>8.1f}"
              f"{row['cores']:>8.2f}{row['peak_rss_mb']:>9.0f}  {row['git_sha']}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--label", help="name for this bench row (e.g. 'baseline', 'no-dev-build')")
    parser.add_argument("--num-envs", type=int, default=4)
    parser.add_argument("--num-arenas", type=int, default=1)
    parser.add_argument("--steps", type=int, default=24000, help="trainer steps to run (max_steps)")
    parser.add_argument("--config", type=Path, default=None,
                        help="trainer YAML (default: the scripted-roster config, or the selfplay "
                             "config under --self-play)")
    parser.add_argument("--self-play", action="store_true",
                        help="passed through to run_parallel.py (RL_SELFPLAY=1 ghost-league composition)")
    parser.add_argument("--initialize-from", metavar="RUN_ID", default=None,
                        help="warm-start run id; hold constant across an A/B")
    parser.add_argument("--env", type=Path, default=None, help="player exe (defaults to run_parallel's)")
    parser.add_argument("--trainer-runtime", choices=TRAINER_RUNTIMES, default="owned",
                        help="arm to bench; 'ml-agents' is the stock reference arm of a paired bench")
    parser.add_argument("--microbatch-worker-cap", type=int, default=1,
                        help="owned-runtime workers per inference forward")
    parser.add_argument("--report", action="store_true", help="print collected rows and exit")
    args = parser.parse_args()

    if args.report:
        report()
        return
    if not args.label:
        parser.error("--label is required (it names the bench row)")
    if args.microbatch_worker_cap < 1:
        parser.error("--microbatch-worker-cap must be a positive integer")

    run_id = f"bench-{args.label}"
    source = args.config or RL_DIR / ("ppo_ship_combat_selfplay.yaml" if args.self_play else "ppo_ship_combat.yaml")
    config = bench_config(source, args.steps, run_id)
    cmd = launch_command(args, config)

    print(f"bench '{args.label}': N={args.num_envs} M={args.num_arenas} steps={args.steps} "
          f"runtime={args.trainer_runtime}")
    sampler = CpuSampler()
    sampler.start()
    started = time.monotonic()
    proc = subprocess.run(cmd, cwd=RL_DIR)
    sampler.stop()
    sampler.join(timeout=15)
    wall = time.monotonic() - started

    if proc.returncode != 0:
        sys.exit(f"FAIL: run_parallel exited {proc.returncode}; see {trainer_log_path(run_id)}")

    summaries, config_hash = collect_run_metrics(args.trainer_runtime, run_id, config)
    microbatch_metrics = collect_microbatch_metrics(args.trainer_runtime, run_id)
    rate, spread, intervals = steady_steps_per_second(summaries)
    if intervals < 2:
        sys.exit(f"FAIL: only {len(summaries)} trainer summaries — raise --steps for a steady-state read")
    cores, peak_workers = sampler.steady_cores()

    row = {
        "timestamp": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "label": args.label,
        "git_sha": git_sha(),
        "num_envs": args.num_envs,
        "num_arenas": args.num_arenas,
        "steps": args.steps,
        "config": source.name,
        "config_hash": config_hash,
        "trainer_runtime": args.trainer_runtime,
        "initialize_from": args.initialize_from,
        "steps_per_second": round(rate, 2),
        "spread": round(spread, 2),
        "intervals": intervals,
        "cores": round(cores, 2),
        "cores_per_worker": round(cores / peak_workers, 2) if peak_workers else 0.0,
        "peak_rss_mb": round(sampler.peak_rss_mb(), 1),
        "wall_seconds": round(wall, 1),
        **microbatch_metrics,
    }
    BENCH_DIR.mkdir(parents=True, exist_ok=True)
    with open(RESULTS_JSONL, "a") as handle:
        handle.write(json.dumps(row) + "\n")

    print(f"PASS: {rate:.1f} steps/s (spread {spread:.1f} over {intervals} intervals)")
    print(f"  cores {cores:.2f} total / {row['cores_per_worker']:.2f} per worker"
          f"   peak RSS {row['peak_rss_mb']:.0f} MB   wall {wall:.0f}s")
    if args.trainer_runtime == "owned":
        print(
            "  microbatch "
            f"cap {row['microbatch_requested_worker_cap']} requested / "
            f"{row['microbatch_effective_worker_cap']} effective "
            f"window {row['microbatch_window_micros']}us  "
            f"workers/forward {row['microbatch_mean_workers_per_forward']:.2f} mean / "
            f"{row['microbatch_max_workers_per_forward']} max  "
            f"forwards {row['microbatch_policy_forward_count']}  "
            f"requests {row['microbatch_worker_request_count']}  "
            f"agent rows {row['microbatch_agent_row_count']}"
        )
    print(f"  row appended to {RESULTS_JSONL}")


if __name__ == "__main__":
    main()
