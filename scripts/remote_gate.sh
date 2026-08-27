#!/usr/bin/env bash
#
# Dispatch the Unity test gate to a remote lane machine over SSH.
# Ships the branch as a git bundle (no push — avoids LFS hooks reaching
# GitHub) plus an scp diff of missing LFS objects, launches
# unity_test_agent.ps1 detached via WMI (Start-Process dies with the SSH
# session), polls the run log, and pulls the summary JSON back.
#
# Usage: remote_gate.sh [branch]          (default: current branch)
# Env:   REMOTE_GATE_HOST (alastor) · REMOTE_GATE_REPO (C:/dev/astronomical-home)
#        REMOTE_GATE_UNITY · REMOTE_GATE_MODE (Both) · REMOTE_GATE_TIMEOUT_MIN (45)

set -euo pipefail

HOST="${REMOTE_GATE_HOST:-alastor}"
RREPO="${REMOTE_GATE_REPO:-C:/dev/astronomical-home}"
RUNITY="${REMOTE_GATE_UNITY:-C:\\Program Files\\Unity\\Hub\\Editor\\6000.1.8f1\\Editor\\Unity.exe}"
MODE="${REMOTE_GATE_MODE:-Both}"
TIMEOUT_MIN="${REMOTE_GATE_TIMEOUT_MIN:-45}"

ROOT="$(git rev-parse --show-toplevel)"
BRANCH="${1:-$(git -C "$ROOT" rev-parse --abbrev-ref HEAD)}"
SHA="$(git -C "$ROOT" rev-parse "$BRANCH")"
RUNID="rg-$(date +%Y%m%d-%H%M%S)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

rssh() { ssh "$HOST" "$@" | tr -d '\r'; }

echo "[remote_gate] $BRANCH@${SHA:0:8} -> $HOST:$RREPO ($RUNID)"

# --- 1. git objects -------------------------------------------------------
if rssh "git -C $RREPO cat-file -e $SHA; if (\$?) { echo have }" | grep -q have; then
    echo "[remote_gate] remote already has $SHA"
else
    REMOTE_MAIN="$(rssh "git -C $RREPO rev-parse main")"
    git -C "$ROOT" bundle create "$STAGE/br.bundle" "^$REMOTE_MAIN" "$BRANCH"
    scp -q "$STAGE/br.bundle" "$HOST:C:/dev/$RUNID.bundle"
    rssh "git -C $RREPO fetch C:/dev/$RUNID.bundle '$BRANCH'; Remove-Item C:\\dev\\$RUNID.bundle"
fi

# --- 2. LFS objects the remote lacks --------------------------------------
rssh "Get-ChildItem -Path $RREPO/.git/lfs/objects -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object Name" \
    | sort > "$STAGE/remote-oids"
missing=0
while read -r oid _; do
    if ! grep -qx "$oid" "$STAGE/remote-oids"; then
        src="$ROOT/.git/lfs/objects/${oid:0:2}/${oid:2:2}/$oid"
        [ -f "$src" ] || { echo "[remote_gate] LFS object $oid not in local cache" >&2; exit 1; }
        mkdir -p "$STAGE/lfs/${oid:0:2}/${oid:2:2}"
        cp "$src" "$STAGE/lfs/${oid:0:2}/${oid:2:2}/"
        missing=$((missing+1))
    fi
done < <(git -C "$ROOT" lfs ls-files -l "$SHA" | awk '{print $1}')
if [ "$missing" -gt 0 ]; then
    echo "[remote_gate] copying $missing LFS object(s)"
    scp -q -r "$STAGE/lfs/." "$HOST:$RREPO/.git/lfs/objects/"
fi

# --- 3. checkout + detached gate launch -----------------------------------
rssh "git -C $RREPO checkout -f --detach $SHA" >/dev/null
cat > "$STAGE/launch.ps1" <<EOF
Set-Location $RREPO
.\\scripts\\unity_test_agent.ps1 -Mode $MODE -UnityPath '$RUNITY' *> C:\\dev\\$RUNID.log
"LAUNCHER_EXIT:\$LASTEXITCODE" | Add-Content C:\\dev\\$RUNID.log
EOF
scp -q "$STAGE/launch.ps1" "$HOST:C:/dev/$RUNID.ps1"
rssh "\$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='powershell -NoProfile -ExecutionPolicy Bypass -File C:\\dev\\$RUNID.ps1'}; echo \"launched pid=\$(\$r.ProcessId) rc=\$(\$r.ReturnValue)\""

# --- 4. poll --------------------------------------------------------------
deadline=$(( $(date +%s) + TIMEOUT_MIN * 60 ))
while ! rssh "if (Select-String -Path C:\\dev\\$RUNID.log -Pattern 'LAUNCHER_EXIT' -Quiet -ErrorAction SilentlyContinue) { echo done }" | grep -q done; do
    [ "$(date +%s)" -lt "$deadline" ] || { echo "[remote_gate] timed out after ${TIMEOUT_MIN}m (log: $HOST C:/dev/$RUNID.log)" >&2; exit 2; }
    sleep 20
done

# --- 5. results -----------------------------------------------------------
STATUS_LINE="$(rssh "Get-Content C:\\dev\\$RUNID.log | Select-String 'STATUS=' | ForEach-Object Line" | tail -1)"
SUMMARY_RPATH="$(rssh "Get-Content C:\\dev\\$RUNID.log | Select-String 'UNITY_TEST_SUMMARY_JSON=' | ForEach-Object Line" | tail -1 | cut -d= -f2 | tr '\\' '/')"
mkdir -p "$ROOT/results/remote-gate"
SUMMARY_LOCAL="$ROOT/results/remote-gate/$RUNID-summary.json"
scp -q "$HOST:$SUMMARY_RPATH" "$SUMMARY_LOCAL"
rssh "Remove-Item C:\\dev\\$RUNID.ps1, C:\\dev\\$RUNID.log -ErrorAction SilentlyContinue"

echo "[remote_gate] $STATUS_LINE"
echo "[remote_gate] summary: $SUMMARY_LOCAL"
[[ "$STATUS_LINE" == *STATUS=passed* ]]
