"""Boot/release long-lived editors and run self-exiting batch children through the unity-access coordinator
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


def start_editor(lease: str, project: Path, editor_args, unity: Path, env) -> int:
    args_literal = ",".join(_ps_literal(a) for a in ["-projectPath", str(project), *editor_args])
    inner = (f"& {_ps_literal(COORDINATOR)} -Action StartEditor -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -UnityPath {_ps_literal(unity)} -SkipMcp -WaitSeconds 15 -Json "
             f"-EditorArgs @({args_literal})")
    proc = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                          capture_output=True, text=True, env=env)
    result = _coordinator_json(proc)
    if result.get("status") != "attached":
        sys.exit(f"FAIL: project busy: {result.get('status', 'unknown')} (unity-access coordinator; see skills/unity-access)")
    return int(result["owner"]["processId"])


def run_batch(lease: str, project: Path, batch_script: Path, env, batch_args=(), wait_seconds: int = 900) -> int:
    """Run a self-exiting batch child under the coordinator, which holds the boot lane for its whole run.

    The child is opaque to the coordinator, so it must exit on its own (Unity -batchmode with an
    -executeMethod that calls EditorApplication.Exit). Returns the child's exit code.
    """
    args_literal = ",".join(_ps_literal(a) for a in batch_args)
    batch_arguments = f" -BatchArguments @({args_literal})" if args_literal else ""
    inner = (f"& {_ps_literal(COORDINATOR)} -Action RunBatch -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -BatchScript {_ps_literal(batch_script)} "
             f"-WaitSeconds {int(wait_seconds)} -Json{batch_arguments}")
    proc = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                          capture_output=True, text=True, env=env)
    result = _coordinator_json(proc)
    if result.get("status") != "batch_complete":
        sys.exit(f"FAIL: project busy: {result.get('status', 'unknown')} (unity-access coordinator; see skills/unity-access)")
    return int(result["exitCode"])


def release_editor(lease: str, project: Path, env) -> None:
    inner = (f"& {_ps_literal(COORDINATOR)} -Action Release -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -CloseEditor -Json")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                   capture_output=True, text=True, env=env)
