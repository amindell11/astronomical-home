"""Render training-progress charts from a run's TensorBoard event files to a PNG.

Usage: python plot_progress.py --run-dir ../../results/rl-training/<run_id>/ShipCombat [--out chart.png]
"""
import argparse
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

PANELS = [
    ("Environment/Cumulative Reward", "Cumulative reward"),
    ("Environment/Episode Length", "Episode length"),
    ("Losses/Policy Loss", "Policy loss"),
    ("Losses/Value Loss", "Value loss"),
    ("Policy/Extrinsic Value Estimate", "Value estimate"),
    ("Policy/Entropy", "Entropy"),
]

# The curriculum's lesson-advance thresholds, as a progress reference on the reward panel.
REWARD_THRESHOLDS = (0.3, 0.45, 0.6, 0.75, 0.9, 1.05)


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", required=True, type=Path,
                        help="behavior directory holding the event files, e.g. results/rl-training/<run_id>/ShipCombat")
    parser.add_argument("--out", type=Path, default=Path("training_progress.png"))
    args = parser.parse_args()
    if not args.run_dir.is_dir():
        parser.error(f"--run-dir does not exist: {args.run_dir}")
    return args


def run_label(run_dir: Path) -> str:
    """The event files sit under <run_id>/<behavior>, so the run's identity is the parent."""
    return run_dir.parent.name or run_dir.name


def trailing_mean(vals, window_count=20):
    """Window scales with the series so the overlay reads the same at 200k steps and at 3M."""
    k = max(len(vals) // window_count, 2)
    return [sum(w) / len(w) for w in (vals[max(0, i - k):i + 1] for i in range(len(vals)))]


def main():
    args = parse_args()

    acc = EventAccumulator(str(args.run_dir), size_guidance={"scalars": 0})
    acc.Reload()
    tags = set(acc.Tags()["scalars"])
    lesson_tags = sorted(t for t in tags if "Lesson" in t)

    fig, axes = plt.subplots(2, 4, figsize=(20, 8))
    axes = axes.flatten()

    last_step = 0
    for ax, (tag, title) in zip(axes, PANELS):
        if tag in tags:
            events = acc.Scalars(tag)
            steps = [e.step for e in events]
            vals = [e.value for e in events]
            last_step = max(last_step, steps[-1] if steps else 0)
            ax.plot(steps, vals, linewidth=1.2)
            if len(vals) >= 20:
                ax.plot(steps, trailing_mean(vals), linewidth=2.0)
        else:
            ax.text(0.5, 0.5, "no data yet", ha="center", va="center", transform=ax.transAxes)
        ax.set_title(title)
        ax.grid(alpha=0.3)

    lesson_ax = axes[len(PANELS)]
    for tag in lesson_tags:
        events = acc.Scalars(tag)
        lesson_ax.step([e.step for e in events], [e.value for e in events], where="post",
                       label=tag.split("/")[-1])
    lesson_ax.set_title("Curriculum lesson index")
    lesson_ax.grid(alpha=0.3)
    if lesson_tags:
        lesson_ax.legend(fontsize=7)

    if "Environment/Cumulative Reward" in tags:
        for threshold in REWARD_THRESHOLDS:
            axes[0].axhline(threshold, color="gray", linewidth=0.5, linestyle="--", alpha=0.6)

    for spare in axes[len(PANELS) + 1:]:
        spare.axis("off")

    fig.suptitle(f"{run_label(args.run_dir)} — step {last_step:,}", fontsize=14)
    fig.tight_layout(rect=(0, 0, 1, 0.96))
    fig.savefig(args.out, dpi=110)
    print(f"wrote {args.out} at step {last_step}")


if __name__ == "__main__":
    main()
