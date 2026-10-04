#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh scripts/drain_pick.sh scripts/inert_diff.ps1

# Regression for 'land <pr>': every preflight refusal, slot borrowing (free slots only, a dead
# run's land-<pr> slot reused, another session's slot refused, a running gate's slot left alone),
# the authorize phase's covers relation, the shadow CLASS verdict, bounded retries of the
# transient refusals, and the slot reset after a refusal or finalized after a merge. The real
# drain_pick.sh reads the PR's facts; gh is a stub that fails closed on any call it does not
# model, answers the hosted statuses for whatever commit it is asked about, and squash-merges
# into the bare origin.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
pool() { bash "$POOL" "$@"; }
pool_fn() { (source "$POOL"; "$@"); }

TMP="$(mktemp -d)"
trap 'touch "$TMP/release"; rm -rf "$TMP" 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix" ORIGIN="$TMP/origin.git" PRIMARY="$TMP/primary"
export GH_MERGE_LOG="$TMP/gh-merge.log" GH_DISPATCH_LOG="$TMP/gh-dispatch.log"
export GIT_AUTHOR_NAME="Pool Test" GIT_AUTHOR_EMAIL=pool-test@example.test
export GIT_COMMITTER_NAME="Pool Test" GIT_COMMITTER_EMAIL=pool-test@example.test
mkdir -p "$FIX" "$TMP/bin"
: > "$GH_MERGE_LOG"
: > "$GH_DISPATCH_LOG"

# land never boots Unity or asks memory admission; inert_diff.ps1 reaches the real powershell.
cat > "$TMP/bin/powershell.exe" <<'EOF'
#!/usr/bin/env bash
if [[ "$*" == *unity_test_agent.ps1* || "$*" == *resharper_ratchet.ps1* || "$*" == *BootAdmission* ]]; then
  echo "powershell stub: land must not run: $*" >&2
  exit 97
fi
real="$(type -pa powershell.exe | grep -vF "$(dirname "$0")" | head -n 1)"
exec "$real" "$@"
EOF

# PR <n>'s head branch is task/p<n>. $FIX/hosted is the hosted run's verdict for any commit:
# green, error or failure; a dispatch turns it green. $FIX/move-base, when present, moves main on
# the next status read (and is consumed unless $FIX/move-always exists).
cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
URL=https://github.com/pool-test/repo/actions/runs
args="$*"
move_base() {
  git -C "$FIX/mover" fetch -q origin && git -C "$FIX/mover" reset -q --hard origin/main
  echo "$RANDOM$RANDOM" > "$FIX/mover/moved-$RANDOM.txt"
  git -C "$FIX/mover" add -A && git -C "$FIX/mover" commit -qm "main moves" && git -C "$FIX/mover" push -q origin HEAD:main
}
case "$1 $2" in
  "pr view") cat "$FIX/view-$3" 2>/dev/null || printf 'OPEN\tmain\ttask/p%s\tfalse\tfalse\n' "$3" ;;
  "pr list")
    n="${args##*--head task/p}"; n="${n%% *}"
    if [[ "$args" == *"--state all"* ]]; then cat "$FIX/merged-$n"; else [[ -e "$FIX/merged-$n" ]] || echo "$n"; fi ;;
  "pr merge")
    echo "$args" >> "$GH_MERGE_LOG"
    want="${args##*--match-head-commit }"; want="${want%% *}"
    tip="$(git --git-dir="$ORIGIN" rev-parse "refs/heads/task/p$3")"
    [[ "$tip" == "$want" ]] || { echo "gh stub: head is $tip, not $want" >&2; exit 1; }
    merge="$(git --git-dir="$ORIGIN" commit-tree "$tip^{tree}" -p "$(git --git-dir="$ORIGIN" rev-parse refs/heads/main)" -m "squash #$3")"
    git --git-dir="$ORIGIN" update-ref refs/heads/main "$merge"
    printf 'MERGED\t%s\t%s\n' "$tip" "$merge" > "$FIX/merged-$3" ;;
  "api graphql")
    [[ "$args" == *"pullRequest(number"* ]] || { echo "gh stub: unmodelled call: $args" >&2; exit 97; }
    n="${args#*number=}"; cat "$FIX/facts-${n%% *}.json" ;;
  "api repos/pool-test/repo/commits/"*)
    if [[ -e "$FIX/move-base" ]]; then
      [[ -e "$FIX/move-always" ]] || rm -f "$FIX/move-base"
      move_base >&2
    fi
    sha="${2#repos/pool-test/repo/commits/}"; sha="${sha%%/*}"
    tree="$(git -C "$PRIMARY" rev-parse "$sha^{tree}")"
    if [[ "$args" == *merge-proof/resharper* ]]; then
      printf 'success\037tree=%s baseTree=%s files=1 unity=0 blocking=0\037%s/98\n' "$tree" "$(git --git-dir="$ORIGIN" rev-parse 'main^{tree}')" "$URL"
    else
      case "$(cat "$FIX/hosted")" in
        green) printf 'success\037tree=%s total=5 passed=5 skipped=0\037%s/98\n' "$tree" "$URL" ;;
        error) printf 'error\037run cancelled or timed out\037%s/97\n' "$URL" ;;
        failure) printf 'failure\037headless suite failed - see run\037%s/97\n' "$URL" ;;
      esac
    fi ;;
  "run list"|"run view") printf 'completed\t98\n' ;;
  "workflow run") echo "$args" >> "$GH_DISPATCH_LOG"; echo green > "$FIX/hosted" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh" "$TMP/bin/powershell.exe"
export PATH="$TMP/bin:$PATH"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks" WORKTREE_POOL_REMOTE_POLL_SECONDS=1
LOCKS="$WORKTREE_POOL_LOCK_ROOT"

git init -q --bare -b main "$ORIGIN"
git clone -q "$ORIGIN" "$PRIMARY"
# repo_slug reads a GitHub URL off origin; insteadOf keeps the transport on the local bare repo.
git -C "$PRIMARY" config remote.origin.url "https://github.com/pool-test/repo.git"
git -C "$PRIMARY" config "url.$ORIGIN.insteadOf" "https://github.com/pool-test/repo.git"
printf 'results/\n.worktree-pool/\n' > "$PRIMARY/.gitignore"
mkdir -p "$PRIMARY/src"
printf 'class Gate {\n    // first comment\n    int value = 1;\n}\n' > "$PRIMARY/src/Gate.cs"
echo base > "$PRIMARY/README.md"
git -C "$PRIMARY" add -A
git -C "$PRIMARY" commit -qm init
git -C "$PRIMARY" push -q origin main
for n in 1 2 3; do git -C "$PRIMARY" worktree add -q -b "agent-$n" "$TMP/agent-$n" main; done
git clone -q "$ORIGIN" "$FIX/mover"
cd "$PRIMARY"

NONE=$'## Test status\n\n### Owed local\n\nNone.\n'
# pr_branch <n> <path>=<content>…: commits each edit on task/p<n> off origin/main and pushes it.
pr_branch() {
  local n="$1" edit
  shift
  git -C "$PRIMARY" fetch -q origin
  git -C "$PRIMARY" checkout -q -B "task/p$n" origin/main
  for edit in "$@"; do
    printf '%b' "${edit#*=}" > "$PRIMARY/${edit%%=*}"
    git -C "$PRIMARY" add -A
    git -C "$PRIMARY" commit -qm "p$n: ${edit%%=*}"
  done
  git -C "$PRIMARY" push -q origin "task/p$n"
  git -C "$PRIMARY" checkout -q --detach
}
head_of() { git -C "$PRIMARY" rev-parse "task/p$1${2:-}"; }
# facts <n> <instruction sha|-> <reviewed sha|-> [body] [unresolved threads] [other open PRs, csv]
facts() {
  python3 - "$FIX/facts-$1.json" "$1" "$2" "$3" "${4-}" "${5:-0}" "${6:-}" \
    $(git -C "$PRIMARY" rev-list "origin/main..task/p$1") <<'PY'
import json, sys
out, n, instruction, reviewed, body, unresolved, live = sys.argv[1:8]
commits = sys.argv[8:]
comments = []
if instruction != "-":
    comments.append({"author": {"login": "amindell11"}, "body": f"Merge instruction 2026-10-03: `{instruction}`"})
if reviewed != "-":
    comments.append({"author": {"login": "chatgpt-codex-connector"}, "body":
        "<!-- codex-pull-request-review-summary -->\n| Review | Status | Commit | Review trigger |\n| --- | --- | --- | --- |\n"
        f"| **Code Review** | **Completed** | `{reviewed[:7]}` | PR opened |\n"})
threads = [{"isResolved": False}] * int(unresolved or 0)
pr = {"number": int(n), "title": "t", "isDraft": False, "headRefOid": commits[0], "body": body, "changedFiles": 1,
      "files": {"nodes": []}, "commits": {"nodes": [{"commit": {"status": None}}]},
      "history": {"nodes": [{"commit": {"oid": c}} for c in reversed(commits)]},
      "comments": {"nodes": comments}, "reviewThreads": {"totalCount": len(threads), "nodes": threads},
      "reactions": {"nodes": []}}
live = [int(x) for x in [n] + live.split(",") if x]
json.dump({"data": {"repository": {"pullRequest": pr, "pullRequests": {"nodes": [{"number": x} for x in live]}}}},
          open(out, "w"))
PY
}
# land <n> [env…]: runs it, stdout to $out and stderr to $TMP/err, exit code to $rc.
land() {
  local n="$1"
  shift
  rc=0
  env "$@" bash "$POOL" land "$n" > "$TMP/out" 2> "$TMP/err" || rc=$?
  out="$(cat "$TMP/out")"
}
show() { echo "--- stdout:"; cat "$TMP/out"; echo "--- stderr:"; cat "$TMP/err"; } >&2
gate() { grep '^GATE=' "$TMP/out" || true; }
class() { grep '^CLASS=' "$TMP/out" || true; }
expect_refused() {
  [[ "$rc" -eq 1 && "$(gate)" == "GATE=refused:$1" ]] || { show; fail "$2: GATE=refused:$1, exit 1 (rc=$rc)"; }
}
expect_merged() {
  [[ "$rc" -eq 0 && "$(gate)" == GATE=merged ]] || { show; fail "$1: GATE=merged, exit 0 (rc=$rc)"; }
}
merges() { grep -c 'squash' "$GH_MERGE_LOG" || true; }
dispatches() { grep -c 'workflow run' "$GH_DISPATCH_LOG" || true; }
attempts() { grep -c '^land: PR #' "$TMP/err" || true; }
locked() { [[ -d "$LOCKS/$1.lock" ]]; }
lease_of() { cat "$LOCKS/$1.lock/lease" 2>/dev/null || true; }
journal() { ls -1t "$PRIMARY/.worktree-pool/merge-runs/$1-"*.jsonl | head -n 1; }
phases() { sed -n 's/.*"event":"phase-start","phase":"\([^"]*\)".*/\1/p' "$(journal "$1")" | tr '\n' ' '; }
echo green > "$FIX/hosted"

# --- a covered PR lands on the hosted path, in the class, and its slot is finalized ---------------------
pr_branch 1 'a.txt=one\n'
facts 1 "$(head_of 1)" "$(head_of 1)" "$NONE"
land 1
expect_merged "an exact instruction on a green PR"
[[ "$(class)" == CLASS=in ]] || { show; fail "nothing owed, no scripts/ or .github/, both statuses accepted and a covering review is in the class"; }
[[ "$(grep -c '^GATE=' "$TMP/out")" -eq 1 && "$(grep -c '^CLASS=' "$TMP/out")" -eq 1 ]] || { show; fail "land prints each trailer once"; }
grep -q -- "--match-head-commit $(head_of 1)" "$GH_MERGE_LOG" || fail "gh pr merge names the landing commit (got: $(cat "$GH_MERGE_LOG"))"
[[ "$(phases agent-1)" == "turn-wait preflight fetch base-merge authorize proof-check tests resharper push base-recheck gh-merge " ]] \
  || fail "land runs the hosted ladder with authorize after base-merge (got '$(phases agent-1)')"
grep -q '"msg":"class in"' "$(journal agent-1)" || fail "the journal notes the class verdict"
! locked agent-1 || fail "a merged land finalizes its slot"
[[ -z "$(git ls-remote origin refs/heads/task/p1)" ]] || fail "finalize deletes the merged task branch"
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$(git ls-remote origin refs/heads/main | cut -f1)" ]] || fail "the finalized slot sits on main"

# --- preflight refusals: no slot is taken and nothing merges --------------------------------------------
pr_branch 2 'b.txt=two\n'
merges_before="$(merges)"
preflight_refuses() {
  local reason="$1" why="$2"
  shift 2
  land 2 "$@"
  expect_refused "$reason" "$why"
  for slot in agent-1 agent-2 agent-3; do [[ "$(lease_of "$slot")" != land-2 ]] || fail "$why: preflight takes no slot"; done
  rm -f "$FIX/view-2"
}
printf 'CLOSED\tmain\ttask/p2\tfalse\tfalse\n' > "$FIX/view-2"; preflight_refuses not-open "a closed PR"
printf 'OPEN\trelease\ttask/p2\tfalse\tfalse\n' > "$FIX/view-2"; preflight_refuses base "a PR against another base"
printf 'OPEN\tmain\ttask/p2\ttrue\tfalse\n' > "$FIX/view-2"; preflight_refuses draft "a draft"
printf 'OPEN\tmain\tfeature/p2\tfalse\tfalse\n' > "$FIX/view-2"; preflight_refuses head-branch "a head outside task/*"
printf 'OPEN\tmain\ttask/p2\tfalse\ttrue\n' > "$FIX/view-2"; preflight_refuses head-branch "a head branch on a fork"
facts 2 "$(head_of 2)" - $'### Owed local\n\n- [ ] unity: boot\n'; preflight_refuses owed-open "an open owed item"
facts 2 "$(head_of 2)" - $'### Owed local\n\n- [ ] manual: look\n'; preflight_refuses owed-malformed "a malformed checklist"
facts 2 - - "$NONE"; preflight_refuses no-instruction "no recorded instruction"
facts 2 "$(head_of 2)" - "$NONE" 1; preflight_refuses unresolved "an unresolved review thread"
facts 2 "$(head_of 2)" - "$NONE"$'\n## Merge order\n\n- after #77 — same file\n' 0 77; preflight_refuses after:77 "a live merge-order constraint"
facts 2 "$(head_of 2)" - "$NONE"$'\n## Merge order\n\nafter #77\n'; preflight_refuses merge-order-malformed "a malformed merge order"
facts 2 "$(head_of 2)" - "$NONE"
pool acquire p2 agent-3 >/dev/null
preflight_refuses slot:agent-3 "another session's slot holds the PR's branch"
grep -q "its session merges it with 'merge agent-3'" "$TMP/err" || { show; fail "the refusal names the holding slot's merge"; }
pool release agent-3 >/dev/null
# Free slots only: a stale lock is never reclaimed, so with every other slot held there is none.
pool acquire other-1 agent-1 >/dev/null
pool acquire other-2 agent-2 >/dev/null
pool acquire stale-3 agent-3 >/dev/null
echo 2020-01-01T00:00:00Z | tee "$LOCKS/agent-3.lock/timestamp" > "$LOCKS/agent-3.lock/last_use"
preflight_refuses no-free-slot "no free slot, though a stale one could be reclaimed"
[[ "$(lease_of agent-3)" == stale-3 ]] || fail "land never reclaims a stale slot"
for n in 1 2 3; do pool release "agent-$n" >/dev/null; done
[[ "$(merges)" == "$merges_before" ]] || fail "no preflight refusal reaches gh pr merge"

# --- a dead run's land-<pr> slot is reused; a slot whose gate is running is left alone ---------------------
pool acquire land-2 agent-2 >/dev/null
land 2
expect_merged "the PR's own land slot"
grep -q '^land: PR #2 on agent-2 ' "$TMP/err" || { show; fail "land reuses the land-<pr> slot a dead run left"; }
! locked agent-1 && ! locked agent-2 || fail "the reused slot is finalized, and no other was taken"

pr_branch 3 'c.txt=three\n'
facts 3 "$(head_of 3)" - "$NONE"
pool acquire land-3 agent-2 >/dev/null
tree_before="$(git -C "$TMP/agent-2" rev-parse HEAD)"
pool_fn with_flock "$LOCKS/agent-2.merge" 0 "" bash -c "touch '$TMP/held'; until [[ -e '$TMP/release' ]]; do sleep 0.2; done" &
holder=$!
until [[ -e "$TMP/held" ]]; do kill -0 "$holder" || fail "fixture: the .merge holder died"; sleep 0.2; done
land 3
touch "$TMP/release"; wait "$holder"; rm -f "$TMP/held" "$TMP/release"
expect_refused gate-running "a slot whose .merge lock is held"
[[ "$(lease_of agent-2)" == land-3 && "$(git -C "$TMP/agent-2" rev-parse HEAD)" == "$tree_before" ]] \
  || fail "a refusal at another gate's slot leaves that slot as it was"
pool release agent-2 >/dev/null

# --- authorize: the covers relation ---------------------------------------------------------------------
# p4: instruction and review on the first commit, then a doc-only push.
pr_branch 4 'd.txt=four\n' 'README.md=reworded\n'
facts 4 "$(head_of 4 "~1")" "$(head_of 4 "~1")" ""
land 4
expect_merged "an instruction covering through a doc-only delta"
[[ "$(class)" == CLASS=out:owed ]] || { show; fail "with no owed section and the review covering, the first failing condition is owed"; }
# p5: a C# comment-only push after the instruction; the review is absent.
pr_branch 5 'e.txt=five\n' 'src/Gate.cs=class Gate {\n    // second comment\n    int value = 1;\n}\n'
facts 5 "$(head_of 5 "~1")" - "$NONE"
land 5
expect_merged "an instruction covering through a comment-only delta"
[[ "$(class)" == CLASS=out:review ]] || { show; fail "a missing review is out:review once everything before it holds"; }
# p6: a code push after the instruction.
pr_branch 6 'f.txt=six\n' 'src/Gate.cs=class Gate {\n    // first comment\n    int value = 2;\n}\n'
facts 6 "$(head_of 6 "~1")" "$(head_of 6)" "$NONE"
merges_before="$(merges)"
land 6
expect_refused unauthorized "a code delta since the instruction"
[[ "$(attempts)" -eq 1 && "$(merges)" == "$merges_before" ]] || { show; fail "unauthorized is refused once, with no merge"; }
[[ -z "$(class)" ]] || fail "a refusal at authorize prints no class verdict"
grep -q "drain_pick.sh instruct 6@<head>" "$TMP/err" || { show; fail "the refusal names how to record a new instruction"; }
! locked agent-1 || fail "a refused land releases its slot"
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$(git -C "$TMP/agent-1" rev-parse origin/main)" ]] || fail "a refused land resets its slot to origin/main"
# Switched on, the class stands in for the instruction.
facts 6 - "$(head_of 6)" "$NONE"
land 6 WORKTREE_POOL_AUTO_MERGE_CLASS=1
expect_merged "no instruction, but in the class with the switch on"
[[ "$(class)" == CLASS=in ]] || { show; fail "the switched merge is a class one"; }

# --- retries ----------------------------------------------------------------------------------------------
# Base moves once mid-gate: the second attempt integrates it, has the hosted suite prove that landing
# commit, and lands.
pr_branch 7 'g.txt=seven\n'
facts 7 "$(head_of 7)" "$(head_of 7)" "$NONE"
touch "$FIX/move-base"
land 7
expect_merged "base moved, then success"
[[ "$(attempts)" -eq 2 ]] && grep -q 'attempt 1/3 refused (base-moved)' "$TMP/err" || { show; fail "base-moved is retried once"; }
[[ "$(class)" == CLASS=in ]] || { show; fail "the instruction and the review cover the integrated tree"; }
# The hosted run errored: land re-dispatches it, waits for the new status, then lands.
pr_branch 8 'h.txt=eight\n'
facts 8 "$(head_of 8)" - "$NONE"
echo error > "$FIX/hosted"
dispatches_before="$(dispatches)"
land 8
expect_merged "an errored hosted run, re-dispatched"
[[ "$(dispatches)" -eq $((dispatches_before + 1)) ]] && grep -q -- '--ref task/p8' "$GH_DISPATCH_LOG" \
  || { show; fail "land re-dispatches the hosted suite on the PR's branch once"; }
[[ "$(attempts)" -eq 2 ]] || { show; fail "hosted-error is retried once"; }
# A failure verdict is never retried.
pr_branch 9 'i.txt=nine\n'
facts 9 "$(head_of 9)" - "$NONE"
echo failure > "$FIX/hosted"
dispatches_before="$(dispatches)"
land 9
expect_refused failure "a red hosted run"
[[ "$(attempts)" -eq 1 && "$(dispatches)" == "$dispatches_before" ]] || { show; fail "failure is neither retried nor re-dispatched"; }
! locked agent-1 || fail "the refused slot is released"
echo green > "$FIX/hosted"
# Base moves on every read: three attempts, then the transient reason stands.
touch "$FIX/move-base" "$FIX/move-always"
merges_before="$(merges)"
land 9
rm -f "$FIX/move-base" "$FIX/move-always"
expect_refused base-moved "base moving under every attempt"
[[ "$(attempts)" -eq 3 && "$(merges)" == "$merges_before" ]] || { show; fail "at most three attempts per call"; }
! locked agent-1 || fail "after the last attempt the slot is released"

echo "PASS test_pool_land.sh"
