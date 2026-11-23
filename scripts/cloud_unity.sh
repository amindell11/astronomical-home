#!/usr/bin/env bash
#
# Run this project's Unity tests inside a Claude cloud session (Linux, Docker, no GPU).
# Syncs a copy of the checkout into a work dir, applies the FMOD stub to the copy, claims the
# account's Personal seat, runs the suite in the unityci editor image and returns the seat.
# The container reports the same machine id as the hosted suite's runner, so both share one
# seat: a hosted run that returns it mid-run here ends this run with no_results.
# The checkout's tracked files are never written; only the light LFS set is pulled, so
# RequiresLfsPng tests cannot run here.
#
# Usage: cloud_unity.sh pull
#            Start dockerd if it is down (detached; log in the work dir), pull the editor image.
#        cloud_unity.sh test --mode EditMode|PlayMode [--category <filter>] [--graphics] [--out <dir>]
#            --category  NUnit category filter (default '!RequiresGraphics;!RequiresLfsPng')
#            --graphics  drop -nographics and wrap the editor in xvfb-run (software GL)
#            --out       results dir (default <repo>/results/unity-tests-agent)
#
# Env:   UNITY_EMAIL, UNITY_PASSWORD   required by `test`; passed to docker by name, and both
#                                      values are masked out of every log before it is written
#        CLOUD_UNITY_WORK              work dir (default $HOME/.cache/cloud-unity); must lie outside
#                                      the checkout. Holds proj/ (the synced copy with its Library),
#                                      home/ (the container's home) and dockerd.log
#        CLOUD_UNITY_CA                proxy CA bundle mounted into the container
#                                      (default /root/.ccr/ca-bundle.crt)
#        HTTPS_PROXY HTTP_PROXY NO_PROXY and lowercase forms are passed through when set
#
# Files: <out>/<mode>-results.xml  NUnit results    <out>/<mode>.log  masked editor log
#        <out>/claim-seat.log, <out>/return-seat.log   masked licensing logs
#
# Stdout (machine channel; prose goes to stderr):
#   pull:  IMAGE=<ref>
#   test:  MODE= VERDICT= TOTAL= PASSED= FAILED= SKIPPED= RESULTS= LOG=   then SEAT_RETURN=ok|failed
#          VERDICT: passed | failed | empty (results exist, total 0) | no_results (counts are empty)
#          SEAT_RETURN is printed whenever the seat was claimed; a failed return never changes the exit code.
#
# Exit:  0 passed (pull: image ready)  1 tests failed
#        2 the run could not judge: bad arguments, missing credentials or tools, Docker down,
#          a preparation step failed, the seat was refused, no results, or an empty selection

set -Eeuo pipefail
trap 'rc=$?; echo "[cloud_unity] step failed (exit $rc): $BASH_COMMAND" >&2; exit 2' ERR
trap 'exit 2' INT TERM

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${CLOUD_UNITY_WORK:-$HOME/.cache/cloud-unity}"
CA="${CLOUD_UNITY_CA:-/root/.ccr/ca-bundle.crt}"
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/src/Asteroids3D/ProjectSettings/ProjectVersion.txt")"
IMAGE="unityci/editor:ubuntu-$VERSION-base-3"
LFS_INCLUDE='src/Asteroids3D/Assets/**/*.fbx,src/Asteroids3D/Assets/**/*.FBX,src/Asteroids3D/Assets/**/*.obj,src/Asteroids3D/Assets/**/*.onnx,src/Asteroids3D/Assets/Resources/RunRecap/Ships/*.png'

say() { echo "[cloud_unity] $*" >&2; }
die() { say "$*"; exit 2; }
usage() { sed -n '11,17p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

ensure_docker() {
    docker info >/dev/null 2>&1 && return
    mkdir -p "$WORK"
    say "dockerd is down; starting it (log: $WORK/dockerd.log)"
    # Detached: a session worker restart kills a daemon attached to this shell.
    setsid nohup dockerd >"$WORK/dockerd.log" 2>&1 </dev/null &
    for _ in $(seq 60); do
        docker info >/dev/null 2>&1 && return
        sleep 1
    done
    die "dockerd did not answer within 60 s; see $WORK/dockerd.log"
}

sync_project() {
    say "pulling the light LFS set"
    git -C "$ROOT" lfs pull --include="$LFS_INCLUDE" --exclude="" >&2
    say "syncing src/ training/ audio/ into $WORK/proj"
    mkdir -p "$WORK/proj" "$WORK/home"
    rsync -a --delete \
        --exclude=/src/Asteroids3D/Library/ --exclude=/src/Asteroids3D/Temp/ \
        --exclude=/src/Asteroids3D/Logs/ --exclude=/src/Asteroids3D/obj/ \
        "$ROOT/src" "$ROOT/training" "$ROOT/audio" "$WORK/proj/"
    local m="$WORK/proj/src/Asteroids3D/Packages/manifest.json"
    local l="$WORK/proj/src/Asteroids3D/Packages/packages-lock.json"
    jq '.dependencies["com.fmod.fmod-for-unity.v204"] = "file:../../fmod-stub"' "$m" > "$m.tmp" && mv "$m.tmp" "$m"
    jq 'del(.dependencies["com.fmod.fmod-for-unity.v204"])' "$l" > "$l.tmp" && mv "$l.tmp" "$l"
}

mask() {
    perl -pe 'BEGIN{$p=quotemeta($ENV{UNITY_PASSWORD}); $e=quotemeta($ENV{UNITY_EMAIL})} s/$p/***/g; s/$e/***/g'
}

# in_container <log> <bash snippet> [args...]: credentials are named only inside the container.
in_container() {
    local log=$1 snippet=$2; shift 2
    docker run --rm --network host \
        -v "$WORK:/work" -v "$WORK/home:/root" -v "$OUT:/out" \
        -v "$CA:/usr/local/share/ca-certificates/ccr.crt:ro" \
        -e UNITY_EMAIL -e UNITY_PASSWORD \
        -e HTTPS_PROXY -e HTTP_PROXY -e NO_PROXY -e https_proxy -e http_proxy -e no_proxy \
        -e NODE_EXTRA_CA_CERTS=/usr/local/share/ca-certificates/ccr.crt \
        -e SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt \
        "$IMAGE" bash -c "update-ca-certificates >/dev/null 2>&1 && $snippet" _ "$@" \
        2>&1 | mask >"$log"
}

# The editor's own -username/-password sign-in never claims a Personal seat; the client does.
claim_seat() {
    say "claiming the Personal seat"
    in_container "$OUT/claim-seat.log" \
        '/opt/unity/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"' \
        || die "the licensing client refused the sign-in or the seat; see $OUT/claim-seat.log"
}

return_seat() {
    say "returning the seat"
    in_container "$OUT/return-seat.log" \
        'exec unity-editor -batchmode -nographics -quit -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -logFile -' || true
    if grep -q 'Successfully returned the entitlement license' "$OUT/return-seat.log"; then
        echo "SEAT_RETURN=ok"
    else
        say "the seat was not returned; see $OUT/return-seat.log"
        echo "SEAT_RETURN=failed"
    fi
}

attr() {
    local v
    v=$(sed -n "s/.* $1=\"\([0-9][0-9]*\)\".*/\1/p" <<<"$RUN_TAG")
    [ -n "$v" ] || die "the results' <test-run> has no $1 count"
    echo "$v"
}

cmd_test() {
    local mode="" category='!RequiresGraphics;!RequiresLfsPng' graphics=0
    OUT="$ROOT/results/unity-tests-agent"
    while [ $# -gt 0 ]; do
        case "$1" in
            --mode|--category|--out) [ $# -ge 2 ] || die "$1 needs a value"
                case "$1" in --mode) mode=$2 ;; --category) category=$2 ;; --out) OUT=$2 ;; esac
                shift 2 ;;
            --graphics) graphics=1; shift ;;
            *) say "unknown argument: $1"; usage ;;
        esac
    done
    case "$mode" in EditMode|PlayMode) ;; *) die "--mode must be EditMode or PlayMode" ;; esac
    [ -n "${UNITY_EMAIL-}" ] || die "UNITY_EMAIL is not set"
    [ -n "${UNITY_PASSWORD-}" ] || die "UNITY_PASSWORD is not set"
    [ -f "$CA" ] || die "CA bundle not found: $CA (set CLOUD_UNITY_CA)"
    for tool in docker dockerd git-lfs rsync jq perl; do
        command -v "$tool" >/dev/null || die "required tool not found: $tool"
    done
    WORK="$(realpath -m "$WORK")"
    OUT="$(realpath -m "$OUT")"
    case "$WORK/" in "$(realpath "$ROOT")"/*) die "CLOUD_UNITY_WORK must lie outside the checkout: $WORK" ;; esac
    mkdir -p "$WORK" "$OUT"

    ensure_docker
    sync_project

    local results="$OUT/$mode-results.xml" log="$OUT/$mode.log" rc=0
    local -a editor=(unity-editor -batchmode -nographics)
    [ "$graphics" -eq 0 ] || editor=(xvfb-run -a -s '-screen 0 1920x1080x24' unity-editor -batchmode)
    rm -f "$results"
    claim_seat
    trap return_seat EXIT
    say "running $mode tests (category '$category'); log: $log"
    in_container "$log" 'exec "$@" -logFile -' "${editor[@]}" -projectPath /work/proj/src/Asteroids3D \
        -runTests -testPlatform "$mode" -testCategory "$category" -testResults "/out/$mode-results.xml" || rc=$?
    say "editor exited $rc"

    local verdict total="" passed="" failed="" skipped="" code
    if [ -f "$results" ] && RUN_TAG="$(grep -o -m1 '<test-run [^>]*' "$results")"; then
        total=$(attr total); passed=$(attr passed); failed=$(attr failed); skipped=$(attr skipped)
        if [ "$total" -eq 0 ]; then verdict=empty; code=2
        elif [ "$failed" -gt 0 ]; then verdict=failed; code=1
        else verdict=passed; code=0; fi
    else
        verdict=no_results; code=2
        say "no results at $results; see $log"
    fi
    printf '%s\n' "MODE=$mode" "VERDICT=$verdict" "TOTAL=$total" "PASSED=$passed" "FAILED=$failed" \
        "SKIPPED=$skipped" "RESULTS=$results" "LOG=$log"
    exit "$code"
}

case "${1-}" in
    pull)
        [ $# -eq 1 ] || usage
        ensure_docker
        docker pull "$IMAGE" >&2
        echo "IMAGE=$IMAGE" ;;
    test) shift; cmd_test "$@" ;;
    *) usage ;;
esac
