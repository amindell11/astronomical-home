"""Boot/release batch editors through the unity-access coordinator
(scripts/unity_access.ps1) so every RL driver's editor PID is owner-tracked from
birth (skills/unity-access). The coordinator protocol — PowerShell quoting, JSON
handshake, lease semantics — lives here and nowhere else.
"""
import json
import subprocess
import sys
from pathlib import Path

COORDINATOR = Path(__file__).resolve().parent.parent.parent / "scripts" / "unity_access.ps1"


def _ps_literal(value) -> str:
    return "'" + str(value).replace("'", "''") + "'"


def _coordinator_json(proc: subprocess.CompletedProcess) -> dict:
    for line in reversed(proc.stdout.splitlines()):
        line = line.strip()
        if line.startswith("{"):
            return json.loads(line)
    sys.exit(f"FAIL: no JSON from unity-access coordinator (exit {proc.returncode})\n{proc.stdout}\n{proc.stderr}")


def start_editor(lease: str, editor_args, unity: Path, env) -> int:
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
