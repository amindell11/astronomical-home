"""Checkpoint watch: the discover -> per-step-dir -> replay-or-run loop behind the eval gate.

Watches a training run's checkpoint exports (ShipCombat-<step>.onnx) and hands each new one
to the caller in step order, paired with its step-named artifact dir and the replicates
already finished inside it (step-<N>/rep-<k>/ each holding one summary — one run dir is one
sample). The caller re-derives its verdict from the replicates present, so a restarted
caller rebuilds its history without spending another eval.
"""
import re
import sys
import time
import logging
from pathlib import Path
from typing import NamedTuple

from eval_lane import summaries_in
from trainer_runtime.contract import checkpoint_manifest_path, read_checkpoint_manifest

CHECKPOINT = re.compile(r"^ShipCombat-(\d+)\.onnx$")
REP = re.compile(r"^rep-(\d+)$")
logger = logging.getLogger(__name__)


class PendingStep(NamedTuple):
    step: int
    checkpoint: Path
    out_dir: Path
    reps: tuple  # ((rep_index, summary_path), ...) ascending


def rep_dir(step_dir: Path, rep: int) -> Path:
    return step_dir / f"rep-{rep}"


def discover_checkpoints(behavior_dir: Path) -> list:
    run_dir = behavior_dir.parent
    manifest = checkpoint_manifest_path(run_dir)
    if manifest.exists():
        checkpoints = []
        for entry in read_checkpoint_manifest(manifest):
            onnx = run_dir / entry.onnx
            if onnx.exists():
                checkpoints.append((entry.step, onnx))
            else:
                logger.warning("Skipping pruned checkpoint step %s: %s", entry.step, onnx)
        return checkpoints
    matches = ((CHECKPOINT.match(p.name), p) for p in behavior_dir.glob("ShipCombat-*.onnx"))
    return sorted((int(m.group(1)), p) for m, p in matches if m)


def discover_reps(step_dir: Path) -> tuple:
    """Finished replicates in a step dir, ascending; a rep dir without its summary is
    unfinished and simply re-runs. A summary at the step root is a pre-replicate gate
    layout that cannot be pooled, and a rep dir with several summaries breaks
    one-run-dir-one-sample — both refuse loudly rather than misread evidence."""
    if not step_dir.exists():
        return ()
    if summaries_in(step_dir):
        sys.exit(f"FAIL: {step_dir} holds a summary at the step root (pre-replicate gate layout); "
                 f"restart the gate into a fresh --out-root")
    reps = []
    for child in step_dir.iterdir():
        match = REP.match(child.name)
        if not match or not child.is_dir():
            continue
        summaries = summaries_in(child)
        if len(summaries) > 1:
            sys.exit(f"FAIL: {child} holds {len(summaries)} summaries; one run dir is one sample "
                     f"— remove the strays")
        if summaries:
            reps.append((int(match.group(1)), summaries[0]))
    return tuple(sorted(reps))


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
            yield PendingStep(step, checkpoint, out_dir, discover_reps(out_dir))
        if once:
            return
        time.sleep(poll_seconds)
