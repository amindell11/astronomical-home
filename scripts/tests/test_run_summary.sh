#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/balance/run_summary.py

# A fixture record file through the summary script: the Runs rows and spreads per stat
# fingerprint, each run's outcome, the mixed flag, the three pooled tables, and the
# unknown-schema refusal. A second file mixes v2 and v3, including an abandoned run.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUMMARY="$SCRIPT_DIR/../balance/run_summary.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"

# Three v1 deaths and one v2 extraction. The first two share a stat fingerprint; the second
# spawns the Lasers loadout under a second loadout stat hash (3333…), which makes it mixed.
# A fifth record carries a build label and its own fingerprint.
PLAYER='{"chassis":"Vanguard","engine":"Default_Engine","shield":"Default_Shield","primary":"Lasers","secondary":"","statHash":"a1b2c3d4e5f60718"}'
lasers() { echo '{"chassis":"Ship_2","engine":"Racer_Engine","shield":"Light_Shield","primary":"Lasers","secondary":"","statHash":"'"$1"'"}'; }
RIPPERS='{"chassis":"Ship_2","engine":"Racer_Engine","shield":"Light_Shield","primary":"Rippers","secondary":"Missiles","statHash":"2222222222222222"}'
cat > "$TMP/run-records.jsonl" <<EOF
{"schema":"run-record-v1","endedUtc":"2026-10-01T12:34:56Z","sector":"TrialSector","buildIdentity":{"commit":"abc123de4567890f","dirty":true},"statFingerprint":"f0e1d2c3b4a59687","player":$PLAYER,"kills":3,"secondsSurvived":42.5,"damage":[{"kind":"Laser","total":25.0,"hits":2,"source":"Ship_2","spawn":1},{"kind":"Collision","total":40.0,"hits":1,"source":"asteroid","spawn":-1}],"killingBlow":{"kind":"Laser","spawn":1},"spawns":[{"spawnSeconds":1.0,"pilot":"AICommander","loadout":$(lasers 1111111111111111),"aliveSeconds":10.0,"killedByPlayer":true},{"spawnSeconds":2.0,"pilot":"AICommander","loadout":$RIPPERS,"aliveSeconds":40.5,"killedByPlayer":false}]}
{"schema":"run-record-v1","endedUtc":"2026-10-01T13:00:00Z","sector":"TrialSector","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"f0e1d2c3b4a59687","player":$PLAYER,"kills":1,"secondsSurvived":21.5,"damage":[{"kind":"Laser","total":30.0,"hits":3,"source":"Ship_2","spawn":0}],"killingBlow":{"kind":"Collision","spawn":-1},"spawns":[{"spawnSeconds":0.0,"pilot":"AICommander","loadout":$(lasers 1111111111111111),"aliveSeconds":5.0,"killedByPlayer":true},{"spawnSeconds":13.5,"pilot":"AICommander","loadout":$(lasers 3333333333333333),"aliveSeconds":8.0,"killedByPlayer":false}]}
{"schema":"run-record-v1","endedUtc":"2026-10-02T09:00:00Z","sector":"TrialSector","buildIdentity":{"commit":"0f9e8d7c6b5a4321","dirty":false},"statFingerprint":"0123456789abcdef","player":$PLAYER,"kills":5,"secondsSurvived":60.0,"damage":[{"kind":"Laser","total":12.5,"hits":1,"source":"Ship_2","spawn":0}],"killingBlow":{"kind":"Laser","spawn":0},"spawns":[{"spawnSeconds":4.0,"pilot":"AICommander","loadout":$RIPPERS,"aliveSeconds":30.5,"killedByPlayer":true}]}
{"schema":"run-record-v2","endedUtc":"2026-10-03T10:00:00Z","outcome":"extracted","sectorsCleared":3,"sector":"Sector 3","buildIdentity":{"commit":"0f9e8d7c6b5a4321","dirty":false},"statFingerprint":"0123456789abcdef","player":$PLAYER,"kills":7,"secondsSurvived":480.0,"damage":[],"killingBlow":{"kind":"","spawn":-1},"spawns":[]}
{"schema":"run-record-v2","endedUtc":"2026-10-04T08:00:00Z","outcome":"extracted","sectorsCleared":1,"sector":"Sector 1","buildIdentity":{"commit":"b1b1b1b1cccccccc","dirty":false,"label":"friends-1"},"statFingerprint":"fedcba9876543210","player":$PLAYER,"kills":4,"secondsSurvived":12.0,"damage":[],"killingBlow":{"kind":"","spawn":-1},"spawns":[]}
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
has "## Runs on stat fingerprint 0123456789abcdef (Sector 3, TrialSector)" "a Runs table per stat fingerprint"
has "| 2026-10-01T12:34:56Z | abc123de+dirty | $PLAYER_CELL | died (0 cleared) | 3 | 42.5 | Laser from $RIPPERS_CELL | |" "run row"
has "| 2026-10-01T13:00:00Z | abc123de | $PLAYER_CELL | died (0 cleared) | 1 | 21.5 | Collision (no ship) | mixed |" "mixed run row"
has "| 2026-10-02T09:00:00Z | 0f9e8d7c | $PLAYER_CELL | died (0 cleared) | 5 | 60 | Laser from $RIPPERS_CELL | |" "a v1 record reads as a death"
has "| 2026-10-03T10:00:00Z | 0f9e8d7c | $PLAYER_CELL | extracted (3 cleared) | 7 | 480 | - | |" "an extracted run has no killing blow"
has "| 2026-10-04T08:00:00Z | friends-1 b1b1b1b1 | $PLAYER_CELL | extracted (1 cleared) | 4 | 12 | - | |" "a build label prints before the commit"
has "mixed 2026-10-01T13:00:00Z: $LASERS_CELL shows loadout stat hashes 1111111111111111, 3333333333333333" "mixed note"
[[ "$(grep -c "| mixed |" <<< "$out")" -eq 1 ]] || fail "only the run with two hashes for one named loadout is mixed"$'\n'"$out"

has "kills: min 1 / median 2 / max 3" "kills spread of the shared fingerprint"
has "seconds survived: min 21.5 / median 32 / max 42.5" "seconds-survived spread of the shared fingerprint"
has "kills: min 5 / median 6 / max 7" "kills spread of the second fingerprint"
has "seconds survived: min 60 / median 270 / max 480" "seconds-survived spread of the second fingerprint"

has "| (no ship) | asteroid | Collision | - | 40 | 1 |" "damage from no logged ship"
has "| 2222222222222222 | $RIPPERS_CELL | Laser | 2 | 37.5 | 3 |" "damage pooled across fingerprints"
has "| 1111111111111111 | $LASERS_CELL | Laser | 2 | 30 | 3 |" "damage row"
has "| 2222222222222222 | $RIPPERS_CELL | Laser | 2 | 2 |" "killing blows pooled across fingerprints"
has "| (no ship) | | Collision | - | 1 |" "killing blow from no logged ship"
has "| 1111111111111111 | $LASERS_CELL | 2 | 2 | 2 | 7.5 |" "spawned row"
has "| 2222222222222222 | $RIPPERS_CELL | 2 | 2 | 1 | 35.5 |" "spawned row"
has "| 3333333333333333 | $LASERS_CELL | 1 | 1 | 0 | 8 |" "spawned row of the second hash"
[[ "$out" != *"## Picks"* ]] || fail "records without picks print no picks table"$'\n'"$out"
[[ "$out" != *"## Sectors"* ]] || fail "records without sectors print no sectors table"$'\n'"$out"
[[ "$out" != *"## Damage dealt"* ]] || fail "records without dealt damage print no dealt table"$'\n'"$out"

# One v3 death carrying the new fields, one v3 abandon, and one v2 line that lacks them.
cat > "$TMP/mixed.jsonl" <<EOF
{"schema":"run-record-v2","endedUtc":"2026-11-01T10:00:00Z","outcome":"extracted","sectorsCleared":1,"sector":"Sector 1","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"aaaaaaaaaaaaaaaa","player":$PLAYER,"kills":2,"secondsSurvived":10.0,"damage":[],"killingBlow":{"kind":"","spawn":-1},"spawns":[]}
{"schema":"run-record-v3","endedUtc":"2026-11-01T11:00:00Z","outcome":"died","sectorsCleared":0,"sector":"Sector 1","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"aaaaaaaaaaaaaaaa","player":$PLAYER,"kills":4,"secondsSurvived":20.0,"damage":[],"killingBlow":{"kind":"Missile","spawn":-1},"spawns":[],"picks":[{"kind":"mod","sector":1,"seconds":4.0,"offered":["Overcharge","Cooling"],"taken":"Overcharge","replaced":""},{"kind":"weapon","sector":1,"seconds":9.0,"offered":["Rippers","Missiles"],"taken":"","replaced":""}],"sectors":[{"sector":1,"seconds":12.5,"kills":2,"hullIn":1.0,"shieldIn":1.0,"hullOut":0.5,"shieldOut":0.0,"eliteMet":true,"eliteBeaten":false}],"dealt":[{"sector":1,"kind":"Laser","total":40.0,"shield":10.0,"hull":30.0,"hits":4}],"endPhase":{"index":2,"inLull":false,"done":true,"elapsedSeconds":3.0}}
{"schema":"run-record-v3","endedUtc":"2026-11-01T12:00:00Z","outcome":"abandoned","sectorsCleared":0,"sector":"Sector 1","buildIdentity":{"commit":"abc123de4567890f","dirty":false},"statFingerprint":"aaaaaaaaaaaaaaaa","player":$PLAYER,"kills":1,"secondsSurvived":5.0,"damage":[],"killingBlow":{"kind":"Ram","spawn":-1},"spawns":[]}
EOF

rc=0
mixed="$("$PY" "$SUMMARY" "$TMP/mixed.jsonl" 2>"$TMP/stderr" | tr -s ' ')" || rc=$?
[[ "$rc" -eq 0 ]] || fail "a mix of v2 and v3 must exit 0 (got $rc: $(cat "$TMP/stderr"))"
mix() { [[ "$mixed" == *"$1"* ]] || fail "$2 — missing: $1"$'\n'"$mixed"; }
mix "| 2026-11-01T12:00:00Z | abc123de | $PLAYER_CELL | abandoned (0 cleared) | 1 | 5 | - | |" "an abandoned run is a Runs row"
mix "kills: min 1 / median 2 / max 4" "an abandoned run stays in the Runs spread"
mix "| Overcharge | mod | 1 | 1 |" "a taken mod"
mix "| Cooling | mod | 1 | 0 |" "an offered mod that was not taken"
mix "| Missiles | weapon | 1 | 0 |" "a skipped weapon offer"
mix "| Rippers | weapon | 1 | 0 |" "a skipped weapon offer"
mix "| 1 | 1 | 12.5 | 0.5 | 1 | 0 |" "sector row from the v3 record only"
mix "| Laser | 40 | 4 | 1 |" "damage dealt"
mix "| 1 | elite | died | 1 |" "a death during the elite fight, named from the phase mark and the elite flags"
[[ "$mixed" != *"Ram"* ]] || fail "an abandoned run stays out of the killing blows"$'\n'"$mixed"
blows="${mixed#*## Killing blows}"
[[ "$blows" == *"Missile"* ]] || fail "a death's killing blow is pooled"$'\n'"$mixed"

head -n 1 "$TMP/run-records.jsonl" > "$TMP/unknown-schema.jsonl"
echo '{"schema":"run-record-v0","kills":1}' >> "$TMP/unknown-schema.jsonl"
rc=0
out="$("$PY" "$SUMMARY" "$TMP/unknown-schema.jsonl" 2>"$TMP/stderr")" || rc=$?
err="$(cat "$TMP/stderr")"
[[ "$rc" -eq 1 ]] || fail "an unknown schema tag must exit 1 (got $rc: $err)"
[[ -z "$out" ]] || fail "a refusal must print no table (got: $out)"
[[ "$err" == *"unknown-schema.jsonl:2"* ]] || fail "the refusal must name the file and line (got: $err)"
[[ "$err" == *'"run-record-v0"'* ]] || fail "the refusal must name the tag it met (got: $err)"

echo "PASS: balance/run_summary.py — table rows, outcomes, mixed flag, unknown-schema refusal"
