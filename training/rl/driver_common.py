"""Finds the Unity exe the harness lane launches."""
import re
import sys
from pathlib import Path


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
