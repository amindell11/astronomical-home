"""Eval lane launcher: composes the lane's environment, runs the batch child through the
unity-access coordinator, and reads the summary back from the dir it named.

The child env is composed from explicit parameters only: every inherited RL_HARNESS_* AND
retired RL_EVAL_* variable is stripped first, so an inherited override can never move a run
off what the caller asked for. Values pass through as strings — the C# SessionSpec parser is
the single grammar authority and throws at boot, so a bad value fails loud on first run.

As a CLI this is the manual scripted eval at the gate shape (it replaced rl_eval.ps1):

    cd training/rl
    .venv\\Scripts\\python eval_lane.py --onnx <ckpt.onnx> --seeds 2001,2002,2003,2004,2005

--exec player moves the sim off the shared editors: a leased editor convert step builds the
session's model bundle, then the dedicated headless eval player (no unity-access lease —
run_parallel.py precedent) runs the same protocol. Player scores are an uncalibrated
executionMode until bundle v2; the editor stays the verdict-bearing reference.
"""
import argparse
import os
import subprocess
import sys
import time
from pathlib import Path

from driver_common import default_unity_exe
from unity_access import run_batch

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
HARNESS_CHILD = RL_DIR / "harness_child.ps1"
CONVERT_CHILD = RL_DIR / "convert_child.ps1"
PLAYER_EXE = REPO_ROOT / "build" / "rl-harness" / "RLHarnessEval.exe"
BUNDLE_NAME = "eval-models.bundle"
# Staging discipline: manual artifacts land in the primary tree, never slot-only.
MANUAL_ROOT = Path(r"D:\amind\git\astronomical-home") / "results" / "rl-eval" / "manual"
SMOKE_FIXTURE_STEM = "ShipCombat-smoke"


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
                  onnx=None, seeds=None, episodes_per_seed=None, density=None,
                  opponent=None, probes=None, open_loop=None, lease_wait: int = 1800) -> Path:
    """One eval-lane session through the coordinator; returns the summary path read back from out_dir.

    Every parameter besides the plumbing maps 1:1 onto an RL_HARNESS_* variable; None means
    "leave unset" and takes the SessionSpec default (no onnx = the committed smoke fixture).
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    log = out_dir / "editor.log"
    # A retried session's leftover log still carries a boot marker, which would free the lane at once.
    log.unlink(missing_ok=True)
    env = compose_child_env(unity, project, log, {
        "RL_HARNESS_ONNX": onnx,
        "RL_HARNESS_SEEDS": seeds,
        "RL_HARNESS_EPISODES_PER_SEED": episodes_per_seed,
        "RL_HARNESS_DENSITY": density,
        "RL_HARNESS_OPPONENT": opponent,
        "RL_HARNESS_PROBES": probes,
        "RL_HARNESS_OPENLOOP": open_loop,
        "RL_HARNESS_OUT_DIR": out_dir,
    })
    code = run_batch(lease, project, HARNESS_CHILD, env, wait_seconds=lease_wait, log_path=log)
    if code != 0:
        subject = Path(onnx).name if onnx else "the smoke fixture"
        sys.exit(f"FAIL: eval of {subject} exited {code} (see {log})")
    return summary_in(out_dir)


def run_convert_step(*, project: Path, unity: Path, lease: str, out_dir: Path,
                     onnx, opponent=None, lease_wait: int = 1800) -> Path:
    """Run the editor-only ONNX conversion under a Unity lease."""
    out_dir.mkdir(parents=True, exist_ok=True)
    log = out_dir / "convert.log"
    bundle = (out_dir / BUNDLE_NAME).resolve()
    log.unlink(missing_ok=True)
    env = compose_child_env(unity, project, log, {
        "RL_HARNESS_ONNX": onnx,
        "RL_HARNESS_OPPONENT": opponent,
        "RL_HARNESS_BUNDLE": bundle,
    })
    code = run_batch(lease, project, CONVERT_CHILD, env, wait_seconds=lease_wait, log_path=log)
    if code != 0:
        sys.exit(f"FAIL: model-bundle convert of {Path(onnx).name} exited {code} (see {log})")
    return bundle


def run_player_eval(*, exe: Path, bundle: Path, out_dir: Path, onnx, seeds=None,
                    episodes_per_seed=None, density=None, opponent=None, probes=None) -> Path:
    """Run the player simulation without a Unity-access lease."""
    log = out_dir / "player.log"
    env = stripped_parent_env()
    env.update({name: str(value) for name, value in {
        "RL_HARNESS_BUNDLE": bundle,
        "RL_HARNESS_ONNX": onnx,
        "RL_HARNESS_SEEDS": seeds,
        "RL_HARNESS_EPISODES_PER_SEED": episodes_per_seed,
        "RL_HARNESS_DENSITY": density,
        "RL_HARNESS_OPPONENT": opponent,
        "RL_HARNESS_PROBES": probes,
        "RL_HARNESS_OUT_DIR": out_dir,
    }.items() if value is not None})
    code = subprocess.run([str(exe), "-batchmode", "-nographics", "-logFile", str(log)],
                          env=env).returncode
    if code != 0:
        sys.exit(f"FAIL: player eval of {Path(onnx).name} exited {code} (see {log})")
    return summary_in(out_dir)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--onnx", type=Path, default=None,
                        help="checkpoint .onnx to evaluate (default: the committed smoke fixture)")
    parser.add_argument("--seeds", required=True,
                        help="RL_HARNESS_SEEDS selection (keep clear of the sealed held-out 1001-1020)")
    parser.add_argument("--episodes-per-seed", default=None, help="RL_HARNESS_EPISODES_PER_SEED")
    parser.add_argument("--density", default=None, help="RL_HARNESS_DENSITY (omit for the canonical eval env)")
    parser.add_argument("--opponent", default=None, help="RL_HARNESS_OPPONENT (omit for the roster)")
    parser.add_argument("--probes", default=None, help="RL_HARNESS_PROBES (omit for the default probe set)")
    parser.add_argument("--open-loop", default=None,
                        help="RL_HARNESS_OPENLOOP: run the K1-2 velrebase lane on this archetype (or \"all\") "
                             "instead of a checkpoint eval")
    parser.add_argument("--exec", dest="exec_mode", choices=("editor", "player"), default="editor",
                        help="editor: the calibrated reference protocol, sim in the leased batch child; "
                             "player: leased convert step, then the sim in the dedicated headless exe "
                             "(uncalibrated executionMode until bundle v2)")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the eval boots in (point at a free pool slot, not the tree you work in)")
    parser.add_argument("--unity", type=Path, default=None,
                        help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--out-root", type=Path, default=MANUAL_ROOT,
                        help="artifact root; each run gets its own <ckpt-stem>-<timestamp> dir")
    parser.add_argument("--lease", default="rl-eval-manual", help="unity-access lease name")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane")
    args = parser.parse_args()
    if not args.project.exists():
        parser.error(f"--project {args.project} does not exist")
    if args.onnx is not None and not args.onnx.exists():
        parser.error(f"--onnx {args.onnx} does not exist")
    if not HARNESS_CHILD.exists():
        sys.exit(f"FAIL: batch child missing at {HARNESS_CHILD}")
    if args.exec_mode == "player":
        if args.onnx is None:
            parser.error("--exec player requires an explicit --onnx (a player has no smoke default)")
        if args.open_loop:
            parser.error("--open-loop is editor-only; the open-loop lane has no player")
        # Freshness is the operator's (run_parallel.py precedent) — no staleness oracle here.
        if not PLAYER_EXE.exists():
            sys.exit(f"FAIL: eval player exe missing at {PLAYER_EXE}; build it first "
                     "(-executeMethod Game.RLHarness.RLEvalPlayerBuild.Build via unity_access RunBatch)")

    unity = args.unity or default_unity_exe(args.project)
    if args.open_loop:
        stem = f"velrebase-{args.open_loop.lower()}"
    else:
        stem = args.onnx.stem if args.onnx else SMOKE_FIXTURE_STEM
    out_dir = args.out_root / f"{stem}-{time.strftime('%Y%m%d-%H%M%S')}"
    print(f"[eval-lane] exec {args.exec_mode}  project {args.project}  seeds {args.seeds}  artifacts {out_dir}")
    if args.exec_mode == "player":
        bundle = run_convert_step(project=args.project, unity=unity, lease=args.lease, out_dir=out_dir,
                                  onnx=args.onnx, opponent=args.opponent, lease_wait=args.lease_wait)
        summary = run_player_eval(exe=PLAYER_EXE, bundle=bundle, out_dir=out_dir, onnx=args.onnx,
                                  seeds=args.seeds, episodes_per_seed=args.episodes_per_seed,
                                  density=args.density, opponent=args.opponent, probes=args.probes)
    else:
        summary = run_eval_lane(project=args.project, unity=unity, lease=args.lease, out_dir=out_dir,
                                onnx=args.onnx, seeds=args.seeds, episodes_per_seed=args.episodes_per_seed,
                                density=args.density, opponent=args.opponent, probes=args.probes,
                                open_loop=args.open_loop, lease_wait=args.lease_wait)
    print(f"[eval-lane] summary {summary}")


if __name__ == "__main__":
    main()
