#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh scripts/drain_pick.sh

# Regression for 'land-check <pr>': the verdict GitHub auto-merge waits on. A green head with
# nothing owed passes whatever its paths and whether or not it contains main; an unticked owed
# item waits unless a recorded instruction covers the head; a red proof, an unresolved thread and
# a draft each get the right STATE and CHECK; a head that moved under the check gives no verdict.
# The real drain_pick.sh reads the PR's facts; gh is a stub that fails closed on any call it does
# not model and never merges.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix" ORIGIN="$TMP/origin.git" PRIMARY="$TMP/primary" GH_LOG="$TMP/gh.log"
export GIT_AUTHOR_NAME="Pool Test" GIT_AUTHOR_EMAIL=pool-test@example.test
export GIT_COMMITTER_NAME="Pool Test" GIT_COMMITTER_EMAIL=pool-test@example.test
mkdir -p "$FIX" "$TMP/bin"
: > "$GH_LOG"

# PR <n>'s head branch is task/p<n>; $FIX/head-<n> overrides the head GitHub reports.
# $FIX/hosted is the headless verdict for any commit: green or failure.
cat > "$TMP/bin/gh" <<'EOF2'
#!/usr/bin/env bash
URL=https://github.com/pool-test/repo/actions/runs
args="$*"
case "$1 $2" in
  "pr view")
    if [[ "$args" == *headRefOid* ]]; then
      cat "$FIX/head-$3" 2>/dev/null || git --git-dir="$ORIGIN" rev-parse "refs/heads/task/p$3"
    else
      cat "$FIX/view-$3" 2>/dev/null || printf 'OPEN\tmain\ttask/p%s\tfalse\tfalse\n' "$3"
    fi ;;
  "pr edit") echo "$args" >> "$GH_LOG" ;;
  "api graphql")
    [[ "$args" == *"pullRequest(number"* ]] || { echo "gh stub: unmodelled call: $args" >&2; exit 97; }
    n="${args#*number=}"; cat "$FIX/facts-${n%% *}.json" ;;
  "api repos/pool-test/repo/commits/"*)
    sha="${2#repos/pool-test/repo/commits/}"; sha="${sha%%/*}"
    tree="$(git --git-dir="$ORIGIN" rev-parse "$sha^{tree}")"
    if [[ "$args" == *merge-proof/resharper* ]]; then
      printf 'success\037tree=%s baseTree=%s files=1 unity=0 blocking=0\037%s/98\n' "$tree" "$(git --git-dir="$ORIGIN" rev-parse 'main^{tree}')" "$URL"
    elif [[ "$(cat "$FIX/hosted")" == green ]]; then
      printf 'success\037tree=%s total=5 passed=5 skipped=0\037%s/98\n' "$tree" "$URL"
    else
      printf 'failure\037headless suite failed - see run\037%s/97\n' "$URL"
    fi ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF2
# No real normalizer: a C# delta reads as code, as it would with powershell missing.
printf '#!/usr/bin/env bash\nexit 1\n' > "$TMP/bin/powershell.exe"
chmod +x "$TMP/bin/gh" "$TMP/bin/powershell.exe"
export PATH="$TMP/bin:$PATH"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

git init -q --bare -b main "$ORIGIN"
git clone -q "$ORIGIN" "$PRIMARY"
git -C "$PRIMARY" config remote.origin.url "https://github.com/pool-test/repo.git"
git -C "$PRIMARY" config "url.$ORIGIN.insteadOf" "https://github.com/pool-test/repo.git"
echo base > "$PRIMARY/README.md"
git -C "$PRIMARY" add -A
git -C "$PRIMARY" commit -qm init
git -C "$PRIMARY" push -q origin main
git clone -q "$ORIGIN" "$FIX/mover"
cd "$PRIMARY"

NONE=$'## Test status\n\n### Owed local\n\nNone.\n'
pr_branch() {
  local n="$1" edit
  shift
  git -C "$PRIMARY" fetch -q origin
  git -C "$PRIMARY" checkout -q -B "task/p$n" origin/main
  for edit in "$@"; do
    mkdir -p "$(dirname "$PRIMARY/${edit%%=*}")"
    printf '%b' "${edit#*=}" > "$PRIMARY/${edit%%=*}"
    git -C "$PRIMARY" add -A
    git -C "$PRIMARY" commit -qm "p$n: ${edit%%=*}"
  done
  git -C "$PRIMARY" push -q origin "task/p$n"
  git -C "$PRIMARY" checkout -q --detach
}
head_of() { git -C "$PRIMARY" rev-parse "task/p$1${2:-}"; }
move_main() {
  git -C "$FIX/mover" fetch -q origin && git -C "$FIX/mover" reset -q --hard origin/main
  echo "$RANDOM" > "$FIX/mover/moved-$RANDOM.txt"
  git -C "$FIX/mover" add -A && git -C "$FIX/mover" commit -qm "main moves" && git -C "$FIX/mover" push -q origin HEAD:main
}
# facts <n> <instruction sha|-> <reviewed sha|-> [body]
facts() {
  python3 - "$FIX/facts-$1.json" "$1" "$2" "$3" "${4-}" \
    $(git -C "$PRIMARY" rev-list "origin/main..task/p$1") <<'PY'
import json, sys
out, n, instruction, reviewed, body = sys.argv[1:6]
commits = sys.argv[6:]
comments = []
if instruction != "-":
    comments.append({"author": {"login": "amindell11"}, "body": f"Merge instruction 2026-10-03: `{instruction}`"})
if reviewed != "-":
    comments.append({"author": {"login": "chatgpt-codex-connector"}, "body":
        "<!-- codex-pull-request-review-summary -->\n| Review | Status | Commit | Review trigger |\n| --- | --- | --- | --- |\n"
        f"| **Code Review** | **Completed** | `{reviewed[:7]}` | PR opened |\n"})
pr = {"number": int(n), "title": "t", "isDraft": False, "headRefOid": commits[0], "body": body, "changedFiles": 1,
      "files": {"nodes": []}, "commits": {"nodes": [{"commit": {"status": None}}]},
      "history": {"nodes": [{"commit": {"oid": c}} for c in reversed(commits)]},
      "comments": {"nodes": comments}, "reviewThreads": {"totalCount": 0, "nodes": []},
      "reactions": {"nodes": []}}
json.dump({"data": {"repository": {"pullRequest": pr, "pullRequests": {"nodes": [{"number": int(n)}]}}}},
          open(out, "w"))
PY
}
check() {
  rc=0
  bash "$POOL" land-check "$1" > "$TMP/out" 2> "$TMP/err" || rc=$?
}
show() { echo "--- stdout:"; cat "$TMP/out"; echo "--- stderr:"; cat "$TMP/err"; } >&2
field() { sed -n "s/^$1=//p" "$TMP/out"; }
expect() {
  local state="$1" reason="$2" why="$3" want_rc=1
  [[ "$state" == success ]] && want_rc=0
  [[ "$rc" -eq "$want_rc" && "$(field STATE)" == "$state" && "$(field CHECK)" == "$reason" ]] \
    || { show; fail "$why: STATE=$state CHECK=$reason, exit $want_rc (rc=$rc)"; }
  [[ "$(field SHA)" == "$(cat "$FIX/head-$n" 2>/dev/null || git --git-dir="$ORIGIN" rev-parse "refs/heads/task/p$n")" ]] \
    || { show; fail "$why: SHA names the head GitHub reports"; }
}
labels() { grep -c "^pr edit $1 --add-label auto-merged$" "$GH_LOG" || true; }
echo green > "$FIX/hosted"
OWED=$'## Test status\n\n### Owed local\n\n- [ ] eyes: the hangar looks right\n'

n=1; pr_branch 1 'a.txt=one\n'
facts 1 - - "$NONE"
check 1
expect success green "a green head with nothing owed, no instruction and no review"

n=2; pr_branch 2 'b.txt=two\n'
facts 2 - - "$OWED"
check 2
expect pending owed-open "an unticked owed item with no instruction"

n=3; pr_branch 3 'c.txt=three\n'
facts 3 "$(head_of 3)" - "$OWED"
check 3
expect success instruction "an unticked owed item with an instruction on the head"

n=4; pr_branch 4 'd.txt=four\n' 'README.md=reworded\n'
facts 4 "$(head_of 4 "~1")" - "$OWED"
check 4
expect success instruction "an instruction covering through a doc-only delta"

n=5; pr_branch 5 'e.txt=five\n' 'e.txt=changed\n'
facts 5 "$(head_of 5 "~1")" - "$OWED"
check 5
expect pending owed-open "an instruction behind a code delta does not cover the head"

n=6; pr_branch 6 'f.txt=six\n'
facts 6 - - "$NONE"
echo failure > "$FIX/hosted"
check 6
echo green > "$FIX/hosted"
expect pending hosted "a red proof status"

n=7; pr_branch 7 '.github/workflows/x.yml=on: push\n' 'scripts/x.sh=echo\n' '.githooks/pre-push=echo\n'
facts 7 - - "$NONE"
check 7
expect success green "a landing diff touching .github/, scripts/ and .githooks/"

n=8; pr_branch 8 'h.txt=eight\n'
facts 8 - - "$NONE"
printf 'OPEN\tmain\ttask/p8\ttrue\tfalse\n' > "$FIX/view-8"
check 8
rm -f "$FIX/view-8"
expect failure draft "a draft"

n=9; pr_branch 9 'i.txt=nine\n'
facts 9 - - "$NONE"
move_main
check 9
expect success green "a head that does not contain main"

n=10; pr_branch 10 'j.txt=ten\n'
facts 10 - - "$NONE"
git -C "$PRIMARY" rev-parse origin/main > "$FIX/head-10"
check 10
[[ "$rc" -eq 1 && -z "$(field STATE)" ]] || { show; fail "a head that moved under the check gives no verdict"; }

n=11; pr_branch 11 'k.txt=eleven\n'
facts 11 - - "$NONE"
python3 - "$FIX/facts-11.json" <<'PY2'
import json, sys
d = json.load(open(sys.argv[1]))
d["data"]["repository"]["pullRequest"]["reviewThreads"] = {"totalCount": 1, "nodes": [{"isResolved": False}]}
json.dump(d, open(sys.argv[1], "w"))
PY2
check 11
expect pending unresolved "an unresolved review thread"

n=12; pr_branch 12 'l.txt=twelve\n'
facts 12 - - "$NONE"
printf 'OPEN\tmain\ttask/p12\tfalse\ttrue\n' > "$FIX/view-12"
check 12
rm -f "$FIX/view-12"
expect failure fork "a head from a fork"

[[ ! -s "$GH_LOG" ]] || { cat "$GH_LOG" >&2; fail "land-check edits no PR"; }

echo "PASS test_pool_land_check.sh"
