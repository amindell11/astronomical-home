#!/usr/bin/env bash
#
# Front door of the remote lane — the second Unity box that remote_gate.sh and
# remote_editor.sh dispatch to over SSH: one availability verdict, and the switch
# that turns the lane off.
#
# Usage: remote_lane.sh status             read-only verdict
#        remote_lane.sh disable [reason]   switch the lane off from this machine
#        remote_lane.sh enable             switch it back on
#
# The switch is two marker files holding the reason; either one disables the lane:
#   here   $REMOTE_LANE_STATE_DIR/remote-lane-disabled — shared by every slot, and
#          settable while the box is asleep.
#   there  C:\dev\remote-lane-disabled on the box — create or delete it at that
#          box's own keyboard.
# disable writes the marker here. enable removes it and, when the box answers, the
# one there. remote_gate.sh and `remote_editor.sh start` refuse while either is set.
#
# Env:  REMOTE_LANE_HOST (alastor) · REMOTE_LANE_REPO (C:/dev/astronomical-home)
#       REMOTE_LANE_STATE_DIR (<primary tree>/.worktree-pool)
#       REMOTE_LANE_CONNECT_TIMEOUT (8, seconds)
# Exit: 0 verdict printed / switch changed · 1 the box answered but its report was
#       incomplete (no verdict is printed) · 2 usage.
# Stdout trailers, one per line, stable; prose goes to stderr.
#   status:  REMOTE_LANE=disabled|unreachable|busy|available   first match wins, in that order
#            DISABLED_BY=here|there · REASON=<text>             disabled only
#            FREE_RAM_GB · CHECKOUT · DIRTY_FILES · CONSOLE_SESSION=true|false · UNITY_CLAIMS
#                                                               busy and available only
#            busy means UNITY_CLAIMS > 0: owners + queue + blockers + a held or wedged boot
#            lane + a legacy owner, from the box's own `unity_access.ps1 -Action Status -Json`.
#            unreachable means SSH gave no answer: asleep, off, or off the network.
#            CONSOLE_SESSION=false means `remote_editor.sh start` cannot launch.
#   disable: REMOTE_LANE_SWITCH=disabled
#   enable:  REMOTE_LANE_SWITCH=enabled · REMOTE_MARKER=cleared|absent|unchecked
#            unchecked = the box did not answer; its marker, if set, still disables.

set -euo pipefail

HOST="${REMOTE_LANE_HOST:-alastor}"
RREPO="${REMOTE_LANE_REPO:-C:/dev/astronomical-home}"
CONNECT_TIMEOUT="${REMOTE_LANE_CONNECT_TIMEOUT:-8}"
STATE_DIR="${REMOTE_LANE_STATE_DIR:-$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")/.worktree-pool}"
MARKER_HERE="$STATE_DIR/remote-lane-disabled"
MARKER_THERE='C:\dev\remote-lane-disabled'

ACTION="${1:-}"
[ -n "$ACTION" ] || { sed -n '3,9p' "$0" >&2; exit 2; }
shift

# ssh exits 255 when it cannot connect; status reads that as unreachable.
box() {
    local out rc=0
    out="$(ssh -o BatchMode=yes -o ConnectTimeout="$CONNECT_TIMEOUT" "$HOST" 'powershell -NoProfile -NonInteractive -Command -')" || rc=$?
    printf '%s\n' "$out" | tr -d '\r'
    return "$rc"
}

field() { sed -n "s/^$1=//p" <<<"$2" | head -1; }

case "$ACTION" in

status)
    if [ -f "$MARKER_HERE" ]; then
        reason="$(head -1 "$MARKER_HERE")"
        echo "[remote_lane] $HOST: disabled here — $reason" >&2
        printf 'REMOTE_LANE=disabled\nDISABLED_BY=here\nREASON=%s\n' "$reason"
        exit 0
    fi

    rc=0
    report="$(box <<EOF
\$ErrorActionPreference = 'Stop'
if (Test-Path '$MARKER_THERE') { 'MARKER=' + ("\$(Get-Content '$MARKER_THERE' -Raw)" -replace '\s+', ' ').Trim() }
'FREE_RAM_GB=' + [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
Set-Location '$RREPO'
'CHECKOUT=' + (git rev-parse --short HEAD)
'DIRTY_FILES=' + @(git status --porcelain).Count
'CONSOLE_SESSION=' + ([bool](Get-Process explorer -ErrorAction SilentlyContinue)).ToString().ToLower()
\$s = & .\scripts\unity_access.ps1 -Action Status -Json | ConvertFrom-Json
'UNITY_CLAIMS=' + @(@(\$s.owners) + @(\$s.queue) + @(\$s.blockers) + @(\$s.boot) + @(\$s.legacyOwner) | Where-Object { \$null -ne \$_ }).Count
'BOOT_WEDGED=' + ([bool]\$s.bootWedged).ToString().ToLower()
'REPORT=complete'
EOF
)" || rc=$?

    if [ "$rc" -eq 255 ]; then
        echo "[remote_lane] $HOST: unreachable — no SSH answer in ${CONNECT_TIMEOUT}s (asleep, off, or off the network)" >&2
        echo "REMOTE_LANE=unreachable"
        exit 0
    fi
    if grep -q '^MARKER=' <<<"$report"; then
        reason="$(field MARKER "$report")"
        echo "[remote_lane] $HOST: disabled there — $reason" >&2
        printf 'REMOTE_LANE=disabled\nDISABLED_BY=there\nREASON=%s\n' "$reason"
        exit 0
    fi
    if [ "$rc" -ne 0 ] || ! grep -qx 'REPORT=complete' <<<"$report"; then
        echo "[remote_lane] $HOST answered but its report is incomplete (ssh exit $rc):" >&2
        echo "$report" >&2
        exit 1
    fi

    claims="$(field UNITY_CLAIMS "$report")"
    [ "$(field BOOT_WEDGED "$report")" != true ] || claims=$((claims + 1))
    verdict=available
    [ "$claims" -eq 0 ] || verdict=busy
    echo "[remote_lane] $HOST: $verdict — $(field FREE_RAM_GB "$report") GB free, $claims Unity claim(s), checkout $(field CHECKOUT "$report") with $(field DIRTY_FILES "$report") uncommitted file(s), console session $(field CONSOLE_SESSION "$report")" >&2
    echo "REMOTE_LANE=$verdict"
    grep -E '^(FREE_RAM_GB|CHECKOUT|DIRTY_FILES|CONSOLE_SESSION)=' <<<"$report"
    echo "UNITY_CLAIMS=$claims"
    ;;

disable)
    reason="$(tr -s '[:space:]' ' ' <<<"${*:-no reason given}" | sed 's/ $//')"
    mkdir -p "$STATE_DIR"
    printf '%s\n' "$reason" > "$MARKER_HERE"
    echo "[remote_lane] $HOST: disabled here — $reason" >&2
    echo "REMOTE_LANE_SWITCH=disabled"
    ;;

enable)
    rm -f "$MARKER_HERE"
    there="$(box <<EOF
if (Test-Path '$MARKER_THERE') { Remove-Item '$MARKER_THERE' -ErrorAction Stop; 'REMOTE_MARKER=cleared' } else { 'REMOTE_MARKER=absent' }
EOF
)" || there=""
    there="$(grep -Ex 'REMOTE_MARKER=(cleared|absent)' <<<"$there" || echo "REMOTE_MARKER=unchecked")"
    if [ "$there" = "REMOTE_MARKER=unchecked" ]; then
        echo "[remote_lane] $HOST: enabled here; the box did not answer, so a marker set at its keyboard would still disable the lane" >&2
    else
        echo "[remote_lane] $HOST: enabled" >&2
    fi
    echo "REMOTE_LANE_SWITCH=enabled"
    echo "$there"
    ;;

*)
    echo "[remote_lane] unknown action '$ACTION' (status|disable|enable)" >&2
    exit 2
    ;;
esac
