"""Asserting long-run training driver (pilot or full): boots a batch-mode editor, runs
mlagents-learn against the given config, and FAILS unless the trainer exited 0, a
checkpoint was exported, and the pacing-contract marker was logged. Structurally parallel
to run_smoke.py (armed batch editor + start flag + log markers). Run from training/rl with
the venv set up (README.md); coordinate editor access first (skills/unity-access) - this
boots its own editor and pegs the CPU for the run's whole wall-clock.
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
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
RESULTS = REPO_ROOT / "results" / "rl-training"
START_FLAG = RESULTS / "start-play.flag"
ARMED_MARKER = "[TrainingBootstrap] armed"
PACING_MARKER = "[PacingContract] holds"
EPISODE_LINE = re.compile(r"\[TrainingHost\] episode \d+:.*terminals=(\d+) truncations=(\d+)")


def default_unity_exe() -> Path:
    version = re.search(r"m_EditorVersion: (\S+)",
                        (PROJECT / "ProjectSettings" / "ProjectVersion.txt").read_text()).group(1)
    candidates = [
        Path(rf"D:\Programs\Unity\Editor\{version}\Editor\Unity.exe"),
        Path(rf"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe"),
    ]
    for c in candidates:
        if c.exists():
            return c
    sys.exit(f"FAIL: Unity {version} not found; pass --unity (tried {[str(c) for c in candidates]})")


def wait_for(predicate, what: str, timeout_s: float, poll_s: float = 2.0):
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        if predicate():
            return
        time.sleep(poll_s)
    sys.exit(f"FAIL: timed out after {timeout_s:.0f}s waiting for {what}")


def log_contains(log_path: Path, needle: str) -> bool:
    return log_path.exists() and needle in log_path.read_text(errors="replace")


def config_run_id(config: Path) -> str:
    match = re.search(r"^\s*run_id:\s*(\S+)", config.read_text(), re.MULTILINE)
    if not match:
        sys.exit(f"FAIL: no checkpoint_settings run_id in {config}; pass --run-id")
    return match.group(1)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=RL_DIR / "ppo_ship_combat.yaml", help="trainer YAML (default: the full 2M-step config; use ppo_ship_combat_pilot.yaml for the pilot)")
    parser.add_argument("--run-id", default=None, help="mlagents run id (default: the config's checkpoint_settings run_id)")
    parser.add_argument("--resume", action="store_true", help="resume the run id's existing checkpoints")
    parser.add_argument("--force", action="store_true", help="overwrite the run id's existing results")
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--boot-timeout", type=float, default=1800.0, help="seconds to wait for the editor to arm")
    parser.add_argument("--run-timeout", type=float, default=172800.0, help="seconds to wait for the trainer to finish (default 48h; training is frame-rate-bound)")
    args = parser.parse_args()
    if args.resume and args.force:
        parser.error("--resume and --force are mutually exclusive")

    unity = args.unity or default_unity_exe()
    run_id = args.run_id or config_run_id(args.config)
    onnx = RESULTS / run_id / "ShipCombat.onnx"
    RESULTS.mkdir(parents=True, exist_ok=True)
    START_FLAG.unlink(missing_ok=True)
    editor_log = RESULTS / f"{run_id}-editor.log"
    editor_log.unlink(missing_ok=True)

    # An inherited RL_SMOKE=1 would silently shrink TrainingHost to the smoke arena/clock.
    editor_env = {k: v for k, v in os.environ.items() if k != "RL_SMOKE"}
    editor = subprocess.Popen(
        [str(unity), "-projectPath", str(PROJECT), "-batchmode", "-nographics",
         "-executeMethod", "Game.RLHarness.TrainingBootstrap.EnterTrainingPlayModeWhenSignaled",
         "-logFile", str(editor_log)],
        env=editor_env)
    trainer = None
    try:
        wait_for(lambda: log_contains(editor_log, ARMED_MARKER), "editor to arm", args.boot_timeout)

        trainer_cmd = [str(RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"),
                       str(args.config), "--run-id", run_id]
        if args.resume:
            trainer_cmd.append("--resume")
        if args.force:
            trainer_cmd.append("--force")

        trainer_log = RESULTS / f"{run_id}-trainer.log"
        with open(trainer_log, "w") as tl:
            trainer = subprocess.Popen(trainer_cmd, cwd=RL_DIR, stdout=tl, stderr=subprocess.STDOUT)
        wait_for(lambda: log_contains(trainer_log, "Listening on port"), "trainer to listen", 300.0)

        START_FLAG.touch()
        wait_for(lambda: trainer.poll() is not None, "trainer to finish", args.run_timeout, poll_s=30.0)
        if trainer.returncode != 0:
            sys.exit(f"FAIL: mlagents-learn exited {trainer.returncode} (see {trainer_log})")
    finally:
        if trainer and trainer.poll() is None:
            trainer.kill()
        editor.kill()
        editor.wait()

    failures = []
    if not onnx.exists():
        failures.append(f"no checkpoint exported at {onnx}")
    log_text = editor_log.read_text(errors="replace")
    if PACING_MARKER not in log_text:
        failures.append(f"pacing-contract marker '{PACING_MARKER}' missing from {editor_log}")
    episode_lines = EPISODE_LINE.findall(log_text)
    if episode_lines:
        terminals, truncations = (int(n) for n in episode_lines[-1])
        print(f"episodes={len(episode_lines)} terminals={terminals} truncations={truncations}")

    if failures:
        sys.exit("FAIL:\n  " + "\n  ".join(failures))
    print(f"PASS: training run '{run_id}' complete")
    print(f"  checkpoints: {RESULTS / run_id}")
    print(f"  final onnx:  {onnx}")
    print(f"  tensorboard: .venv\\Scripts\\tensorboard --logdir {RESULTS}")


if __name__ == "__main__":
    main()
