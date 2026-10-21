#!/usr/bin/env python3
"""Summarize run records as tables.

Handed one or more run-record files; the game logs the store's path on every
append, and this script never looks for the store itself.

    python scripts/balance/run_summary.py <run-records.jsonl> [<more.jsonl> ...]

Accepted schema: "run-record-v1" (RunRecord.SchemaId), one JSON object per line.

Output, on stdout, is for reading and carries no machine contract:
  - one Runs table per stat fingerprint, with min / median / max of kills and of
    seconds survived below it, each on its own line. A run is marked mixed when
    one named loadout shows two loadout stat hashes inside it;
  - three tables pooled over every record handed in, keyed by loadout stat hash:
    damage taken, killing blows, ships spawned. "Runs seen" counts the records
    whose spawn log holds that hash.
No number combines two metrics.

Exit codes:
  0  the tables were printed.
  1  refused, nothing on stdout: stderr names the file and line whose schema tag
     is not the accepted one, or says the files held no record.
  2  usage error, or a file could not be read.
"""
import argparse
import json
import statistics
import sys
from collections import defaultdict

SCHEMA = "run-record-v1"
NO_SPAWN = -1
PARTS = ("chassis", "engine", "shield", "primary", "secondary")


def read_records(paths):
    records = []
    for path in paths:
        try:
            with open(path, encoding="utf-8") as f:
                lines = f.read().splitlines()
        except OSError as e:
            print("run_summary: cannot read %s: %s" % (path, e.strerror), file=sys.stderr)
            sys.exit(2)
        for line_number, line in enumerate(lines, 1):
            try:
                record = json.loads(line)
                tag = record.get("schema")
            except (ValueError, AttributeError):
                record, tag = None, None
            if tag != SCHEMA:
                print("run_summary: refused %s:%d: schema tag %s, accepted \"%s\""
                      % (path, line_number, "unreadable" if record is None else json.dumps(tag), SCHEMA),
                      file=sys.stderr)
                sys.exit(1)
            records.append(record)
    return records


def label(loadout):
    return " / ".join(loadout[part] or "-" for part in PARTS)


def number(value):
    return "%d" % value if float(value).is_integer() else "%.1f" % value


def table(headers, rows):
    cells = [headers] + [[str(cell) for cell in row] for row in rows]
    widths = [max(len(row[i]) for row in cells) for i in range(len(headers))]
    lines = ["| " + " | ".join(cell.ljust(width) for cell, width in zip(row, widths)) + " |" for row in cells]
    lines.insert(1, "|" + "|".join("-" * (width + 2) for width in widths) + "|")
    return "\n".join(lines)


def spread(name, values):
    return "%s: min %s / median %s / max %s" % (
        name, number(min(values)), number(statistics.median(values)), number(max(values)))


def mixed_loadouts(record):
    hashes = defaultdict(set)
    for loadout in [record["player"]] + [spawn["loadout"] for spawn in record["spawns"]]:
        hashes[label(loadout)].add(loadout["statHash"])
    return {name: sorted(seen) for name, seen in hashes.items() if len(seen) > 1}


def killing_blow(record):
    blow = record["killingBlow"]
    if blow["spawn"] == NO_SPAWN:
        return "%s (no ship)" % blow["kind"]
    return "%s from %s" % (blow["kind"], label(record["spawns"][blow["spawn"]]["loadout"]))


def runs_section(fingerprint, records):
    records = sorted(records, key=lambda record: record["endedUtc"])
    sectors = sorted({record["sector"] for record in records})
    rows, notes = [], []
    for record in records:
        identity = record["buildIdentity"]
        mixed = mixed_loadouts(record)
        rows.append([
            record["endedUtc"],
            identity["commit"][:8] + ("+dirty" if identity["dirty"] else ""),
            "%s [%s]" % (label(record["player"]), record["player"]["statHash"]),
            record["kills"],
            number(record["secondsSurvived"]),
            killing_blow(record),
            "mixed" if mixed else "",
        ])
        for name, seen in mixed.items():
            notes.append("mixed %s: %s shows loadout stat hashes %s" % (record["endedUtc"], name, ", ".join(seen)))
    return "\n".join([
        "## Runs on stat fingerprint %s (%s)" % (fingerprint, ", ".join(sectors)),
        "",
        table(["Ended (UTC)", "Build identity", "Player loadout", "Kills", "Seconds survived", "Killing blow",
               "Mixed"], rows),
        "",
        spread("kills", [record["kills"] for record in records]),
        spread("seconds survived", [record["secondsSurvived"] for record in records]),
    ] + notes)


def pooled_sections(records):
    names = {}
    runs_seen = defaultdict(int)
    damage = defaultdict(lambda: [0.0, 0])
    blows = defaultdict(int)
    alive = defaultdict(list)
    killed = defaultdict(int)

    def attacker(record, spawn, source):
        if spawn == NO_SPAWN:
            return "(no ship)", source
        loadout = record["spawns"][spawn]["loadout"]
        return loadout["statHash"], label(loadout)

    for record in records:
        for spawn in record["spawns"]:
            stat_hash = spawn["loadout"]["statHash"]
            names.setdefault(stat_hash, label(spawn["loadout"]))
            alive[stat_hash].append(spawn["aliveSeconds"])
            killed[stat_hash] += spawn["killedByPlayer"]
        for stat_hash in {spawn["loadout"]["statHash"] for spawn in record["spawns"]}:
            runs_seen[stat_hash] += 1
        for row in record["damage"]:
            entry = damage[attacker(record, row["spawn"], row["source"]) + (row["kind"],)]
            entry[0] += row["total"]
            entry[1] += row["hits"]
        blow = record["killingBlow"]
        blows[attacker(record, blow["spawn"], "") + (blow["kind"],)] += 1

    def seen(stat_hash):
        return runs_seen.get(stat_hash, "-")

    damage_rows = [[stat_hash, name, kind, seen(stat_hash), number(total), hits]
                   for (stat_hash, name, kind), (total, hits)
                   in sorted(damage.items(), key=lambda item: (-item[1][0], item[0]))]
    blow_rows = [[stat_hash, name, kind, seen(stat_hash), count]
                 for (stat_hash, name, kind), count in sorted(blows.items(), key=lambda item: (-item[1], item[0]))]
    spawn_rows = [[stat_hash, names[stat_hash], runs_seen[stat_hash], len(seconds), killed[stat_hash],
                   number(statistics.median(seconds))]
                  for stat_hash, seconds in sorted(alive.items(), key=lambda item: (-len(item[1]), item[0]))]
    return [
        "## Damage taken\n\n" + table(
            ["Loadout stat hash", "Loadout", "Kind", "Runs seen", "Damage", "Hits"], damage_rows),
        "## Killing blows\n\n" + table(
            ["Loadout stat hash", "Loadout", "Kind", "Runs seen", "Killing blows"], blow_rows),
        "## Ships spawned\n\n" + table(
            ["Loadout stat hash", "Loadout", "Runs seen", "Spawned", "Killed by the player", "Median seconds alive"],
            spawn_rows),
    ]


def main():
    parser = argparse.ArgumentParser(description="Summarize run records as tables.")
    parser.add_argument("record_files", nargs="+", help="run-record files, one JSON object per line")
    args = parser.parse_args()

    records = read_records(args.record_files)
    if not records:
        print("run_summary: no record in the files handed in", file=sys.stderr)
        sys.exit(1)

    by_fingerprint = defaultdict(list)
    for record in records:
        by_fingerprint[record["statFingerprint"]].append(record)

    sections = ["run records: %d, files: %d" % (len(records), len(args.record_files))]
    sections += [runs_section(fingerprint, runs) for fingerprint, runs in by_fingerprint.items()]
    sections += pooled_sections(records)
    print("\n\n".join(sections))


if __name__ == "__main__":
    main()
