"""CLI for the executed-return value baseline artifact producer."""

import argparse
import json
import sys
from pathlib import Path

from value_baseline import ArtifactMetadata, ValueBaselineError, build_value_artifact


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="+", type=Path,
                        help="transition JSONL files or directories")
    parser.add_argument("--output-dir", type=Path, required=True,
                        help="new directory that will own the artifact and audit outputs")
    parser.add_argument("--artifact-id", required=True,
                        help="stable identifier recorded in the manifest")
    parser.add_argument("--collection-command", required=True,
                        help="exact transition-collection command recorded for provenance")
    parser.add_argument("--collection-config", type=Path,
                        help="trainer YAML whose path and SHA-256 are recorded")
    parser.add_argument("--source-commit",
                        help="source Git commit used for collection (default: current HEAD)")
    args = parser.parse_args()

    metadata = ArtifactMetadata(
        artifact_id=args.artifact_id,
        collection_command=args.collection_command,
        collection_config=args.collection_config,
        source_commit=args.source_commit,
    )
    try:
        result = build_value_artifact(args.inputs, args.output_dir, metadata)
    except ValueBaselineError as error:
        sys.exit(f"FAIL: {error}")
    print(json.dumps(result, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
