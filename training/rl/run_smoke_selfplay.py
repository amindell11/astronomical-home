"""Asserting self-play smoke: the same trainer-connected boot as run_smoke.py but with
RL_SELFPLAY=1, so the opponent ship is a second team-1 ShipCombat agent. Proves native
mlagents self_play trains two team_ids under automatic Academy stepping and exports a
checkpoint. Run from training/rl with the venv (README.md); coordinate editor access
first (skills/unity-access) - this boots its own editor and binds trainer port 5004.
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

from unity_access import release_editor, start_editor

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
RESULTS = REPO_ROOT / "results" / "rl-training"
START_FLAG = RESULTS / "start-play.flag"
SMOKE_ONNX = RESULTS / "ship_combat_selfplay_smoke" / "ShipCombat.onnx"
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


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--boot-timeout", type=float, default=1800.0, help="seconds to wait for the editor to arm")
    parser.add_argument("--run-timeout", type=float, default=3600.0, help="seconds to wait for the trainer to finish")
    args = parser.parse_args()

    unity = args.unity or default_unity_exe()
    RESULTS.mkdir(parents=True, exist_ok=True)
    START_FLAG.unlink(missing_ok=True)
    editor_log = RESULTS / "selfplay-smoke-editor.log"
    editor_log.unlink(missing_ok=True)

    env = dict(os.environ, RL_SMOKE="1", RL_SELFPLAY="1")
    lease = "rl-selfplay-smoke"
    editor_pid = start_editor(
        lease, PROJECT,
        ["-batchmode", "-nographics",
         "-executeMethod", "Game.RLHarness.TrainingBootstrap.EnterTrainingPlayModeWhenSignaled",
         "-logFile", str(editor_log)],
        unity, env)
    print(f"editor pid {editor_pid} (owned by unity-access lease {lease})")
    trainer = None
    try:
        wait_for(lambda: log_contains(editor_log, ARMED_MARKER), "editor to arm", args.boot_timeout)

        trainer_log = RESULTS / "selfplay-smoke-trainer.log"
        with open(trainer_log, "w") as tl:
            trainer = subprocess.Popen(
                [str(RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"),
                 str(RL_DIR / "ppo_ship_combat_selfplay_smoke.yaml"), "--force"],
                cwd=RL_DIR, stdout=tl, stderr=subprocess.STDOUT)
        wait_for(lambda: log_contains(trainer_log, "Listening on port"), "trainer to listen", 300.0)

        START_FLAG.touch()
        wait_for(lambda: trainer.poll() is not None, "trainer to finish", args.run_timeout, poll_s=10.0)
        if trainer.returncode != 0:
            sys.exit(f"FAIL: mlagents-learn exited {trainer.returncode} (see {trainer_log})")
    finally:
        if trainer and trainer.poll() is None:
            trainer.kill()
        release_editor(lease, PROJECT, env)

    failures = []
    if not SMOKE_ONNX.exists():
        failures.append(f"no checkpoint exported at {SMOKE_ONNX}")
    log_text = editor_log.read_text(errors="replace")
    if PACING_MARKER not in log_text:
        failures.append(f"pacing-contract marker '{PACING_MARKER}' missing from {editor_log}")
    episode_lines = EPISODE_LINE.findall(log_text)
    if not episode_lines:
        failures.append(f"no [TrainingHost] episode lines in {editor_log}")
    else:
        terminals, truncations = (int(n) for n in episode_lines[-1])
        print(f"episodes={len(episode_lines)} terminals={terminals} truncations={truncations}")

    if failures:
        sys.exit("FAIL:\n  " + "\n  ".join(failures))
    print(f"PASS: self-play smoke complete; checkpoint at {SMOKE_ONNX}")


if __name__ == "__main__":
    main()
