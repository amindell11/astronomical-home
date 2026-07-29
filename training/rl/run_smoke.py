"""Asserting trainer-connected smoke: boots a batch-mode editor through the unity-access
coordinator, runs mlagents-learn against the smoke config, and FAILS unless the run completed,
a checkpoint was exported, the pacing-contract marker was logged, and both a terminal
(EndEpisode) and a truncation (EpisodeInterrupted) occurred.

--self-play swaps in the self-play smoke composition (RL_SELFPLAY=1, the self-play smoke YAML):
the opponent ship is a second team-1 ShipCombat agent, proving native mlagents self_play trains
two team_ids under automatic Academy stepping. --num-arenas M additionally asserts that every
in-process arena completed episodes.

Run from training/rl with the venv set up (README.md).
"""
import argparse
import os
import subprocess
import sys
from pathlib import Path

from driver_common import (ARMED_MARKER, PACING_MARKER, default_unity_exe, episode_rows,
                           log_contains, wait_for)
from unity_access import release_editor, start_editor

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
RESULTS = REPO_ROOT / "results" / "rl-training"
START_FLAG = RESULTS / "start-play.flag"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--self-play", action="store_true",
                        help="RL_SELFPLAY=1 mirror composition against the self-play smoke YAML")
    parser.add_argument("--num-arenas", type=int, default=1,
                        help="in-process arenas (--harness-num-arenas to the editor)")
    parser.add_argument("--unity", type=Path, default=None, help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--boot-timeout", type=float, default=1800.0, help="seconds to wait for the editor to arm")
    parser.add_argument("--run-timeout", type=float, default=3600.0, help="seconds to wait for the trainer to finish")
    args = parser.parse_args()
    if args.num_arenas < 1:
        parser.error("--num-arenas must be >= 1")

    kind = "selfplay-smoke" if args.self_play else "smoke"
    config = RL_DIR / ("ppo_ship_combat_selfplay_smoke.yaml" if args.self_play
                       else "ppo_ship_combat_smoke.yaml")
    onnx = RESULTS / ("ship_combat_selfplay_smoke" if args.self_play
                      else "ship_combat_smoke") / "ShipCombat.onnx"
    unity = args.unity or default_unity_exe(PROJECT)
    RESULTS.mkdir(parents=True, exist_ok=True)
    START_FLAG.unlink(missing_ok=True)
    editor_log = RESULTS / f"{kind}-editor.log"
    editor_log.unlink(missing_ok=True)

    # An inherited RL_SELFPLAY would compose a mirror league behind a scripted-roster config.
    env = dict(os.environ, RL_SMOKE="1")
    if args.self_play:
        env["RL_SELFPLAY"] = "1"
    else:
        env.pop("RL_SELFPLAY", None)
    lease = f"rl-{kind}"
    editor_pid = start_editor(
        lease, PROJECT,
        ["-batchmode", "-nographics",
         "-executeMethod", "Game.RLHarness.TrainingBootstrap.EnterTrainingPlayModeWhenSignaled",
         "-logFile", str(editor_log),
         "--harness-num-arenas", str(args.num_arenas)],
        unity, env)
    print(f"editor pid {editor_pid} (owned by unity-access lease {lease})")
    trainer = None
    try:
        wait_for(lambda: log_contains(editor_log, ARMED_MARKER), "editor to arm", args.boot_timeout)

        trainer_log = RESULTS / f"{kind}-trainer.log"
        with open(trainer_log, "w") as tl:
            trainer = subprocess.Popen(
                [str(RL_DIR / ".venv" / "Scripts" / "mlagents-learn.exe"), str(config), "--force"],
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
    if not onnx.exists():
        failures.append(f"no checkpoint exported at {onnx}")
    log_text = editor_log.read_text(errors="replace")
    if PACING_MARKER not in log_text:
        failures.append(f"pacing-contract marker '{PACING_MARKER}' missing from {editor_log}")
    rows = episode_rows(log_text)
    if not rows:
        failures.append(f"no [TrainingHost] episode lines in {editor_log}")
    else:
        _, terminals, truncations = rows[-1]
        if terminals < 1:
            failures.append("no terminal (EndEpisode) episode occurred")
        if truncations < 1:
            failures.append("no truncation (EpisodeInterrupted) episode occurred")
        arenas_seen = {arena for arena, _, _ in rows}
        missing = set(range(args.num_arenas)) - arenas_seen
        if missing:
            failures.append(f"arenas {sorted(missing)} completed no episodes (saw {sorted(arenas_seen)})")
        print(f"episodes={len(rows)} terminals={terminals} truncations={truncations}")

    if failures:
        sys.exit("FAIL:\n  " + "\n  ".join(failures))
    print(f"PASS: {kind} complete; checkpoint at {onnx}")


if __name__ == "__main__":
    main()
