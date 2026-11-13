#!/usr/bin/env python3
"""Summarize run records as tables.

Handed one or more run-record files; the game logs the store's path on every
append, and this script never looks for the store itself.

    python scripts/balance/run_summary.py <run-records.jsonl> [<more.jsonl> ...]

Accepted schemas: "run-record-v3" (RunRecord.SchemaId), "run-record-v2", and "run-record-v1",
one JSON object per line. A v1 record predates multi-sector runs and reads as a death with no
sector cleared. A v2 or v1 record has none of the v3 fields.

Output, on stdout, is for reading and carries no machine contract:
  - one Runs table per stat fingerprint, with each run's outcome and min / median /
    max of kills and of seconds survived below it, each on its own line. A run is
    marked mixed when one named loadout shows two loadout stat hashes inside it;
  - three tables pooled over every record handed in, keyed by loadout stat hash:
    damage taken, killing blows (deaths only; an abandoned run is left out), ships spawned.
    "Runs seen" counts the records whose spawn log holds that hash;
  - four tables built only from records that carry the field: where runs ended (deaths and
    abandoned runs per sector and phase, the phase named from the record's phase mark and the
    sector's elite flags), picks (per offered item and
    kind: times offered, times taken), sectors (per sector number: runs entered, median
    seconds, median hull out, elite met, elite beaten), damage dealt (per damage kind:
    total, hits, runs).
No number combines two metrics.

Exit codes:
  0  the tables were printed.
  1  refused, nothing on stdout: stderr names the file and line whose schema tag
     is not an accepted one, or says the files held no record.
  2  usage error, or a file could not be read.
"""
import argparse
import json
import statistics
import sys
from collections import defaultdict

SCHEMAS = ("run-record-v3", "run-record-v2", "run-record-v1")
V1 = "run-record-v1"
EXTRACTED = "extracted"
ABANDONED = "abandoned"
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
            if tag not in SCHEMAS:
                print("run_summary: refused %s:%d: schema tag %s, accepted %s"
                      % (path, line_number, "unreadable" if record is None else json.dumps(tag),
                         ", ".join(json.dumps(schema) for schema in SCHEMAS)),
                      file=sys.stderr)
                sys.exit(1)
            if tag == V1:
                record.update(outcome="died", sectorsCleared=0)
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
    if record["outcome"] in (EXTRACTED, ABANDONED):
        return "-"
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
            "%s (%d cleared)" % (record["outcome"], record["sectorsCleared"]),
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
        table(["Ended (UTC)", "Build identity", "Player loadout", "Outcome", "Kills", "Seconds survived",
               "Killing blow", "Mixed"], rows),
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
        if record["outcome"] != "died":
            continue
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


def carried(record, key):
    value = record.get(key)
    return value if isinstance(value, list) else None


def end_phase(mark, sector):
    """Names the phase from the record's phase mark; the mark is done through the elite fight and extraction."""
    if mark["done"]:
        return "elite" if sector["eliteMet"] and not sector["eliteBeaten"] else "extraction"
    if mark["inLull"]:
        return "hunt" if mark["index"] == 0 else "lull before phase %d" % (mark["index"] + 1)
    return "phase %d" % (mark["index"] + 1)


def measured_sections(records):
    offered = defaultdict(int)
    taken = defaultdict(int)
    for record in records:
        picks = carried(record, "picks")
        if picks is None:
            continue
        for pick in picks:
            kind = pick["kind"]
            for name in pick["offered"]:
                offered[(name, kind)] += 1
            got = pick.get("taken") or ""
            if got:
                taken[(got, kind)] += 1
    pick_rows = [[name, kind, offered[(name, kind)], taken[(name, kind)]]
                 for (name, kind) in sorted(offered, key=lambda item: (-offered[item], item[1], item[0]))]

    by_sector = defaultdict(lambda: {"seconds": [], "hull": [], "met": 0, "beaten": 0})
    for record in records:
        sectors = carried(record, "sectors")
        if sectors is None:
            continue
        for row in sectors:
            bucket = by_sector[row["sector"]]
            bucket["seconds"].append(row["seconds"])
            bucket["hull"].append(row["hullOut"])
            bucket["met"] += row["eliteMet"]
            bucket["beaten"] += row["eliteBeaten"]
    sector_rows = [[sector, len(bucket["seconds"]), number(statistics.median(bucket["seconds"])),
                    number(statistics.median(bucket["hull"])), bucket["met"], bucket["beaten"]]
                   for sector, bucket in sorted(by_sector.items())]

    dealt = defaultdict(lambda: [0.0, 0, 0])
    for record in records:
        rows = carried(record, "dealt")
        if rows is None:
            continue
        kinds = set()
        for row in rows:
            entry = dealt[row["kind"]]
            entry[0] += row["total"]
            entry[1] += row["hits"]
            kinds.add(row["kind"])
        for kind in kinds:
            dealt[kind][2] += 1
    dealt_rows = [[kind, number(total), hits, runs]
                  for kind, (total, hits, runs) in sorted(dealt.items(), key=lambda item: (-item[1][0], item[0]))]

    ended = defaultdict(int)
    for record in records:
        sectors = carried(record, "sectors")
        if not sectors or record["outcome"] == EXTRACTED:
            continue
        ended[(sectors[-1]["sector"], end_phase(record["endPhase"], sectors[-1]), record["outcome"])] += 1
    ended_rows = [[sector, phase, outcome, runs]
                  for (sector, phase, outcome), runs in sorted(ended.items(), key=lambda item: (-item[1], item[0]))]

    sections = []
    if ended_rows:
        sections.append("## Where runs ended\n\n" + table(
            ["Sector", "Phase", "Outcome", "Runs"], ended_rows))
    if pick_rows:
        sections.append("## Picks\n\n" + table(
            ["Item", "Kind", "Offered", "Taken"], pick_rows))
    if sector_rows:
        sections.append("## Sectors\n\n" + table(
            ["Sector", "Runs entered", "Median seconds", "Median hull out", "Elite met", "Elite beaten"],
            sector_rows))
    if dealt_rows:
        sections.append("## Damage dealt\n\n" + table(
            ["Kind", "Total", "Hits", "Runs"], dealt_rows))
    return sections


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
    sections += measured_sections(records)
    print("\n\n".join(sections))


if __name__ == "__main__":
    main()
