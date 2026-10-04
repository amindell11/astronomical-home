#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/balance/balance_table.py scripts/balance/run_summary.py

# Fixture balance dumps through the table script: the derived table of one dump, the delta
# between two (a changed input, a changed row, an added and a removed row), and the
# unknown-schema refusal.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TABLE="$SCRIPT_DIR/../balance/balance_table.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"

# The baseline holds Lasers/hold and Railgun/hold; the new dump changes Cooldown.fireRate, which
# moves the Lasers row, drops Railgun, adds a Lasers AI row that never recovers, and adds a
# killHullRestore line. JsonUtility writes an endless recovery as Infinity.
row() { echo '{"weapon":"'"$1"'","mode":"'"$2"'","openingDamage":'"$3"',"openingSeconds":0.66,"magazineDamage":'"$3"',"dumpSeconds":0.6,"recoverySeconds":'"$4"',"cycleSeconds":'"$5"',"sustainedDps":'"$6"',"stakes":['"$7"']}'; }
cat > "$TMP/20261001T120000Z-balance-dump.json" <<EOF
{"schema":"balance-dump-v1","takenUtc":"2026-10-01T12:00:00Z","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"f0e1d2c3b4a59687","inputs":["Lasers/Cooldown.fireRate=0.2","Lasers/Heat.maxHeat=100","Ship_2/Ship.weapons=Lasers","Ship_2/Ship.weapons=Rippers"],"pools":[150.0,200.0],"derived":[$(row Lasers hold 80.0 4.825 5.425 14.746 0.5333,0.4),$(row Railgun hold 45.0 1.52 1.52 29.605 0.3,0.225)]}
EOF
cat > "$TMP/20261002T090000Z-balance-dump.json" <<EOF
{"schema":"balance-dump-v1","takenUtc":"2026-10-02T09:00:00Z","buildIdentity":{"commit":"0f9e8d7c6b5a4321","dirty":true},"statFingerprint":"0123456789abcdef","inputs":["Lasers/Cooldown.fireRate=0.25","Lasers/Heat.maxHeat=100","Ship_2/Ship.weapons=Lasers","Ship_2/Ship.weapons=Rippers","setting/killHullRestore=0.25"],"pools":[150.0,200.0],"derived":[$(row Lasers hold 80.0 4.825 5.475 14.612 0.5333,0.4),$(row Lasers AI 60.0 Infinity Infinity 0.0 0.4,0.3)]}
EOF
BASE="$TMP/20261001T120000Z-balance-dump.json"
NEW="$TMP/20261002T090000Z-balance-dump.json"

# Column padding squeezed to one space, so a row is asserted without its alignment.
run() {
  rc=0
  out="$("$PY" "$TABLE" "$@" 2>"$TMP/stderr" | tr -s ' ')" || rc=$?
  [[ "$rc" -eq 0 ]] || fail "accepted dumps must exit 0 (got $rc: $(cat "$TMP/stderr"))"
}
has() { [[ "$out" == *"$1"* ]] || fail "$2 — missing: $1"$'\n'"$out"; }
lacks() { [[ "$out" != *"$1"* ]] || fail "$2 — unexpected: $1"$'\n'"$out"; }

run "$BASE"
has "20261001T120000Z-balance-dump.json: taken 2026-10-01T12:00:00Z, build abc123de, stat fingerprint f0e1d2c3b4a59687" "dump header"
has "Ship resource pools (hull plus shield, each distinct pair once): 150, 200" "pools line"
has "| Weapon | Mode | Opening damage | Opening s | Magazine | Dump s | Recovery s | Cycle s | Sustained DPS | Stakes @150 | Stakes @200 |" "derived header, a stakes column per pool"
has "| Lasers | hold | 80 | 0.66 | 80 | 0.6 | 4.825 | 5.425 | 14.75 | 53.3% | 40% |" "derived row"
has "| Railgun | hold | 45 | 0.66 | 45 | 0.6 | 1.52 | 1.52 | 29.61 | 30% | 22.5% |" "derived row"
lacks "Delta" "one dump prints no delta"

run "$NEW" "$BASE"
has "| Lasers | AI | 60 | 0.66 | 60 | 0.6 | never | never | 0 | 40% | 30% |" "a recovery that never comes"
has "Baseline 20261001T120000Z-balance-dump.json: taken 2026-10-01T12:00:00Z, build abc123de, stat fingerprint f0e1d2c3b4a59687" "baseline header"
has "| Lasers/Cooldown.fireRate | changed | 0.2 | 0.25 |" "changed input"
has "| setting/killHullRestore | added | - | 0.25 |" "added input"
lacks "Ship_2/Ship.weapons" "a key with the same values in both is no delta"
lacks "Lasers/Heat.maxHeat" "an unchanged input is no delta"
has "| Lasers | hold | changed | Cycle s | 5.425 | 5.475 |" "changed row measure"
has "| Lasers | hold | changed | Sustained DPS | 14.75 | 14.61 |" "changed row measure"
lacks "| Lasers | hold | changed | Opening damage |" "an unchanged measure is no delta"
has "| Lasers | AI | added | - | - | - |" "added row"
has "| Railgun | hold | removed | - | - | - |" "removed row"

run "$BASE" "$BASE"
has "No input changed." "a dump against itself"
has "No derived row changed." "a dump against itself"

sed 's/balance-dump-v1/balance-dump-v0/' "$BASE" > "$TMP/unknown-schema.json"
rc=0
out="$("$PY" "$TABLE" "$NEW" "$TMP/unknown-schema.json" 2>"$TMP/stderr")" || rc=$?
err="$(cat "$TMP/stderr")"
[[ "$rc" -eq 1 ]] || fail "an unknown schema tag must exit 1 (got $rc: $err)"
[[ -z "$out" ]] || fail "a refusal must print no table (got: $out)"
[[ "$err" == *"unknown-schema.json"* ]] || fail "the refusal must name the file (got: $err)"
[[ "$err" == *'"balance-dump-v0"'* ]] || fail "the refusal must name the tag it met (got: $err)"

echo "PASS: balance/balance_table.py — derived table, delta keys and rows, unknown-schema refusal"
