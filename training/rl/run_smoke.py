"""Asserting trainer-connected smoke: boots a batch-mode editor, runs mlagents-learn
against ppo_ship_combat_smoke.yaml, and FAILS unless the run completed, a checkpoint
was exported, the pacing-contract marker was logged, and both a terminal (EndEpisode)
and a truncation (EpisodeInterrupted) occurred. Run from training/rl with the venv set up
(README.md); coordinate editor access first (skills/unity-access) - this boots its own editor.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
COORDINATOR = REPO_ROOT / "scripts" / "unity_access.ps1"
RESULTS = REPO_ROOT / "results" / "rl-training"
START_FLAG = RESULTS / "start-play.flag"
SMOKE_ONNX = RESULTS / "ship_combat_smoke" / "ShipCombat.onnx"
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


def _ps_literal(value) -> str:
    return "'" + str(value).replace("'", "''") + "'"


def _coordinator_json(proc: subprocess.CompletedProcess) -> dict:
    for line in reversed(proc.stdout.splitlines()):
        line = line.strip()
        if line.startswith("{"):
            return json.loads(line)
    sys.exit(f"FAIL: no JSON from unity-access coordinator (exit {proc.returncode})\n{proc.stdout}\n{proc.stderr}")


def start_editor(lease: str, editor_args, unity: Path, env) -> int:
    """Boot a batch editor through the Unity-access coordinator so it owns the PID from birth."""
    args_literal = ",".join(_ps_literal(a) for a in editor_args)
    inner = (f"& {_ps_literal(COORDINATOR)} -Action StartEditor -Lease {_ps_literal(lease)} "
             f"-Slot main -UnityPath {_ps_literal(unity)} -SkipMcp -WaitSeconds 15 -Json "
             f"-EditorArgs @({args_literal})")
    proc = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                          capture_output=True, text=True, env=env)
    result = _coordinator_json(proc)
    if result.get("status") != "attached":
        sys.exit(f"FAIL: project busy: {result.get('status', 'unknown')} (unity-access coordinator; see skills/unity-access)")
    return int(result["owner"]["processId"])


def release_editor(lease: str, env) -> None:
    inner = (f"& {_ps_literal(COORDINATOR)} -Action Release -Lease {_ps_literal(lease)} "
             f"-Slot main -CloseEditor -Json")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                   capture_output=True, text=True, env=env)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--boot-timeout", type=float, default=1800.0, help="seconds to wait for the editor to arm")
    parser.add_argument("--run-timeout", type=float, default=3600.0, help="seconds to wait for the trainer to finish")
    args = parser.parse_args()

    unity = args.unity or default_unity_exe()
    RESULTS.mkdir(parents=True, exist_ok=True)
    START_FLAG.unlink(missing_ok=True)
    editor_log = RESULTS / "smoke-editor.log"
    editor_log.unlink(missing_ok=True)

    env = dict(os.environ, RL_SMOKE="1")
    lease = "rl-smoke"
    editor_pid = start_editor(
        lease,
        ["-projectPath", str(PROJECT), "-batchmode", "-nographics",
         "-executeMethod", "Game.RLHarness.TrainingBootstrap.EnterTrainingPlayModeWhenSignaled",
         "-logFile", str(editor_log)],
        unity, env)
    print(f"editor pid {editor_pid} (owned by unity-access lease {lease})")
    trainer = None
    try:
        wait_for(lambda: log_contains(editor_log, ARMED_MARKER), "editor to arm", args.boot_timeout)

        trainer_log = RESULTS / "smoke-trainer.log"
        with open(trainer_log, "w") as tl:
            trainer = subprocess.Popen(
                [str(RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"),
                 str(RL_DIR / "ppo_ship_combat_smoke.yaml"), "--force"],
                cwd=RL_DIR, stdout=tl, stderr=subprocess.STDOUT)
        wait_for(lambda: log_contains(trainer_log, "Listening on port"), "trainer to listen", 300.0)

        START_FLAG.touch()
        wait_for(lambda: trainer.poll() is not None, "trainer to finish", args.run_timeout, poll_s=10.0)
        if trainer.returncode != 0:
            sys.exit(f"FAIL: mlagents-learn exited {trainer.returncode} (see {trainer_log})")
    finally:
        if trainer and trainer.poll() is None:
            trainer.kill()
        release_editor(lease, env)

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
        if terminals < 1:
            failures.append("no terminal (EndEpisode) episode occurred")
        if truncations < 1:
            failures.append("no truncation (EpisodeInterrupted) episode occurred")
        print(f"episodes={len(episode_lines)} terminals={terminals} truncations={truncations}")

    if failures:
        sys.exit("FAIL:\n  " + "\n  ".join(failures))
    print(f"PASS: smoke complete; checkpoint at {SMOKE_ONNX}")


if __name__ == "__main__":
    main()
