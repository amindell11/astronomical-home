import json
import time
from pathlib import Path
from typing import Dict

from mlagents.trainers.stats import StatsSummary, StatsWriter

from trainer_runtime.contract import TrainingSummary, read_summaries


class JsonlStatsWriter(StatsWriter):
    def __init__(self, path: Path, behavior: str, max_steps: int, resume: bool):
        self.path = path
        self.behavior = behavior
        self.max_steps = max_steps
        self.started = time.monotonic()
        if resume:
            # Keep elapsedSeconds monotonic across resume legs: consumers divide steps by it.
            previous = read_summaries(path)
            if previous:
                self.started -= previous[-1].elapsed_seconds
        else:
            path.write_text("", encoding="utf-8")

    def write_stats(self, category: str, values: Dict[str, StatsSummary], step: int) -> None:
        if category != self.behavior:
            return
        reward = values.get("Environment/Cumulative Reward")
        summary = TrainingSummary(
            behavior=category,
            step=step,
            elapsed_seconds=time.monotonic() - self.started,
            max_steps=self.max_steps,
            mean_reward=float(reward.mean) if reward else None,
            reward_std_dev=float(reward.std) if reward else None,
            elo=_mean(values.get("Self-play/ELO")),
            is_training=bool(_mean(values.get("Is Training")) or False),
        )
        data = {
            "behavior": summary.behavior,
            "step": summary.step,
            "elapsedSeconds": round(summary.elapsed_seconds, 6),
            "maxSteps": summary.max_steps,
            "meanReward": summary.mean_reward,
            "rewardStdDev": summary.reward_std_dev,
            "elo": summary.elo,
            "isTraining": summary.is_training,
        }
        with self.path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(data, separators=(",", ":")) + "\n")
            handle.flush()

def _mean(summary: StatsSummary):
    return None if summary is None else float(summary.mean)
