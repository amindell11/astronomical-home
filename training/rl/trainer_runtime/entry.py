import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

from mlagents.trainers import learn
from mlagents.trainers.cli_utils import StoreConfigFile

from trainer_runtime.run_loop import _mode, owned_stats_writers, run_cli, validate_owned_options


def main(argv: Sequence[str] | None = None) -> None:
    args = list(sys.argv[1:] if argv is None else argv)
    refuse_conflicting_restore(args)
    started_at = datetime.now(timezone.utc)
    options = learn.parse_command_line(args)
    validate_owned_options(options)
    config_path = Path(StoreConfigFile.trainer_config_path).resolve()
    run_cli(options, config_path, started_at)


def refuse_conflicting_restore(argv: Sequence[str]) -> None:
    runtime_args = list(argv[:argv.index("--env-args")]) if "--env-args" in argv else list(argv)
    if "--resume" in runtime_args and "--initialize-from" in runtime_args:
        raise SystemExit("FAIL: --resume and --initialize-from are mutually exclusive")


if __name__ == "__main__":
    main()
