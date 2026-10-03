"""Write, verify or reopen a ship's geometry lock (lock.json beside the source).

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_lock.py -- --ship SHIP_JSON --out DIR
--mode lock|verify|reopen. The lock holds the ship-fp-1 geometry component and per-part
geometry hashes. lock: writes it; refuses when a different lock exists. verify: exit 3 when the
geometry no longer matches. reopen: reports changed parts and the downstream stages they
invalidate, then rewrites the lock to the current geometry. Prints SHIP_LOCK=<lock path>.
"""

import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source


def lock_record(ship, fingerprint, source_sha256):
    return {"schema_version": contract.SCHEMA_VERSION, "recipe": fingerprint["recipe"],
            "blender_version": ship_source.blender_version(), "source_sha256": source_sha256,
            "name": ship["name"], "geometry": fingerprint["components"]["geometry"],
            "parts": {name: record["geometry"] for name, record in fingerprint["parts"].items()}}


def changes(locked, current):
    return {"added": sorted(set(current) - set(locked)), "removed": sorted(set(locked) - set(current)),
            "changed": sorted(name for name in set(locked) & set(current) if locked[name] != current[name])}


def main(argv):
    parser = contract.tool_parser("Write, verify or reopen a ship's geometry lock.")
    parser.add_argument("--mode", required=True, choices=("lock", "verify", "reopen"))
    args = parser.parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    lock_path = source.parent / "lock.json"
    trailers = [("SHIP_LOCK", str(lock_path))]
    report = contract.Report("ship_lock", args.out, ship_source.blender_version(), [source])
    report.data["mode"] = args.mode
    if not source.is_file():
        raise report.refuse(f"Source not found: {source}", trailers)
    current = lock_record(ship, ship_source.fingerprint(ship_source.open_source(source)),
                          report.data["source_sha256"])
    locked = json.loads(lock_path.read_text(encoding="utf-8")) if lock_path.is_file() else None
    if locked is None and args.mode != "lock":
        raise report.refuse(f"No lock at {lock_path}", trailers)
    if locked is not None and locked.get("recipe") != contract.FINGERPRINT_RECIPE:
        raise report.refuse(f"Lock recipe {locked.get('recipe')!r} is not {contract.FINGERPRINT_RECIPE}", trailers)
    diff = changes(locked["parts"], current["parts"]) if locked else changes({}, {})
    matches = locked is not None and locked["geometry"] == current["geometry"]
    if args.mode == "lock":
        if locked is not None and not matches:
            raise report.refuse("A different lock exists; use --mode reopen", trailers)
        if locked is None:
            lock_path.write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
        return report.finish(True, trailers, geometry=current["geometry"])
    if args.mode == "verify":
        for kind, names in diff.items():
            for name in names:
                print(f"{kind}: {name}", file=sys.stderr)
        return report.finish(matches, trailers, geometry=current["geometry"], locked=locked["geometry"], **diff)
    invalidated = [] if matches else list(contract.INVALIDATED_BY_GEOMETRY)
    lock_path.write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
    return report.finish(True, trailers, geometry=current["geometry"], previous=locked["geometry"],
                         invalidated=invalidated, **diff)


if __name__ == "__main__":
    contract.run(main)
