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


def start_editor(lease: str, project: Path, editor_args, unity: Path, env, editor_profile: str = "LowMemory") -> int:
    # -projectPath is the coordinator's to inject; it composes these args after it.
    args_literal = ",".join(_ps_literal(a) for a in editor_args)
    inner = (f"& {_ps_literal(COORDINATOR)} -Action StartEditor -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -UnityPath {_ps_literal(unity)} -WaitSeconds 15 "
             f"-EditorProfile {_ps_literal(editor_profile)} -Json "
             f"-EditorArgs @({args_literal})")
    proc = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                          capture_output=True, text=True, env=env)
    result = _coordinator_json(proc)
    if result.get("status") != "attached":
        if result.get("status") == "editor_profile_failed":
            profile = result.get("profile", {})
            sys.exit("FAIL: editor profile verification failed: " + profile.get("note", "unknown failure"))
        sys.exit(f"FAIL: project busy: {result.get('status', 'unknown')} (unity-access coordinator; see skills/unity-access)")
    return int(result["owner"]["processId"])


def run_batch(lease: str, project: Path, batch_script: Path, env, batch_args=(), wait_seconds: int = 900,
              log_path: Path = None) -> int:
    """Run a self-exiting batch child under the coordinator, which renews the owner lease for the
    child's whole run and holds the machine-wide boot lane only across its startup.

    The child is opaque to the coordinator, so it must exit on its own (Unity -batchmode with an
    -executeMethod that calls EditorApplication.Exit). log_path is the child's Unity -logFile: the
    coordinator watches it to know when startup is past the contention window; without it the lane
    falls back to a timed release. Returns the child's exit code.
    """
    args_literal = ",".join(_ps_literal(a) for a in batch_args)
    batch_arguments = f" -BatchArguments @({args_literal})" if args_literal else ""
    batch_log = f" -BatchLogPath {_ps_literal(log_path)}" if log_path else ""
    inner = (f"& {_ps_literal(COORDINATOR)} -Action RunBatch -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -BatchScript {_ps_literal(batch_script)} "
             f"-WaitSeconds {int(wait_seconds)} -Json{batch_arguments}{batch_log}")
    proc = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                          capture_output=True, text=True, env=env)
    result = _coordinator_json(proc)
    if result.get("status") != "batch_complete":
        sys.exit(f"FAIL: batch did not run: {result.get('status', 'unknown')} "
                 f"(unity-access coordinator; see skills/unity-access)")
    return int(result["exitCode"])


def release_editor(lease: str, project: Path, env) -> None:
    inner = (f"& {_ps_literal(COORDINATOR)} -Action Release -Lease {_ps_literal(lease)} "
             f"-ProjectPath {_ps_literal(project)} -CloseEditor -Json")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", inner],
                   capture_output=True, text=True, env=env)
