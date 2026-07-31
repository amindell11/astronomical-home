"""Checkpoint watch: the discover -> per-step-dir -> replay-or-run loop behind the eval gate.

Watches a training run's checkpoint exports (ShipCombat-<step>.onnx) and hands each new one
to the caller in step order, paired with its own step-named artifact dir. A step dir that
already holds its one summary is offered for replay instead of being re-run, so a restarted
caller rebuilds its history without spending another eval; an ambiguous dir (two summaries)
never replays.
"""
import re
import time
from pathlib import Path
from typing import NamedTuple, Optional

from eval_lane import summaries_in

CHECKPOINT = re.compile(r"^ShipCombat-(\d+)\.onnx$")


class PendingStep(NamedTuple):
    step: int
    checkpoint: Path
    out_dir: Path
    replay: Optional[Path]


def discover_checkpoints(behavior_dir: Path) -> list:
    matches = ((CHECKPOINT.match(p.name), p) for p in behavior_dir.glob("ShipCombat-*.onnx"))
    return sorted((int(m.group(1)), p) for m, p in matches if m)


def evaluated_summary(out_dir: Path):
    """A step already evaluated replays instead of re-running: a restarted gate rebuilds its streak
    history in step order and never writes a second summary into a step dir."""
    summaries = summaries_in(out_dir)
    return summaries[0] if len(summaries) == 1 else None


def watch_checkpoints(behavior_dir: Path, out_root: Path, *, from_step: int = 0,
                      poll_seconds: float = 120.0, once: bool = False):
    """Yields a PendingStep per new checkpoint in step order; polls forever unless once."""
    done = set()
    while True:
        pending = [(s, p) for s, p in discover_checkpoints(behavior_dir)
                   if s > from_step and s not in done]
        for step, checkpoint in pending:
            out_dir = out_root / f"step-{step}"
            done.add(step)
            yield PendingStep(step, checkpoint, out_dir, evaluated_summary(out_dir))
        if once:
            return
        time.sleep(poll_seconds)
