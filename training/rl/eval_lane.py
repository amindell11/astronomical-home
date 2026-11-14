"""Matrix lane launcher: composes the lane's environment, runs the batch child through the
unity-access coordinator, and reads the summary back from the dir it named.

The child env is composed from explicit parameters only: every inherited RL_HARNESS_* AND
retired RL_EVAL_* variable is stripped first, so an inherited override can never move a run
off what the caller asked for. Values pass through as strings — the C# HarnessSpec parser is
the single grammar authority and throws at boot, so a bad value fails loud on first run.

    cd training/rl
    .venv\\Scripts\\python eval_lane.py --matrix Lasers,Missiles --seeds 3001,3002
"""
import argparse
import os
import sys
from datetime import datetime
from pathlib import Path

from driver_common import default_unity_exe
from unity_access import run_batch

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
HARNESS_CHILD = RL_DIR / "harness_child.ps1"
# Staging discipline: manual artifacts land in the primary tree, never slot-only.
MANUAL_ROOT = Path(r"D:\amind\git\astronomical-home") / "results" / "rl-eval" / "manual"


def summaries_in(out_dir: Path) -> list:
    """The one summary-glob: every *-summary.json in a caller-named session dir."""
    return sorted(out_dir.glob("*-summary.json"))


def summary_in(out_dir: Path) -> Path:
    """The caller named out_dir, so exactly one summary belongs to it; anything else is a broken contract."""
    summaries = summaries_in(out_dir)
    if len(summaries) != 1:
        sys.exit(f"FAIL: expected exactly one *-summary.json in the caller-named {out_dir}, found {len(summaries)}")
    return summaries[0]


def stripped_parent_env() -> dict:
    return {k: v for k, v in os.environ.items()
            if not k.startswith("RL_HARNESS_") and not k.startswith("RL_EVAL_")}


def compose_child_env(unity: Path, project: Path, log: Path, values: dict) -> dict:
    env = stripped_parent_env()
    env.update(HARNESS_UNITY=str(unity), HARNESS_PROJ=str(project), HARNESS_LOG=str(log))
    env.update({name: str(value) for name, value in values.items() if value is not None})
    return env


def run_eval_lane(*, project: Path, unity: Path, lease: str, out_dir: Path,
                  seeds=None, episodes_per_seed=None, probes=None, matrix=None,
                  matrix_reference=None, matrix_roster=None, matrix_kits=None,
                  lease_wait: int = 1800) -> Path:
    """One matrix-lane session through the coordinator; returns the summary path read back from out_dir.

    Every parameter besides the plumbing maps 1:1 onto an RL_HARNESS_* variable; None means
    "leave unset" and takes the HarnessSpec default.
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    log = out_dir / "editor.log"
    # A retried session's leftover log still carries a boot marker, which would free the lane at once.
    log.unlink(missing_ok=True)
    env = compose_child_env(unity, project, log, {
        "RL_HARNESS_SEEDS": seeds,
        "RL_HARNESS_EPISODES_PER_SEED": episodes_per_seed,
        "RL_HARNESS_PROBES": probes,
        "RL_HARNESS_MATRIX": matrix,
        "RL_HARNESS_MATRIX_REFERENCE": matrix_reference,
        "RL_HARNESS_MATRIX_ROSTER": matrix_roster,
        "RL_HARNESS_MATRIX_KITS": matrix_kits,
        "RL_HARNESS_OUT_DIR": out_dir,
    })
    code = run_batch(lease, project, HARNESS_CHILD, env, wait_seconds=lease_wait, log_path=log)
    if code != 0:
        sys.exit(f"FAIL: matrix lane exited {code} (see {log})")
    return summary_in(out_dir)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--seeds", required=True,
                        help="RL_HARNESS_SEEDS selection (keep clear of the sealed held-out 1001-1020)")
    parser.add_argument("--episodes-per-seed", default=None, help="RL_HARNESS_EPISODES_PER_SEED")
    parser.add_argument("--probes", default=None, help="RL_HARNESS_PROBES (omit for the matrix probe)")
    parser.add_argument("--matrix", default=None,
                        help="RL_HARNESS_MATRIX: run the matrix lane on these weapons (\"all\" for every weapon the item "
                             "catalog lists, or comma-separated catalog weapon prefab names; every unordered pair, "
                             "a weapon against itself included); the matrix table lands beside the summary")
    parser.add_argument("--matrix-reference", default=None,
                        help="RL_HARNESS_MATRIX_REFERENCE: the fixed side of a roster matrix, one "
                             "Chassis:Pilot:Weapon[:Shield] token. Requires --matrix-roster.")
    parser.add_argument("--matrix-roster", default=None,
                        help="RL_HARNESS_MATRIX_ROSTER: comma-separated Chassis:Pilot:Weapon[:Shield] tokens. "
                             "The reference fights each one. Requires --matrix-reference.")
    parser.add_argument("--matrix-kits", default=None,
                        help="RL_HARNESS_MATRIX_KITS: off flies the roster with keystone kits never activated. "
                             "Requires --matrix-roster.")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the lane boots in (point at a free pool slot, not the tree you work in)")
    parser.add_argument("--unity", type=Path, default=None,
                        help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--out-root", type=Path, default=MANUAL_ROOT,
                        help="artifact root; each run gets its own <selection>-<timestamp> dir")
    parser.add_argument("--lease", default="rl-eval-manual", help="unity-access lease name")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane")
    args = parser.parse_args()
    if not args.project.exists():
        parser.error(f"--project {args.project} does not exist")
    if not HARNESS_CHILD.exists():
        sys.exit(f"FAIL: batch child missing at {HARNESS_CHILD}")
    if args.matrix_kits and not args.matrix_roster:
        parser.error("--matrix-kits requires --matrix-roster")

    if args.matrix:
        stem = f"matrix-{args.matrix.lower().replace(',', '+')}"
    elif args.matrix_reference or args.matrix_roster:
        reference = (args.matrix_reference or "").lower().replace(":", "-").replace(",", "-")
        stem = f"matrix-roster-{reference}"
    else:
        parser.error("select a matrix: --matrix, or --matrix-reference with --matrix-roster")

    unity = args.unity or default_unity_exe(args.project)
    # Microseconds: two manual launches in the same second must not share a dir.
    out_dir = args.out_root / f"{stem}-{datetime.now().strftime('%Y%m%d-%H%M%S-%f')}"
    print(f"[matrix-lane] project {args.project}  seeds {args.seeds}  artifacts {out_dir}")
    summary = run_eval_lane(project=args.project, unity=unity, lease=args.lease, out_dir=out_dir,
                            seeds=args.seeds, episodes_per_seed=args.episodes_per_seed, probes=args.probes,
                            matrix=args.matrix, matrix_reference=args.matrix_reference,
                            matrix_roster=args.matrix_roster, matrix_kits=args.matrix_kits,
                            lease_wait=args.lease_wait)
    print(f"[matrix-lane] summary {summary}")


if __name__ == "__main__":
    main()
