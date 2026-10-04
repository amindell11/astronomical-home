#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# Regression for 'land-docs': a delta of doc/** and *.md paths rebases onto a moved main and lands;
# a code path (a rename out of code included), a failed fetch, an empty delta, a conflicting rebase
# and a held merge turn each refuse with main untouched.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
holder=""
trap 'touch "$TMP/release"; if [[ -n "$holder" ]]; then wait "$holder" 2>/dev/null || true; fi; rm -rf "$TMP"' EXIT
fail() { echo "FAIL: $1" >&2; [[ ! -f "$TMP/out" ]] || cat "$TMP/out" "$TMP/err" >&2; exit 1; }

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks" WORKTREE_POOL_MERGE_TURN_WAIT_SECONDS=1
export GIT_AUTHOR_NAME="Pool Test" GIT_AUTHOR_EMAIL=pool-test@example.test
export GIT_COMMITTER_NAME="Pool Test" GIT_COMMITTER_EMAIL=pool-test@example.test
ORIGIN="$TMP/origin.git" SLOT="$TMP/agent-1" MOVER="$TMP/mover"

git init -q --bare -b main "$ORIGIN"
git clone -q "$ORIGIN" "$TMP/primary" 2>/dev/null
mkdir -p "$TMP/primary/doc" "$TMP/primary/src"
echo "line" > "$TMP/primary/doc/a.md"
echo "class Code {}" > "$TMP/primary/src/Code.cs"
git -C "$TMP/primary" add -A && git -C "$TMP/primary" commit -qm base && git -C "$TMP/primary" push -q origin HEAD:main
git -C "$TMP/primary" worktree add -q -b agent-1 "$SLOT" origin/main
git clone -q "$ORIGIN" "$MOVER"

land_docs() {
  rc=0
  (cd "$SLOT" && bash "$POOL" land-docs) > "$TMP/out" 2> "$TMP/err" || rc=$?
}
main_sha() { git -C "$ORIGIN" rev-parse main; }
commit_in() { local dir="$1" msg="$2"; git -C "$dir" add -A && git -C "$dir" commit -qm "$msg"; }
move_main() {
  git -C "$MOVER" pull -q --ff-only origin main
  echo "$1" > "$MOVER/$2"
  commit_in "$MOVER" "main moves" && git -C "$MOVER" push -q origin HEAD:main
}
reset_slot() { git -C "$SLOT" fetch -q origin && git -C "$SLOT" reset -q --hard origin/main; }
expect_refused() {
  local reason="$1" what="$2" before="$3"
  [[ "$rc" -eq 1 && "$(cat "$TMP/out")" == "LAND_DOCS=refused:$reason" ]] || fail "$what: refused:$reason, exit 1 (exit $rc)"
  [[ "$(main_sha)" == "$before" ]] || fail "$what: main must not move"
}

# Docs anywhere under doc/ or named *.md land, rebased over a main that moved meanwhile.
mkdir -p "$SLOT/doc/assets" "$SLOT/.claude/skills/x"
echo "png" > "$SLOT/doc/assets/still.png"
echo "readme" > "$SLOT/README.md"
echo "skill" > "$SLOT/.claude/skills/x/SKILL.md"
commit_in "$SLOT" "docs"
move_main "moved" doc/moved.md
moved="$(main_sha)"
land_docs
[[ "$rc" -eq 0 ]] || fail "a docs-only delta lands (exit $rc)"
[[ "$(cat "$TMP/out")" == "LAND_DOCS=landed $(main_sha)" ]] || fail "the trailer names the landed commit"
[[ "$(git -C "$SLOT" rev-parse HEAD)" == "$(main_sha)" ]] || fail "main is the slot's rebased HEAD"
git -C "$ORIGIN" merge-base --is-ancestor "$moved" main || fail "the landing keeps the commit main moved to"

# A code path refuses, and stderr names it.
reset_slot
echo "more" >> "$SLOT/doc/a.md"
mkdir -p "$SLOT/scripts" && echo "echo hi" > "$SLOT/scripts/x.sh"
commit_in "$SLOT" "docs and code"
before="$(main_sha)"
land_docs
expect_refused paths "a delta touching scripts/" "$before"
grep -qx '  scripts/x.sh' "$TMP/err" || fail "stderr lists the code path"
! grep -q 'doc/a.md' "$TMP/err" || fail "stderr lists only the paths outside doc/ and *.md"

# A rename out of code into doc/ is a delete of code.
reset_slot
git -C "$SLOT" mv src/Code.cs doc/Code.cs
commit_in "$SLOT" "move code into doc"
land_docs
expect_refused paths "a rename from src/ into doc/" "$before"
grep -qx '  src/Code.cs' "$TMP/err" || fail "stderr lists the renamed-away code path"

# An unreachable origin refuses with a trailer.
git -C "$SLOT" remote set-url origin "$TMP/missing.git"
land_docs
git -C "$SLOT" remote set-url origin "$ORIGIN"
expect_refused fetch "a failed fetch" "$before"

# Nothing to land.
reset_slot
land_docs
expect_refused empty "HEAD at origin/main" "$before"

# A conflicting rebase is aborted, leaving HEAD where it was.
reset_slot
echo "slot edit" > "$SLOT/doc/a.md"
commit_in "$SLOT" "slot edits a.md"
slot_head="$(git -C "$SLOT" rev-parse HEAD)"
move_main "main edit" doc/a.md
before="$(main_sha)"
land_docs
expect_refused rebase "a conflicting rebase" "$before"
! git -C "$SLOT" rev-parse -q --verify REBASE_HEAD >/dev/null || fail "the conflicting rebase is aborted"
[[ "$(git -C "$SLOT" rev-parse HEAD)" == "$slot_head" ]] || fail "an aborted rebase leaves HEAD where it was"

# The merge turn excludes it: a holder that outlasts the wait refuses the landing.
reset_slot
echo "turn" > "$SLOT/doc/turn.md"
commit_in "$SLOT" "docs under a held turn"
bash "$POOL" lock merge-turn -- bash -c ': > "$1/held"; until [[ -e "$1/release" ]]; do sleep .05; done' _ "$TMP" &
holder=$!
until [[ -e "$TMP/held" ]]; do kill -0 "$holder" 2>/dev/null || fail "fixture: the turn holder exited early"; sleep .05; done
land_docs
[[ "$rc" -eq 75 && "$(cat "$TMP/out")" == "LAND_DOCS=refused:turn-held" ]] || fail "a held turn refuses with exit 75 (exit $rc)"
[[ "$(main_sha)" == "$before" ]] || fail "a held turn leaves main alone"
touch "$TMP/release"
wait "$holder"
holder=""
land_docs
[[ "$rc" -eq 0 && "$(git -C "$ORIGIN" show main:doc/turn.md)" == turn ]] || fail "the landing goes through once the turn is free (exit $rc)"

echo "PASS test_pool_land_docs.sh"
