#!/usr/bin/env python3
"""Render a balance dump as its derived table, and the delta against a baseline dump.

Handed the dump paths; the dump test logs where it writes each dump, and this
script never looks for a dump itself.

    python scripts/balance/balance_table.py <dump.json> [<baseline.json>]

Accepted schema: "balance-dump-v1" (BalanceDump.SchemaId), one JSON object per file.

Output, on stdout, is markdown for reading and carries no machine contract:
  - the derived table of <dump.json>: one row per weapon and cycle mode, with
    stakes once per distinct ship resource pool;
  - handed a baseline too, the delta tables against it. Inputs are keyed by their
    asset/Type.field key, rows by weapon and mode label; a key or row found in
    one dump only shows as added or removed. Values are compared as printed, so
    a change below the printed precision shows no delta.
Damage prints to two decimals, seconds to three, stakes as a percent of the pool
to one; a recovery that never comes prints "never".

Exit codes:
  0  the tables were printed.
  1  refused, nothing on stdout: stderr names the file whose schema tag is not
     the accepted one.
  2  usage error, or a file could not be read.
"""
import argparse
import json
import math
import os
import sys

from run_summary import table

SCHEMA = "balance-dump-v1"
MEASURES = (
    ("Opening damage", "openingDamage", 2),
    ("Opening s", "openingSeconds", 3),
    ("Magazine", "magazineDamage", 2),
    ("Dump s", "dumpSeconds", 3),
    ("Recovery s", "recoverySeconds", 3),
    ("Cycle s", "cycleSeconds", 3),
    ("Sustained DPS", "sustainedDps", 2),
)


def read_dump(path):
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except OSError as e:
        print("balance_table: cannot read %s: %s" % (path, e.strerror), file=sys.stderr)
        sys.exit(2)
    try:
        dump = json.loads(text)
        tag = dump.get("schema")
    except (ValueError, AttributeError):
        dump, tag = None, None
    if tag != SCHEMA:
        print("balance_table: refused %s: schema tag %s, accepted \"%s\""
              % (path, "unreadable" if dump is None else json.dumps(tag), SCHEMA), file=sys.stderr)
        sys.exit(1)
    return dump


def number(value, places):
    if math.isinf(value):
        return "never"
    return ("%.*f" % (places, value)).rstrip("0").rstrip(".")


def stake_header(pool):
    return "Stakes @%s" % number(pool, 2)


def describe(path, dump):
    identity = dump["buildIdentity"]
    return "%s: taken %s, build %s, stat fingerprint %s" % (
        os.path.basename(path), dump["takenUtc"],
        identity["commit"][:8] + ("+dirty" if identity["dirty"] else ""), dump["statFingerprint"])


def cells(row, pools):
    printed = {name: number(row[field], places) for name, field, places in MEASURES}
    for pool, stake in zip(pools, row["stakes"]):
        printed[stake_header(pool)] = number(stake * 100, 1) + "%"
    return printed


def rows_by_key(dump):
    return {(row["weapon"], row["mode"]): cells(row, dump["pools"]) for row in dump["derived"]}


def inputs_by_key(dump):
    values = {}
    for line in dump["inputs"]:
        key, _, value = line.partition("=")
        values.setdefault(key, []).append(value)
    return {key: ", ".join(sorted(found)) for key, found in values.items()}


def derived_section(path, dump):
    headers = ["Weapon", "Mode"] + [name for name, _, _ in MEASURES] + [stake_header(p) for p in dump["pools"]]
    rows = [[row["weapon"], row["mode"]] + list(cells(row, dump["pools"]).values()) for row in dump["derived"]]
    return "\n".join([
        "# Derived table",
        "",
        describe(path, dump),
        "Ship resource pools (hull plus shield, each distinct pair once): "
        + ", ".join(number(pool, 2) for pool in dump["pools"]),
        "",
        table(headers, rows),
    ])


def delta_section(baseline_path, baseline, dump):
    old_inputs, new_inputs = inputs_by_key(baseline), inputs_by_key(dump)
    input_rows = []
    for key in sorted(old_inputs.keys() | new_inputs.keys()):
        old, new = old_inputs.get(key), new_inputs.get(key)
        if old != new:
            change = "added" if old is None else "removed" if new is None else "changed"
            input_rows.append([key, change, old or "-", new or "-"])

    old_rows, new_rows = rows_by_key(baseline), rows_by_key(dump)
    row_rows = []
    for key in list(new_rows) + [key for key in old_rows if key not in new_rows]:
        old, new = old_rows.get(key), new_rows.get(key)
        if old is None or new is None:
            row_rows.append(list(key) + ["added" if old is None else "removed", "-", "-", "-"])
            continue
        for measure in list(new) + [measure for measure in old if measure not in new]:
            if old.get(measure) != new.get(measure):
                row_rows.append(list(key) + ["changed", measure, old.get(measure, "-"), new.get(measure, "-")])

    return "\n".join([
        "# Delta against the baseline",
        "",
        "Baseline " + describe(baseline_path, baseline),
        "",
        "## Inputs",
        "",
        table(["Input", "Change", "Baseline", "This dump"], input_rows) if input_rows else "No input changed.",
        "",
        "## Derived rows",
        "",
        table(["Weapon", "Mode", "Change", "Measure", "Baseline", "This dump"], row_rows)
        if row_rows else "No derived row changed.",
    ])


def main():
    parser = argparse.ArgumentParser(description="Render a balance dump, and its delta against a baseline.")
    parser.add_argument("dump", help="the balance dump to render")
    parser.add_argument("baseline", nargs="?", help="an earlier balance dump to take the delta against")
    args = parser.parse_args()

    dump = read_dump(args.dump)
    sections = [derived_section(args.dump, dump)]
    if args.baseline:
        sections.append(delta_section(args.baseline, read_dump(args.baseline), dump))
    print("\n\n".join(sections))


if __name__ == "__main__":
    main()
