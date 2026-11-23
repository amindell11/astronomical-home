#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/landing_queue.sh

# Regression for the landing queue: which PR it updates, what it does to the front PR, and reset.
# gh is a stub over fixture JSON that applies the script's own --jq filters with jq, logs every
# write, and fails closed on any call it does not model.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
QUEUE="$SCRIPT_DIR/../landing_queue.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix" GH_LOG="$TMP/gh.log" GITHUB_REPOSITORY=o/r
mkdir -p "$FIX" "$TMP/bin"

cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
jqf="" args=()
while [[ $# -gt 0 ]]; do
  case "$1" in --jq) jqf="$2"; shift 2 ;; --paginate) shift ;; *) args+=("$1"); shift ;; esac
done
set -- "${args[@]}"
out() { if [[ -n "$jqf" ]]; then jq -r "$jqf" "$1"; else cat "$1"; fi; }
case "$1 $2" in
  "label create") echo "$*" >> "$GH_LOG" ;;
  "pr list") out "$FIX/prs.json" ;;
  "pr view") out "$FIX/view-$3.json" ;;
  "pr edit"|"pr comment"|"workflow run") echo "$*" >> "$GH_LOG" ;;
  "api repos/o/r/commits/"*) sha="${2#repos/o/r/commits/}"; out "$FIX/status-${sha%%/*}.json" ;;
  "api repos/o/r/issues/"*) out "$FIX/events.json" ;;
  "api -X")
    echo "$*" >> "$GH_LOG"
    n="${4#repos/o/r/pulls/}"; n="${n%%/*}"
    if [[ -f "$FIX/conflict-$n" ]]; then echo "gh: merge conflict between base and head (HTTP 422)" >&2; exit 1; fi
    echo "moved" > "$FIX/moved-$n" ;;
  "api repos/o/r/pulls/"*)
    n="${2#repos/o/r/pulls/}"
    if [[ -f "$FIX/moved-$n" ]]; then echo "new$n"; else echo "sha$n"; fi ;;
  *) echo "gh stub: unmodelled call: $*" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"

# pr <n> <mergeStateStatus> <labels,comma> [auto|noauto] [draft] [branch]
PRS=()
pr() { PRS+=("$1|$2|${3:-}|${4:-auto}|${5:-}|${6:-task/p$1}"); }
write_prs() {
  python3 - "$FIX/prs.json" "${PRS[@]}" <<'PY'
import json, sys
rows = []
for spec in sys.argv[2:]:
    n, state, labels, auto, draft, branch = spec.split("|")
    rows.append({"number": int(n), "headRefName": branch, "headRefOid": f"sha{n}", "isDraft": draft == "draft",
                 "isCrossRepository": False, "autoMergeRequest": {"mergeMethod": "SQUASH"} if auto == "auto" else None,
                 "mergeStateStatus": state, "labels": [{"name": l} for l in labels.split(",") if l]})
json.dump(rows, open(sys.argv[1], "w"))
PY
}
# st <sha> <context>=<state>:<description> ...
st() {
  local sha="$1"; shift
  python3 - "$FIX/status-$sha.json" "$@" <<'PY'
import json, sys
out = [{"context": c.split("=", 1)[0], "state": c.split("=", 1)[1].split(":", 1)[0],
        "description": c.split(":", 1)[1] if ":" in c else ""} for c in sys.argv[2:]]
json.dump({"statuses": out}, open(sys.argv[1], "w"))
PY
}
events() { printf '[{"event":"labeled","label":{"name":"queue:updating"},"created_at":"%s"}]' "$1" > "$FIX/events.json"; }
reset_fix() { rm -f "$FIX"/*; : > "$GH_LOG"; PRS=(); events "$(date -u +%FT%TZ)"; }
run() { rc=0; bash "$QUEUE" "$@" > "$TMP/out" 2> "$TMP/err" || rc=$?; }
show() { echo "--- out:"; cat "$TMP/out"; echo "--- err:"; cat "$TMP/err"; echo "--- gh:"; grep -v '^label create' "$GH_LOG"; } >&2
last() { tail -n 1 "$TMP/out"; }
writes() { grep -v '^label create' "$GH_LOG" || true; }

# Nothing queued.
reset_fix; write_prs; run step
[[ "$rc" -eq 0 && "$(last)" == QUEUE=idle && -z "$(writes)" ]] || { show; fail "an empty queue is idle and writes nothing"; }

# The pick: the lowest BEHIND member whose land-check is success or pending behind.
reset_fix
pr 3 BEHIND "" noauto; pr 4 BEHIND "" auto draft; pr 5 BEHIND "" auto "" claude/x
pr 6 BEHIND; pr 7 BEHIND queue:stuck; pr 8 CLEAN; pr 9 BEHIND; pr 10 BEHIND
write_prs
st sha6 "land-check=pending:unauthorized"; st sha7 "land-check=success:class"; st sha9 "land-check=pending:behind"; st sha10 "land-check=success:class"
run step
[[ "$rc" -eq 0 && "$(last)" == QUEUE=updated:9 ]] || { show; fail "picks #9: no auto-merge, drafts, non-task heads, an unready land-check and stuck PRs are skipped"; }
want="api -X PUT repos/o/r/pulls/9/update-branch -f expected_head_sha=sha9
workflow run headless-suite.yml --repo o/r --ref task/p9
pr edit 9 --repo o/r --add-label queue:updating"
[[ "$(writes)" == "$want" ]] || { show; fail "updates the branch, then dispatches the suite on it, then labels it"; }

# A conflict labels and comments, and the next candidate is tried.
reset_fix; pr 9 BEHIND; pr 10 BEHIND; write_prs
st sha9 "land-check=success:instruction"; st sha10 "land-check=success:class"; touch "$FIX/conflict-9"
run step
[[ "$rc" -eq 0 && "$(head -n 1 "$TMP/out")" == QUEUE=conflict:9 && "$(last)" == QUEUE=updated:10 ]] || { show; fail "a conflicting update moves on to the next PR"; }
grep -q "^pr edit 9 --repo o/r --add-label queue:conflict$" "$GH_LOG" && grep -q "^pr comment 9 " "$GH_LOG" \
  || { show; fail "a conflict is labelled and commented"; }

# The front waits while its suite runs, and nothing else moves.
reset_fix; pr 9 BLOCKED queue:updating; pr 10 BEHIND; write_prs
st sha9 "merge-proof/headless=pending:running"; st sha10 "land-check=success:class"
run step
[[ "$(last)" == QUEUE=wait:9 && -z "$(writes)" ]] || { show; fail "a front with its suite running waits"; }

# Suite green, no land-check pass yet: land-check is dispatched for it.
reset_fix; pr 9 BLOCKED queue:updating; write_prs
st sha9 "merge-proof/headless=success:tree=x" "land-check=pending:behind"
run step
[[ "$(last)" == QUEUE=landcheck:9 && "$(writes)" == "workflow run land-check.yml --repo o/r --ref main -f pr=9" ]] \
  || { show; fail "a green front without a land-check pass gets land-check dispatched"; }

# A red suite sticks the front, and the next PR is not touched this pass.
reset_fix; pr 9 BLOCKED queue:updating; pr 10 BEHIND; write_prs
st sha9 "merge-proof/headless=failure:failed"; st sha10 "land-check=success:class"
run step
[[ "$(last)" == QUEUE=stuck:9 ]] || { show; fail "a red suite on the front sticks it"; }
want="pr edit 9 --repo o/r --remove-label queue:updating
pr edit 9 --repo o/r --add-label queue:stuck"
[[ "$(writes | grep -v '^pr comment')" == "$want" ]] || { show; fail "stuck swaps the labels"; }

# A front past the timeout sticks.
reset_fix; pr 9 BLOCKED queue:updating; write_prs; events "2000-01-01T00:00:00Z"
st sha9 "merge-proof/headless=success:tree=x" "land-check=success:instruction"
run step
[[ "$(last)" == QUEUE=stuck:9 ]] || { show; fail "a front that has not landed by the timeout sticks"; }

# A front that went behind again loses the label and the pick runs.
reset_fix; pr 9 BEHIND queue:updating; write_prs
st sha9 "merge-proof/headless=success:tree=x" "land-check=success:instruction"
run step
[[ "$(last)" == QUEUE=updated:9 ]] || { show; fail "a front behind again is picked afresh"; }
[[ "$(writes | head -n 1)" == "pr edit 9 --repo o/r --remove-label queue:updating" ]] || { show; fail "its label comes off first"; }

# reset takes off only the queue's labels the PR carries.
reset_fix; echo '{"labels":[{"name":"queue:stuck"},{"name":"bug"}]}' > "$FIX/view-9.json"
run reset 9
[[ "$(last)" == QUEUE=reset:9 && "$(writes)" == "pr edit 9 --repo o/r --remove-label queue:stuck" ]] || { show; fail "reset removes queue labels only"; }

run step extra
[[ "$rc" -eq 2 ]] || fail "usage error exits 2"

echo "PASS test_landing_queue.sh"
