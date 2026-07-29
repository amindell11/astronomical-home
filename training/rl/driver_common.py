"""Shared plumbing for the training/eval drivers: Unity discovery, log-marker polling, and
trainer-config reads. The markers and the episode-line shape are Unity-side formats
(TrainingBootstrap, PacingContract, TrainingHost), so one copy keeps a C#-side rename from
rotting four drivers independently.
"""
import re
import sys
import time
from pathlib import Path

ARMED_MARKER = "[TrainingBootstrap] armed"
PACING_MARKER = "[PacingContract] holds"
EPISODE_LINE = re.compile(
    r"\[TrainingHost\] arena (\d+) episode \d+:.*terminals=(\d+) truncations=(\d+)")


def default_unity_exe(project: Path) -> Path:
    version = re.search(r"m_EditorVersion: (\S+)",
                        (project / "ProjectSettings" / "ProjectVersion.txt").read_text()).group(1)
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


def episode_rows(log_text: str) -> list:
    """(arena, terminals, truncations) per TrainingHost episode line, in log order."""
    return [(int(a), int(t), int(u)) for a, t, u in EPISODE_LINE.findall(log_text)]


def config_run_id(config: Path) -> str:
    match = re.search(r"^\s*run_id:\s*(\S+)", config.read_text(), re.MULTILINE)
    if not match:
        sys.exit(f"FAIL: no checkpoint_settings run_id in {config}; pass --run-id")
    return match.group(1)


def config_has_self_play(config: Path) -> bool:
    return re.search(r"^\s*self_play:", config.read_text(), re.MULTILINE) is not None
