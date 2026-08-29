#!/usr/bin/env bash
#
# Launch, drive, and tear down a graphical Unity editor on a remote lane
# machine over SSH, driven via the unity CLI. Editor-lane sibling of
# remote_gate.sh (which owns branch/LFS sync — this script opens whatever
# is checked out remotely).
#
# A GUI editor MUST land in the remote interactive desktop session:
# SSH children (and WMI Create) run in session 0, where the editor stalls
# before project load on an invisible modal. The launch therefore rides an
# interactive scheduled task (/IT), which requires a user logged in at the
# remote console. The task runs elevated, so git needs the repo in
# safe.directory (ensured idempotently here).
#
# Usage: remote_editor.sh start [lease]     launch + wait until CLI-ready
#        remote_editor.sh status [lease]    editor_status passthrough
#        remote_editor.sh cmd <args...>     unity CLI passthrough (quoting handled)
#        remote_editor.sh stop [lease]      release lease, close editor, clean up
# Env:   REMOTE_EDITOR_HOST (alastor) · REMOTE_EDITOR_REPO (C:/dev/astronomical-home)
#        REMOTE_EDITOR_UNITY (6000.1.8f1 exe) · REMOTE_EDITOR_TIMEOUT_SEC (600)

set -euo pipefail

HOST="${REMOTE_EDITOR_HOST:-alastor}"
RREPO="${REMOTE_EDITOR_REPO:-C:/dev/astronomical-home}"
RUNITY="${REMOTE_EDITOR_UNITY:-C:\\Program Files\\Unity\\Hub\\Editor\\6000.1.8f1\\Editor\\Unity.exe}"
TIMEOUT_SEC="${REMOTE_EDITOR_TIMEOUT_SEC:-600}"

RPROJ="$RREPO/src/Asteroids3D"
RPROJ_WIN="${RPROJ//\//\\}"
RCLI='$env:LOCALAPPDATA\Unity\bin\unity.exe'

ACTION="${1:-}"
[ -n "$ACTION" ] || { sed -n '3,20p' "$0"; exit 1; }
shift

rssh() { ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST" "$@" | tr -d '\r'; }

require_host() {
    rssh "echo up" >/dev/null 2>&1 || {
        echo "[remote_editor] $HOST unreachable over SSH — the box is likely asleep (no WoL); wake it physically." >&2
        exit 3
    }
}

cli_cmd() { # caller pre-quotes any arg that can contain spaces (see psq)
    rssh "& \"$RCLI\" command $* --project-path '$RPROJ_WIN'"
}

# Single-quote args for the remote PowerShell parse (embedded ' doubled).
psq() {
    local out="" a
    for a in "$@"; do out+=" '${a//\'/\'\'}'"; done
    printf '%s' "$out"
}

lease_paths() { # start and stop must agree on these remote artefact names
    TASK="RemoteEditor-$1"
    RLOG="C:/dev/remote_editor_$1.log"
    RLAUNCH="C:/dev/remote_editor_$1.ps1"
}

case "$ACTION" in

start)
    LEASE="${1:-remote-editor}"
    lease_paths "$LEASE"
    require_host

    # Preflight: an interactive desktop session must exist for the /IT task.
    if ! rssh "(Get-Process explorer -ErrorAction SilentlyContinue) -ne \$null" | grep -qi true; then
        echo "[remote_editor] no interactive desktop session on $HOST — log in at its console first (GUI editors cannot boot in session 0)." >&2
        exit 4
    fi

    # Preflight: unity CLI present remotely; install from the local copy if not.
    if ! rssh "Test-Path \"$RCLI\"" | grep -qi true; then
        local_cli="$(command -v unity || echo "${LOCALAPPDATA:-}/Unity/bin/unity.exe")"
        [ -f "$local_cli" ] || { echo "[remote_editor] unity CLI missing on $HOST and no local copy to ship" >&2; exit 5; }
        echo "[remote_editor] installing unity CLI on $HOST"
        rssh "New-Item -ItemType Directory -Force \$env:LOCALAPPDATA\\Unity\\bin | Out-Null"
        scp -q "$local_cli" "$HOST:AppData/Local/Unity/bin/unity.exe"
    fi

    # Preflight: the elevated task token trips git's dubious-ownership check.
    rssh "if (-not ((git config --global --get-all safe.directory 2>\$null) -contains '$RREPO')) { git config --global --add safe.directory '$RREPO' }"

    STAGE="$(mktemp -d)"
    trap 'rm -rf "$STAGE"' EXIT
    cat > "$STAGE/launch.ps1" <<EOF
\$log = '${RLOG//\//\\}'
"START \$(Get-Date -Format o) session=\$([System.Diagnostics.Process]::GetCurrentProcess().SessionId)" | Set-Content \$log
try {
    Set-Location '${RREPO//\//\\}'
    \$out = & .\\scripts\\unity_access.ps1 -Action StartEditor -Lease '$LEASE' -Slot main -Mode editor -WaitSeconds 240 -UnityPath '$RUNITY' -Json 2>&1
    \$out | Out-String -Width 4096 | Add-Content \$log
    "LAUNCHER_EXIT:\$LASTEXITCODE" | Add-Content \$log
} catch {
    "CAUGHT: \$(\$_ | Out-String)" | Add-Content \$log
}
EOF
    scp -q "$STAGE/launch.ps1" "$HOST:$RLAUNCH"
    rssh "schtasks /Create /F /IT /TN '$TASK' /TR 'powershell -NoProfile -ExecutionPolicy Bypass -File \"${RLAUNCH//\//\\}\"' /SC ONCE /ST 23:59 | Out-Null; schtasks /Run /TN '$TASK' | Out-Null; exit \$LASTEXITCODE" \
        || { echo "[remote_editor] schtasks could not create/run $TASK on $HOST" >&2; exit 7; }

    echo "[remote_editor] launch task fired; waiting for editor_status (timeout ${TIMEOUT_SEC}s)"
    deadline=$(( $(date +%s) + TIMEOUT_SEC ))
    while :; do
        log="$(rssh "Get-Content ${RLOG//\//\\} -ErrorAction SilentlyContinue" || true)"
        if grep -q "CAUGHT:\|LAUNCHER_EXIT:[1-9]" <<<"$log"; then
            echo "[remote_editor] launcher failed:" >&2
            echo "$log" >&2
            exit 6
        fi
        if cli_cmd editor_status 2>/dev/null | grep -q '"status":"ready"'; then
            break
        fi
        [ "$(date +%s)" -lt "$deadline" ] || {
            echo "[remote_editor] editor not ready after ${TIMEOUT_SEC}s (log: $HOST $RLOG)" >&2
            exit 2
        }
        sleep 10
    done

    # Unfocused editors need autotick or main-thread ops time out at 5 s.
    cli_cmd set_autotick --enable true >/dev/null
    cli_cmd "$(psq set_window_title --label "$LEASE")" >/dev/null
    echo "[remote_editor] ready — drive it with: $0 cmd <unity-command> [args]"
    cli_cmd editor_status
    ;;

status)
    require_host
    cli_cmd editor_status
    ;;

cmd)
    [ $# -ge 1 ] || { echo "[remote_editor] cmd needs a unity command name" >&2; exit 1; }
    require_host
    cli_cmd "$(psq "$@")"
    ;;

stop)
    LEASE="${1:-remote-editor}"
    lease_paths "$LEASE"
    require_host
    rssh "Set-Location '${RREPO//\//\\}'; & .\\scripts\\unity_access.ps1 -Action Release -Lease '$LEASE' -Slot main -CloseEditor -Json"
    rssh "schtasks /Delete /F /TN '$TASK' 2>\$null; Remove-Item '${RLAUNCH//\//\\}', '${RLOG//\//\\}' -ErrorAction SilentlyContinue; echo cleaned"
    ;;

*)
    echo "[remote_editor] unknown action '$ACTION' (start|status|cmd|stop)" >&2
    exit 1
    ;;
esac
