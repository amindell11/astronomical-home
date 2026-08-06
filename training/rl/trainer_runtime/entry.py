import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

from mlagents.trainers import learn
from mlagents.trainers.cli_utils import StoreConfigFile

from trainer_runtime.contract import MANIFEST_NAME, RunManifest, config_sha256, summaries_path, write_manifest
from trainer_runtime.stats_writer import JsonlStatsWriter


def main(argv: Sequence[str] | None = None) -> None:
    args = list(sys.argv[1:] if argv is None else argv)
    refuse_conflicting_restore(args)
    started_at = datetime.now(timezone.utc)
    options = learn.parse_command_line(args)
    config_path = Path(StoreConfigFile.trainer_config_path).resolve()
    stock_register = learn.register_stats_writer_plugins

    def register_stats_writers(run_options):
        return stock_register(run_options) + owned_stats_writers(run_options, config_path, started_at)

    learn.register_stats_writer_plugins = register_stats_writers
    try:
        learn.run_cli(options)
    finally:
        learn.register_stats_writer_plugins = stock_register


def refuse_conflicting_restore(argv: Sequence[str]) -> None:
    runtime_args = list(argv[:argv.index("--env-args")]) if "--env-args" in argv else list(argv)
    if "--resume" in runtime_args and "--initialize-from" in runtime_args:
        raise SystemExit("FAIL: --resume and --initialize-from are mutually exclusive")


def owned_stats_writers(options, config_path: Path, started_at: datetime) -> list[JsonlStatsWriter]:
    behavior, trainer_settings = _single_behavior(options.behaviors)
    checkpoint = options.checkpoint_settings
    results_dir = Path(checkpoint.results_dir).resolve()
    manifest = RunManifest(
        run_id=checkpoint.run_id,
        behavior=behavior,
        results_dir=results_dir,
        started_at=started_at,
        max_steps=trainer_settings.max_steps,
        mode=_mode(trainer_settings.self_play is not None),
        config_hash=config_sha256(config_path),
    )
    write_manifest(manifest.run_dir / MANIFEST_NAME, manifest)
    return [JsonlStatsWriter(
        summaries_path(manifest.run_dir), behavior, manifest.max_steps, checkpoint.resume
    )]


def _single_behavior(behaviors):
    items = list(behaviors.items())
    if len(items) != 1:
        raise RuntimeError(f"owned trainer entry requires exactly one behavior; found {len(items)}")
    return items[0]


def _mode(self_play: bool) -> str:
    if os.environ.get("RL_HYBRID_SCRIPTED_WORKERS") is not None:
        return "hybrid"
    return "self-play" if self_play else "scripted"


if __name__ == "__main__":
    main()
