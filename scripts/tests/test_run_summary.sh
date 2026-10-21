#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/balance/run_summary.py

# A fixture record file through the summary script: the Runs rows and spreads per stat
# fingerprint, the mixed flag, the three pooled tables, and the unknown-schema refusal.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUMMARY="$SCRIPT_DIR/../balance/run_summary.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"

# Three runs. The first two share a stat fingerprint; the second spawns the Lasers loadout
# under a second loadout stat hash (3333…), which makes it mixed.
PLAYER='{"chassis":"Vanguard","engine":"Default_Engine","shield":"Default_Shield","primary":"Lasers","secondary":"","statHash":"a1b2c3d4e5f60718"}'
lasers() { echo '{"chassis":"Ship_2","engine":"Racer_Engine","shield":"Light_Shield","primary":"Lasers","secondary":"","statHash":"'"$1"'"}'; }
RIPPERS='{"chassis":"Ship_2","engine":"Racer_Engine","shield":"Light_Shield","primary":"Rippers","secondary":"Missiles","statHash":"2222222222222222"}'
cat > "$TMP/run-records.jsonl" <<EOF
{"schema":"run-record-v1","endedUtc":"2026-10-01T12:34:56Z","sector":"TrialSector","buildIdentity":{"commit":"abc123de4567890f","dirty":true},"statFingerprint":"f0e1d2c3b4a59687","player":$PLAYER,"kills":3,"secondsSurvived":42.5,"damage":[{"kind":"Laser","total":25.0,"hits":2,"source":"Ship_2","spawn":1},{"kind":"Collision","total":40.0,"hits":1,"source":"asteroid","spawn":-1}],"killingBlow":{"kind":"Laser","spawn":1},"spawns":[{"spawnSeconds":1.0,"pilot":"AICommander","loadout":$(lasers 1111111111111111),"aliveSeconds":10.0,"killedByPlayer":true},{"spawnSeconds":2.0,"pilot":"AICommander","loadout":$RIPPERS,"aliveSeconds":40.5,"killedByPlayer":false}]}
{"schema":"run-record-v1","endedUtc":"2026-10-01T13:00:00Z","sector":"TrialSector","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"f0e1d2c3b4a59687","player":$PLAYER,"kills":1,"secondsSurvived":21.5,"damage":[{"kind":"Laser","total":30.0,"hits":3,"source":"Ship_2","spawn":0}],"killingBlow":{"kind":"Collision","spawn":-1},"spawns":[{"spawnSeconds":0.0,"pilot":"AICommander","loadout":$(lasers 1111111111111111),"aliveSeconds":5.0,"killedByPlayer":true},{"spawnSeconds":13.5,"pilot":"AICommander","loadout":$(lasers 3333333333333333),"aliveSeconds":8.0,"killedByPlayer":false}]}
{"schema":"run-record-v1","endedUtc":"2026-10-02T09:00:00Z","sector":"TrialSector","buildIdentity":{"commit":"0f9e8d7c6b5a4321","dirty":false},"statFingerprint":"0123456789abcdef","player":$PLAYER,"kills":5,"secondsSurvived":60.0,"damage":[{"kind":"Laser","total":12.5,"hits":1,"source":"Ship_2","spawn":0}],"killingBlow":{"kind":"Laser","spawn":0},"spawns":[{"spawnSeconds":4.0,"pilot":"AICommander","loadout":$RIPPERS,"aliveSeconds":30.5,"killedByPlayer":true}]}
EOF

# Column padding squeezed to one space, so a row is asserted without its alignment.
rc=0
out="$("$PY" "$SUMMARY" "$TMP/run-records.jsonl" 2>"$TMP/stderr" | tr -s ' ')" || rc=$?
[[ "$rc" -eq 0 ]] || fail "accepted records must exit 0 (got $rc: $(cat "$TMP/stderr"))"

has() { [[ "$out" == *"$1"* ]] || fail "$2 — missing: $1"$'\n'"$out"; }

PLAYER_CELL="Vanguard / Default_Engine / Default_Shield / Lasers / - [a1b2c3d4e5f60718]"
LASERS_CELL="Ship_2 / Racer_Engine / Light_Shield / Lasers / -"
RIPPERS_CELL="Ship_2 / Racer_Engine / Light_Shield / Rippers / Missiles"

has "## Runs on stat fingerprint f0e1d2c3b4a59687 (TrialSector)" "a Runs table per stat fingerprint"
has "## Runs on stat fingerprint 0123456789abcdef (TrialSector)" "a Runs table per stat fingerprint"
has "| 2026-10-01T12:34:56Z | abc123de+dirty | $PLAYER_CELL | 3 | 42.5 | Laser from $RIPPERS_CELL | |" "run row"
has "| 2026-10-01T13:00:00Z | abc123de | $PLAYER_CELL | 1 | 21.5 | Collision (no ship) | mixed |" "mixed run row"
has "| 2026-10-02T09:00:00Z | 0f9e8d7c | $PLAYER_CELL | 5 | 60 | Laser from $RIPPERS_CELL | |" "run row"
has "mixed 2026-10-01T13:00:00Z: $LASERS_CELL shows loadout stat hashes 1111111111111111, 3333333333333333" "mixed note"
[[ "$(grep -c "| mixed |" <<< "$out")" -eq 1 ]] || fail "only the run with two hashes for one named loadout is mixed"$'\n'"$out"

has "kills: min 1 / median 2 / max 3" "kills spread of the shared fingerprint"
has "seconds survived: min 21.5 / median 32 / max 42.5" "seconds-survived spread of the shared fingerprint"
has "kills: min 5 / median 5 / max 5" "kills spread of the lone fingerprint"
has "seconds survived: min 60 / median 60 / max 60" "seconds-survived spread of the lone fingerprint"

has "| (no ship) | asteroid | Collision | - | 40 | 1 |" "damage from no logged ship"
has "| 2222222222222222 | $RIPPERS_CELL | Laser | 2 | 37.5 | 3 |" "damage pooled across fingerprints"
has "| 1111111111111111 | $LASERS_CELL | Laser | 2 | 30 | 3 |" "damage row"
has "| 2222222222222222 | $RIPPERS_CELL | Laser | 2 | 2 |" "killing blows pooled across fingerprints"
has "| (no ship) | | Collision | - | 1 |" "killing blow from no logged ship"
has "| 1111111111111111 | $LASERS_CELL | 2 | 2 | 2 | 7.5 |" "spawned row"
has "| 2222222222222222 | $RIPPERS_CELL | 2 | 2 | 1 | 35.5 |" "spawned row"
has "| 3333333333333333 | $LASERS_CELL | 1 | 1 | 0 | 8 |" "spawned row of the second hash"

head -n 1 "$TMP/run-records.jsonl" > "$TMP/unknown-schema.jsonl"
echo '{"schema":"run-record-v0","kills":1}' >> "$TMP/unknown-schema.jsonl"
rc=0
out="$("$PY" "$SUMMARY" "$TMP/unknown-schema.jsonl" 2>"$TMP/stderr")" || rc=$?
err="$(cat "$TMP/stderr")"
[[ "$rc" -eq 1 ]] || fail "an unknown schema tag must exit 1 (got $rc: $err)"
[[ -z "$out" ]] || fail "a refusal must print no table (got: $out)"
[[ "$err" == *"unknown-schema.jsonl:2"* ]] || fail "the refusal must name the file and line (got: $err)"
[[ "$err" == *'"run-record-v0"'* ]] || fail "the refusal must name the tag it met (got: $err)"

echo "PASS: balance/run_summary.py — table rows, mixed flag, unknown-schema refusal"
