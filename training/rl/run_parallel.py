"""Parallel-worker training driver: runs mlagents-learn --env <player exe> --num-envs N,
launching N headless copies of PR-1's RLTraining standalone player under one trainer. Each
worker derives an independent run seed from its ML-Agents port offset against the launcher's
--harness-base-port (see TrainingHost.ResolveWorkerIndex), so the N copies produce
decorrelated experience instead of near-duplicate rollouts. --num-arenas M > 1 additionally
fans each worker out to M in-process arenas (TrainingHost --harness-num-arenas), each writing
its own -w{k}-a{j} JSONL. FAILS unless the trainer exits 0, a checkpoint is exported, and
every expected episode JSONL (-w{k}, or -w{k}-a{j} when M > 1) is present and non-empty.

Build the --env exe first with RLTrainingPlayerBuild (see README). Unlike run_training.py /
run_smoke.py this does NOT boot an editor and does NOT route through unity-access — headless
player exes touch neither the shared editor nor MCP. Run from training/rl with the venv set up.

Merge gate (2-env liveness smoke):
    python run_parallel.py --smoke --num-envs 2 --force
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
RESULTS = REPO_ROOT / "results" / "rl-training"
JSONL_DIR = REPO_ROOT / "results" / "rl-episodes"
MLAGENTS = RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"


def config_run_id(config: Path) -> str:
    match = re.search(r"^\s*run_id:\s*(\S+)", config.read_text(), re.MULTILINE)
    if not match:
        sys.exit(f"FAIL: no checkpoint_settings run_id in {config}; pass --run-id")
    return match.group(1)


def episode_logs(suffix: str) -> set:
    return set(JSONL_DIR.glob(f"*-training{suffix}.jsonl"))


def log_suffixes(num_envs: int, num_arenas: int) -> list:
    if num_arenas > 1:
        return [f"-w{k}-a{j}" for k in range(num_envs) for j in range(num_arenas)]
    return [f"-w{k}" for k in range(num_envs)]


def terminate_tree(trainer: subprocess.Popen) -> None:
    # On timeout the trainer has spawned the N RLTraining.exe workers; on Windows a killed
    # parent doesn't take its children down, so kill the whole tree or the headless workers
    # keep holding ports/CPU (CLAUDE.md session-hygiene orphan-lock hazard).
    if trainer.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(trainer.pid)], capture_output=True)
    else:
        trainer.kill()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--env", type=Path, default=REPO_ROOT / "build" / "rl-training" / "RLTraining.exe",
                        help="headless player built by RLTrainingPlayerBuild")
    parser.add_argument("--num-envs", type=int, default=2, help="parallel worker copies under one trainer")
    parser.add_argument("--num-arenas", type=int, default=1,
                        help="in-process arenas per worker (TrainingHost --harness-num-arenas)")
    parser.add_argument("--config", type=Path, default=None,
                        help="trainer YAML (default: the smoke config under --smoke, else the full 2M config)")
    parser.add_argument("--run-id", default=None, help="mlagents run id (default: the config's run_id)")
    parser.add_argument("--base-port", type=int, default=5006,
                        help="base ML-Agents port; passed to both --base-port and the workers' --harness-base-port")
    parser.add_argument("--smoke", action="store_true",
                        help="RL_SMOKE=1 tight-arena/short-clock gate spec; also defaults --config to the smoke YAML")
    parser.add_argument("--resume", action="store_true", help="resume the run id's existing checkpoints")
    parser.add_argument("--force", action="store_true", help="overwrite the run id's existing results")
    parser.add_argument("--run-timeout", type=float, default=172800.0,
                        help="seconds to wait for the trainer to finish (default 48h)")
    args = parser.parse_args()
    if args.resume and args.force:
        parser.error("--resume and --force are mutually exclusive")
    if args.num_envs < 1:
        parser.error("--num-envs must be >= 1")
    if args.num_arenas < 1:
        parser.error("--num-arenas must be >= 1")
    if not args.env.exists():
        sys.exit(f"FAIL: player exe not found at {args.env}; build it first (RLTrainingPlayerBuild — see README)")

    config = args.config or (RL_DIR / ("ppo_ship_combat_smoke.yaml" if args.smoke else "ppo_ship_combat.yaml"))
    run_id = args.run_id or config_run_id(config)
    onnx = RESULTS / run_id / "ShipCombat.onnx"
    RESULTS.mkdir(parents=True, exist_ok=True)
    JSONL_DIR.mkdir(parents=True, exist_ok=True)
    suffixes = log_suffixes(args.num_envs, args.num_arenas)
    before = {s: episode_logs(s) for s in suffixes}

    # mlagents-learn spawns each worker as a subprocess inheriting this env, so RL_SMOKE reaches them all.
    env = dict(os.environ)
    if args.smoke:
        env["RL_SMOKE"] = "1"
    else:
        env.pop("RL_SMOKE", None)

    trainer_cmd = [str(MLAGENTS), str(config),
                   "--run-id", run_id,
                   "--env", str(args.env),
                   "--num-envs", str(args.num_envs),
                   "--base-port", str(args.base_port),
                   "--no-graphics"]
    if args.resume:
        trainer_cmd.append("--resume")
    if args.force:
        trainer_cmd.append("--force")
    # --env-args must trail: mlagents-learn forwards the remainder to every worker's argv, alongside --mlagents-port.
    trainer_cmd += ["--env-args",
                    "--harness-base-port", str(args.base_port),
                    "--harness-jsonl-dir", str(JSONL_DIR),
                    "--harness-num-arenas", str(args.num_arenas)]

    trainer_log = RESULTS / f"{run_id}-parallel-trainer.log"
    print(f"launching {args.num_envs} worker(s): {args.env}")
    print(f"  base-port {args.base_port}  jsonl {JSONL_DIR}  run-id {run_id}")
    trainer = None
    try:
        with open(trainer_log, "w") as tl:
            trainer = subprocess.Popen(trainer_cmd, cwd=RL_DIR, env=env, stdout=tl, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + args.run_timeout
        while trainer.poll() is None:
            if time.monotonic() > deadline:
                sys.exit(f"FAIL: timed out after {args.run_timeout:.0f}s waiting for the trainer (see {trainer_log})")
            time.sleep(10.0)
    finally:
        if trainer:
            terminate_tree(trainer)
    if trainer.returncode != 0:
        sys.exit(f"FAIL: mlagents-learn exited {trainer.returncode} (see {trainer_log})")

    failures = []
    if not onnx.exists():
        failures.append(f"no checkpoint exported at {onnx}")
    for s in suffixes:
        fresh = [p for p in (episode_logs(s) - before[s]) if p.stat().st_size > 0]
        if not fresh:
            failures.append(f"{s}: no non-empty {s} episode JSONL under {JSONL_DIR}")
        else:
            print(f"{s}: {fresh[0].name} ({fresh[0].stat().st_size} bytes)")

    if failures:
        sys.exit("FAIL:\n  " + "\n  ".join(failures))
    print(f"PASS: {args.num_envs}-env run '{run_id}' complete")
    print(f"  checkpoint: {onnx}")
    print(f"  episodes:   {JSONL_DIR}")


if __name__ == "__main__":
    main()
