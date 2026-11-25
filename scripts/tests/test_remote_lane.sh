#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/remote_lane.sh scripts/remote_gate.sh scripts/remote_editor.sh

# Hermetic regression for scripts/remote_lane.sh: the verdict order (disabled > unreachable > busy >
# available), the two-marker switch, the refusals remote_gate.sh and `remote_editor.sh start` read
# from it, and their published exit codes. ssh is a stub that replays a canned box report and logs
# every call.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LANE="$SCRIPT_DIR/../remote_lane.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export SSH_LOG="$TMP/ssh.log"
export REMOTE_LANE_STATE_DIR="$TMP/state"
mkdir -p "$FIX" "$TMP/bin"

cat > "$TMP/bin/ssh" <<'EOF'
#!/usr/bin/env bash
cat > /dev/null
echo "$*" >> "$SSH_LOG"
[[ ! -e "$FIX/box.down" ]] || exit 255
cat "$FIX/report.txt"
EOF
chmod +x "$TMP/bin/ssh"
export PATH="$TMP/bin:$PATH"

box_report() { printf '%s\n' "$@" > "$FIX/report.txt"; }
ssh_calls() { [[ -f "$SSH_LOG" ]] && wc -l < "$SSH_LOG" || echo 0; }
idle_box() { box_report FREE_RAM_GB=18.2 CHECKOUT=abc1234 DIRTY_FILES=0 CONSOLE_SESSION=true "UNITY_CLAIMS=${1:-0}" REPORT=complete; }

# available: an idle box, no marker on either side; the facts ride along.
idle_box
out="$("$LANE" status 2>/dev/null)"
[[ "$(head -1 <<<"$out")" == "REMOTE_LANE=available" ]] || fail "an idle box is available (got: $out)"
grep -qx 'FREE_RAM_GB=18.2' <<<"$out" && grep -qx 'CONSOLE_SESSION=true' <<<"$out" || fail "the box facts ride along (got: $out)"

# busy: any claim the box's coordinator reports.
idle_box 2
[[ "$("$LANE" status 2>/dev/null | head -1)" == "REMOTE_LANE=busy" ]] || fail "a coordinator claim makes the box busy"

# A wedged boot lane refuses the next boot, so it counts as a claim on its own.
box_report FREE_RAM_GB=18.2 CHECKOUT=abc1234 DIRTY_FILES=0 CONSOLE_SESSION=true UNITY_CLAIMS=0 BOOT_WEDGED=true REPORT=complete
out="$("$LANE" status 2>/dev/null)"
[[ "$(head -1 <<<"$out")" == "REMOTE_LANE=busy" ]] && grep -qx 'UNITY_CLAIMS=1' <<<"$out" || fail "a wedged boot lane makes the box busy (got: $out)"

# unreachable is a verdict, not an error.
touch "$FIX/box.down"
out="$("$LANE" status 2>/dev/null)" || fail "unreachable must exit 0"
[[ "$out" == "REMOTE_LANE=unreachable" ]] || fail "no SSH answer is unreachable (got: $out)"
rm "$FIX/box.down"

# A report cut short is an error with no verdict — never a guess.
box_report FREE_RAM_GB=18.2 CHECKOUT=abc1234
rc=0; out="$("$LANE" status 2>/dev/null)" || rc=$?
[[ "$rc" -eq 1 && -z "$out" ]] || fail "an incomplete report exits 1 with no verdict (rc=$rc: $out)"

# `remote_editor.sh start` maps each report to its published code with no SSH call of its own.
editor_start_exits() { # <code> <why>
    local calls rc=0
    calls="$(ssh_calls)"
    "$SCRIPT_DIR/../remote_editor.sh" start >/dev/null 2>"$TMP/err" || rc=$?
    [[ "$rc" -eq "$1" ]] || fail "remote_editor.sh start exits $1 when $2 (rc=$rc: $(cat "$TMP/err"))"
    [[ "$(ssh_calls)" -eq $((calls + 1)) ]] || fail "remote_editor.sh start probes nothing beyond status when $2"
}
editor_start_exits 9 "status gives no verdict"
grep -q 'report is incomplete' "$TMP/err" || fail "start relays status's reason for no verdict (got: $(cat "$TMP/err"))"
touch "$FIX/box.down"
editor_start_exits 3 "the box is unreachable"
rm "$FIX/box.down"
box_report FREE_RAM_GB=18.2 CHECKOUT=abc1234 DIRTY_FILES=0 CONSOLE_SESSION=false UNITY_CLAIMS=0 REPORT=complete
editor_start_exits 4 "the box has no console session"

# remote_gate.sh: an LFS object missing from the local cache is its own code, before any suite runs.
git init -q "$TMP/repo"
git -C "$TMP/repo" lfs install --local --skip-repo >/dev/null
git -C "$TMP/repo" lfs track '*.bin' >/dev/null
printf 'payload\n' > "$TMP/repo/a.bin"
git -C "$TMP/repo" add -A
git -C "$TMP/repo" -c user.name=t -c user.email=t@t commit -qm lfs
rm -rf "$TMP/repo/.git/lfs/objects"
box_report have
rc=0; (cd "$TMP/repo" && "$SCRIPT_DIR/../remote_gate.sh") </dev/null >/dev/null 2>"$TMP/err" || rc=$?
[[ "$rc" -eq 4 ]] || fail "remote_gate.sh exits 4 on a missing LFS object (rc=$rc: $(cat "$TMP/err"))"

# The marker on the box disables, ahead of busy.
box_report "MARKER=gaming tonight" FREE_RAM_GB=18.2 CHECKOUT=abc1234 DIRTY_FILES=0 CONSOLE_SESSION=true UNITY_CLAIMS=1 REPORT=complete
out="$("$LANE" status 2>/dev/null)"
[[ "$out" == $'REMOTE_LANE=disabled\nDISABLED_BY=there\nREASON=gaming tonight' ]] || fail "the box's marker disables with its reason (got: $out)"

# disable is local: it needs no SSH, and status then answers without asking the box.
idle_box
calls="$(ssh_calls)"
[[ "$("$LANE" disable "box   on loan" 2>/dev/null)" == "REMOTE_LANE_SWITCH=disabled" ]] || fail "disable stamps its trailer"
out="$("$LANE" status 2>/dev/null)"
[[ "$out" == $'REMOTE_LANE=disabled\nDISABLED_BY=here\nREASON=box on loan' ]] || fail "the local marker disables with its reason (got: $out)"
[[ "$(ssh_calls)" == "$calls" ]] || fail "disable and a locally disabled status make no SSH call"

# Both dispatchers refuse while disabled, before reaching the box.
rc=0; "$SCRIPT_DIR/../remote_gate.sh" >/dev/null 2>"$TMP/err" || rc=$?
[[ "$rc" -eq 3 ]] || fail "remote_gate.sh exits 3 while disabled (rc=$rc: $(cat "$TMP/err"))"
rc=0; "$SCRIPT_DIR/../remote_editor.sh" start >/dev/null 2>"$TMP/err" || rc=$?
[[ "$rc" -eq 8 ]] || fail "remote_editor.sh start exits 8 while disabled (rc=$rc: $(cat "$TMP/err"))"
[[ "$(ssh_calls)" == "$calls" ]] || fail "a refused dispatch makes no SSH call"

# enable clears the local marker and reports what it found on the box.
box_report REMOTE_MARKER=cleared
out="$("$LANE" enable 2>/dev/null)"
[[ "$out" == $'REMOTE_LANE_SWITCH=enabled\nREMOTE_MARKER=cleared' ]] || fail "enable reports the box's marker (got: $out)"
[[ ! -e "$REMOTE_LANE_STATE_DIR/remote-lane-disabled" ]] || fail "enable removes the local marker"

# enable with the box asleep still enables here, and says the box went unchecked.
"$LANE" disable >/dev/null 2>&1
touch "$FIX/box.down"
out="$("$LANE" enable 2>/dev/null)"
[[ "$out" == $'REMOTE_LANE_SWITCH=enabled\nREMOTE_MARKER=unchecked' ]] || fail "enable with the box down is unchecked (got: $out)"
[[ ! -e "$REMOTE_LANE_STATE_DIR/remote-lane-disabled" ]] || fail "enable removes the local marker with the box down"

echo "PASS test_remote_lane.sh"
