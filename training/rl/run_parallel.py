"""Parallel-worker training driver: runs the selected trainer runtime against N headless
copies of PR-1's RLTraining standalone player. Each
worker derives an independent run seed from its ML-Agents port offset against the launcher's
--harness-base-port (see TrainingHost.ResolveWorkerIndex), so the N copies produce
decorrelated experience instead of near-duplicate rollouts. --num-arenas M > 1 additionally
fans each worker out to M in-process arenas (TrainingHost --harness-num-arenas), each writing
its own -w{k}-a{j} JSONL. FAILS unless the trainer exits 0, a checkpoint is exported, and
every expected episode JSONL (-w{k}, or -w{k}-a{j} when M > 1) is present and non-empty.

Build the --env exe first with RLTrainingPlayerBuild (see README). Unlike run_training.py /
run_smoke.py this does NOT boot an editor and does NOT route through unity-access — headless
player exes touch neither the shared editor nor MCP. Run from training/rl with the venv set up.

--initialize-from RUN_ID warm-starts a fresh run from another run's weights (self-play
graduation seeds from the curriculum winner). mlagents resolves it as
<results_dir>/RUN_ID/<behavior>/checkpoint.pt, so an archived run must be staged under
results/rl-training/ first; a path that doesn't resolve throws before any worker boots.

Merge gate (2-env liveness smoke):
    python run_parallel.py --smoke --num-envs 2 --force
"""
import argparse
import os
import re
import signal
import subprocess
import sys
import time
from pathlib import Path

from driver_common import config_has_self_play, config_run_id

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
RESULTS = REPO_ROOT / "results" / "rl-training"
JSONL_DIR = REPO_ROOT / "results" / "rl-episodes"
MLAGENTS = RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"
TRAINER_RUNTIMES = ("owned", "ml-agents")

# TrainingHost.ComposeSuffix (C#) owns this format; RLDriverContractEditModeTests pins the pair.
WORKER_SUFFIX = "-w{k}"
ARENA_SUFFIX = "-a{j}"


def trainer_log_path(run_id: str) -> Path:
    """Where this launcher writes the trainer's stdout; consumers import this rather than rebuild it."""
    return RESULTS / f"{run_id}-parallel-trainer.log"


def trainer_command(runtime: str) -> list[str]:
    if runtime == "owned":
        return [sys.executable, "-m", "trainer_runtime.entry"]
    return [str(MLAGENTS)]


def config_has_roster_weights(config: Path) -> bool:
    return re.search(r"^\s*opponent_weight_", config.read_text(), re.MULTILINE) is not None


def episode_logs(suffix: str) -> set:
    return set(JSONL_DIR.glob(f"*-training{suffix}.jsonl"))


def log_suffixes(num_envs: int, num_arenas: int) -> list:
    # The arena part appears only when fanning out, so M=1 filenames stay byte-identical.
    if num_arenas > 1:
        return [WORKER_SUFFIX.format(k=k) + ARENA_SUFFIX.format(j=j)
                for k in range(num_envs) for j in range(num_arenas)]
    return [WORKER_SUFFIX.format(k=k) for k in range(num_envs)]


def terminate_pid_tree(pid: int) -> None:
    # On Windows a killed parent leaves its workers holding ports/CPU (CLAUDE.md orphan-lock hazard).
    if os.name == "nt":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(pid)], capture_output=True)
    else:
        os.kill(pid, signal.SIGKILL)


def terminate_tree(trainer: subprocess.Popen) -> None:
    if trainer.poll() is None:
        terminate_pid_tree(trainer.pid)


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
    parser.add_argument("--trainer-runtime", choices=TRAINER_RUNTIMES, default="owned",
                        help="trainer entry implementation; owned delegates the loop to pinned ML-Agents")
    parser.add_argument("--base-port", type=int, default=5006,
                        help="base ML-Agents port; passed to both --base-port and the workers' --harness-base-port")
    parser.add_argument("--smoke", action="store_true",
                        help="RL_SMOKE=1 tight-arena/short-clock gate spec; also defaults --config to the smoke YAML")
    parser.add_argument("--self-play", action="store_true",
                        help="RL_SELFPLAY=1 ghost-league composition; also defaults --config to the selfplay YAML")
    parser.add_argument("--hybrid-scripted-workers", type=int, default=None, metavar="K",
                        help="first K workers boot the scripted roster instead of the mirror league "
                             "(RL_HYBRID_SCRIPTED_WORKERS; requires --self-play)")
    parser.add_argument("--initialize-from", metavar="RUN_ID", default=None,
                        help="warm-start fresh weights from another run id under results/rl-training")
    parser.add_argument("--resume", action="store_true", help="resume the run id's existing checkpoints")
    parser.add_argument("--force", action="store_true", help="overwrite the run id's existing results")
    parser.add_argument("--run-timeout", type=float, default=172800.0,
                        help="seconds to wait for the trainer to finish (default 48h)")
    args = parser.parse_args()
    if args.resume and args.force:
        parser.error("--resume and --force are mutually exclusive")
    # mlagents only warns and silently drops --resume in this combination; refuse it at the boundary.
    if args.resume and args.initialize_from:
        parser.error("--resume and --initialize-from are mutually exclusive")
    if args.num_envs < 1:
        parser.error("--num-envs must be >= 1")
    if args.hybrid_scripted_workers is not None:
        if not args.self_play:
            parser.error("--hybrid-scripted-workers only splits a self-play league; pass --self-play")
        if args.hybrid_scripted_workers < 0:
            parser.error("--hybrid-scripted-workers must be >= 0")
        # At K == num_envs every worker boots scripted: a self-play config with no ghost league at all.
        if args.hybrid_scripted_workers >= args.num_envs:
            parser.error(f"--hybrid-scripted-workers {args.hybrid_scripted_workers} leaves no mirror worker "
                         f"of --num-envs {args.num_envs}; the hybrid split needs both sides")
    if args.num_arenas < 1:
        parser.error("--num-arenas must be >= 1")
    if not args.env.exists():
        sys.exit(f"FAIL: player exe not found at {args.env}; build it first (RLTrainingPlayerBuild — see README)")

    defaults = {(False, False): "ppo_ship_combat.yaml",
                (False, True): "ppo_ship_combat_smoke.yaml",
                (True, False): "ppo_ship_combat_selfplay.yaml",
                (True, True): "ppo_ship_combat_selfplay_smoke.yaml"}
    hybrid = bool(args.hybrid_scripted_workers)
    if hybrid and not args.smoke:
        # Hybrid takes precedence over the plain selfplay default; the smoke gate keeps the
        # short-max_steps smoke YAML (a hybrid smoke proves worker composition, not the roster mix).
        defaults[(True, False)] = "ppo_ship_combat_hybrid.yaml"
    config = args.config or RL_DIR / defaults[(args.self_play, args.smoke)]
    # A flag/YAML mismatch trains the wrong thing while looking healthy — fail before boot.
    if args.self_play and not config_has_self_play(config):
        parser.error(f"--self-play passed but {config.name} has no self_play: block — "
                     "the trainer would run without a ghost league")
    if config_has_self_play(config) and not args.self_play:
        parser.error(f"{config.name} has a self_play: block but --self-play was not passed — "
                     "the harness would compose the scripted roster")
    if hybrid and args.config and not config_has_roster_weights(config):
        parser.error(f"--hybrid-scripted-workers passed but {config.name} has no opponent_weight_ params — "
                     "the scripted workers would silently fall back to RewardSpec's default roster mix")
    run_id = args.run_id or config_run_id(config)
    onnx = RESULTS / run_id / "ShipCombat.onnx"
    RESULTS.mkdir(parents=True, exist_ok=True)
    JSONL_DIR.mkdir(parents=True, exist_ok=True)
    suffixes = log_suffixes(args.num_envs, args.num_arenas)
    before = {s: episode_logs(s) for s in suffixes}

    # mlagents-learn workers inherit this env; the pops stop an inherited RL_SELFPLAY bypassing the cross-check.
    env = dict(os.environ)
    if args.smoke:
        env["RL_SMOKE"] = "1"
    else:
        env.pop("RL_SMOKE", None)
    if args.self_play:
        env["RL_SELFPLAY"] = "1"
    else:
        env.pop("RL_SELFPLAY", None)
    if args.hybrid_scripted_workers is not None:
        env["RL_HYBRID_SCRIPTED_WORKERS"] = str(args.hybrid_scripted_workers)
    else:
        env.pop("RL_HYBRID_SCRIPTED_WORKERS", None)

    trainer_cmd = trainer_command(args.trainer_runtime) + [str(config),
                   "--run-id", run_id,
                   "--env", str(args.env),
                   "--num-envs", str(args.num_envs),
                   "--base-port", str(args.base_port),
                   "--no-graphics"]
    if args.resume:
        trainer_cmd.append("--resume")
    if args.force:
        trainer_cmd.append("--force")
    if args.initialize_from:
        trainer_cmd += ["--initialize-from", args.initialize_from]
    # --env-args must trail: mlagents-learn forwards the remainder to every worker's argv, alongside --mlagents-port.
    trainer_cmd += ["--env-args",
                    "--harness-base-port", str(args.base_port),
                    "--harness-jsonl-dir", str(JSONL_DIR),
                    "--harness-num-arenas", str(args.num_arenas)]

    trainer_log = trainer_log_path(run_id)
    print(f"launching {args.num_envs} worker(s): {args.env}")
    print(f"  trainer-runtime {args.trainer_runtime}  base-port {args.base_port}  "
          f"jsonl {JSONL_DIR}  run-id {run_id}")
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
        sys.exit(f"FAIL: trainer runtime exited {trainer.returncode} (see {trainer_log})")

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
