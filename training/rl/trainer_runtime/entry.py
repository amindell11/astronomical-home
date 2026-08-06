import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

from mlagents.trainers import learn
from mlagents.trainers.cli_utils import StoreConfigFile

from trainer_runtime.microbatch import MicrobatchSettings
from trainer_runtime.run_loop import _mode, owned_stats_writers, run_cli, validate_owned_options

MICROBATCH_WORKER_CAP = "--microbatch-worker-cap"


def main(argv: Sequence[str] | None = None) -> None:
    args = list(sys.argv[1:] if argv is None else argv)
    refuse_conflicting_restore(args)
    args, requested_worker_cap = extract_microbatch_worker_cap(args)
    started_at = datetime.now(timezone.utc)
    options = learn.parse_command_line(args)
    validate_owned_options(options)
    config_path = Path(StoreConfigFile.trainer_config_path).resolve()
    settings = MicrobatchSettings.create(
        requested_worker_cap, options.env_settings.num_envs
    )
    run_cli(options, config_path, started_at, settings)


def refuse_conflicting_restore(argv: Sequence[str]) -> None:
    runtime_args = list(argv[:argv.index("--env-args")]) if "--env-args" in argv else list(argv)
    if "--resume" in runtime_args and "--initialize-from" in runtime_args:
        raise SystemExit("FAIL: --resume and --initialize-from are mutually exclusive")


def extract_microbatch_worker_cap(argv: Sequence[str]) -> tuple[list[str], int]:
    runtime_end = argv.index("--env-args") if "--env-args" in argv else len(argv)
    runtime_args = list(argv[:runtime_end])
    env_args = list(argv[runtime_end:])
    cleaned = []
    values = []
    index = 0
    while index < len(runtime_args):
        arg = runtime_args[index]
        if arg == MICROBATCH_WORKER_CAP:
            if index + 1 == len(runtime_args):
                raise SystemExit(f"FAIL: {MICROBATCH_WORKER_CAP} requires a positive integer")
            values.append(runtime_args[index + 1])
            index += 2
            continue
        if arg.startswith(MICROBATCH_WORKER_CAP + "="):
            values.append(arg.split("=", 1)[1])
            index += 1
            continue
        cleaned.append(arg)
        index += 1
    if len(values) > 1:
        raise SystemExit(f"FAIL: {MICROBATCH_WORKER_CAP} may be specified only once")
    value = values[0] if values else "1"
    try:
        worker_cap = int(value)
    except ValueError as exception:
        raise SystemExit(
            f"FAIL: {MICROBATCH_WORKER_CAP} requires a positive integer; got {value!r}"
        ) from exception
    if worker_cap < 1:
        raise SystemExit(
            f"FAIL: {MICROBATCH_WORKER_CAP} requires a positive integer; got {value!r}"
        )
    return cleaned + env_args, worker_cap


if __name__ == "__main__":
    main()
