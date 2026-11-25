#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# Regression for 'land-balance': a PR that only edits existing Settings assets and prefabs merges with
# no gate; an added, deleted or renamed file, a code path, a fork head, a moved head, a conflict and a
# held merge turn each refuse with main untouched.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
holder=""
trap 'touch "$TMP/release"; if [[ -n "$holder" ]]; then wait "$holder" 2>/dev/null || true; fi; rm -rf "$TMP"' EXIT
fail() { echo "FAIL: $1" >&2; [[ ! -f "$TMP/out" ]] || cat "$TMP/out" "$TMP/err" >&2; exit 1; }

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks" WORKTREE_POOL_MERGE_TURN_WAIT_SECONDS=1
export GIT_AUTHOR_NAME="Pool Test" GIT_AUTHOR_EMAIL=pool-test@example.test
export GIT_COMMITTER_NAME="Pool Test" GIT_COMMITTER_EMAIL=pool-test@example.test
export ORIGIN="$TMP/origin.git" FIX="$TMP/fix"
PRIMARY="$TMP/primary" AUTHOR="$TMP/author"
mkdir -p "$FIX" "$TMP/bin" "$WORKTREE_POOL_LOCK_ROOT"

# PR <n>'s head is refs/pull/<n>/head on origin; $FIX/view-<n> overrides the PR's view row.
cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
case "$1 $2" in
  "pr view")
    tip="$(git --git-dir="$ORIGIN" rev-parse "refs/pull/$3/head")"
    cat "$FIX/view-$3" 2>/dev/null || printf 'OPEN\tmain\tfalse\tfalse\t%s\n' "$tip" ;;
  "pr merge")
    echo "$args" >> "$FIX/merge-log"
    [[ "$args" == *--admin* ]] || { echo "gh stub: the ruleset refuses a merge without --admin" >&2; exit 1; }
    want="${args##*--match-head-commit }"; want="${want%% *}"
    tip="$(git --git-dir="$ORIGIN" rev-parse "refs/pull/$3/head")"
    [[ "$tip" == "$want" ]] || { echo "gh stub: head is $tip, not $want" >&2; exit 1; }
    tree="$(git --git-dir="$ORIGIN" merge-tree --write-tree main "$tip")"
    merge="$(git --git-dir="$ORIGIN" commit-tree "$tree" -p "$(git --git-dir="$ORIGIN" rev-parse refs/heads/main)" -m "squash #$3")"
    git --git-dir="$ORIGIN" update-ref refs/heads/main "$merge" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"

SETTINGS=src/Game/Assets/Settings
git init -q --bare -b main "$ORIGIN"
git clone -q "$ORIGIN" "$PRIMARY" 2>/dev/null
mkdir -p "$PRIMARY/$SETTINGS/Factions" "$PRIMARY/src/Game/Assets/Prefabs" "$PRIMARY/src/Game/Assets/Scripts"
echo "speed: 1" > "$PRIMARY/$SETTINGS/Factions/Veil.asset"
echo "rate: 1" > "$PRIMARY/src/Game/Assets/Prefabs/Gun.prefab"
echo "guid: 1" > "$PRIMARY/src/Game/Assets/Prefabs/Gun.prefab.meta"
echo "class Code {}" > "$PRIMARY/src/Game/Assets/Scripts/Code.cs"
git -C "$PRIMARY" add -A && git -C "$PRIMARY" commit -qm base && git -C "$PRIMARY" push -q origin HEAD:main
git clone -q "$ORIGIN" "$AUTHOR"

pr=0
# Opens PR <n+1> from origin/main plus the edits <fn> makes in the author clone.
open_pr() {
  pr=$((pr + 1))
  git -C "$AUTHOR" fetch -q origin && git -C "$AUTHOR" reset -q --hard origin/main
  "$@"
  git -C "$AUTHOR" add -A && git -C "$AUTHOR" commit -qm "pr $pr"
  git -C "$AUTHOR" push -q origin "HEAD:refs/pull/$pr/head"
}
land_balance() {
  rc=0
  (cd "$PRIMARY" && bash "$POOL" land-balance "$pr") > "$TMP/out" 2> "$TMP/err" || rc=$?
}
main_sha() { git --git-dir="$ORIGIN" rev-parse main; }
move_main() {
  git -C "$PRIMARY" pull -q --ff-only origin main
  echo "$1" > "$PRIMARY/$2"
  git -C "$PRIMARY" add -A && git -C "$PRIMARY" commit -qm "main moves" && git -C "$PRIMARY" push -q origin HEAD:main
}
expect_refused() {
  local reason="$1" what="$2" before
  before="$(main_sha)"
  land_balance
  [[ "$rc" -eq 1 && "$(cat "$TMP/out")" == "LAND_BALANCE=refused:$reason" ]] || fail "$what: refused:$reason, exit 1 (exit $rc)"
  [[ "$(main_sha)" == "$before" ]] || fail "$what: main must not move"
}

# Edits to a Settings asset and a prefab land over a main that moved meanwhile.
open_pr bash -c 'echo "speed: 2" > "$1/'"$SETTINGS"'/Factions/Veil.asset"; echo "rate: 2" > "$1/src/Game/Assets/Prefabs/Gun.prefab"' _ "$AUTHOR"
move_main "elsewhere" README.md
land_balance
[[ "$rc" -eq 0 ]] || fail "a balance-only PR lands (exit $rc)"
[[ "$(cat "$TMP/out")" == "LAND_BALANCE=landed $(git --git-dir="$ORIGIN" rev-parse "refs/pull/$pr/head")" ]] || fail "the trailer names the landed head"
[[ "$(git --git-dir="$ORIGIN" show "main:$SETTINGS/Factions/Veil.asset")" == "speed: 2" ]] || fail "main carries the asset edit"
[[ "$(git --git-dir="$ORIGIN" show main:README.md)" == elsewhere ]] || fail "main keeps the commit it moved to"

# A code path refuses; stderr names only the path outside the balance set.
open_pr bash -c 'echo "speed: 3" > "$1/'"$SETTINGS"'/Factions/Veil.asset"; echo "class Code { int x; }" > "$1/src/Game/Assets/Scripts/Code.cs"' _ "$AUTHOR"
expect_refused paths "a PR touching code"
grep -qx '  M src/Game/Assets/Scripts/Code.cs' "$TMP/err" || fail "stderr lists the code path"
! grep -q 'Veil.asset' "$TMP/err" || fail "stderr lists only the off-limits paths"

# New files refuse, a prefab with its .meta included.
open_pr bash -c 'echo "rate: 9" > "$1/src/Game/Assets/Prefabs/New.prefab"; echo "guid: 9" > "$1/src/Game/Assets/Prefabs/New.prefab.meta"' _ "$AUTHOR"
expect_refused paths "a PR adding a prefab"
grep -qx '  A src/Game/Assets/Prefabs/New.prefab' "$TMP/err" || fail "stderr lists the added prefab"

# An asset outside Settings, a deletion and a rename each refuse.
open_pr bash -c 'echo "x: 1" > "$1/src/Game/Assets/Other.asset"' _ "$AUTHOR"
expect_refused paths "a PR adding an asset outside Settings"
open_pr git -C "$AUTHOR" rm -q "$SETTINGS/Factions/Veil.asset"
expect_refused paths "a PR deleting a Settings asset"
open_pr git -C "$AUTHOR" mv "$SETTINGS/Factions/Veil.asset" "$SETTINGS/Factions/Veil2.asset"
expect_refused paths "a PR renaming a Settings asset"

# A PR that conflicts with main refuses.
open_pr bash -c 'echo "speed: 4" > "$1/'"$SETTINGS"'/Factions/Veil.asset"' _ "$AUTHOR"
move_main "speed: 5" "$SETTINGS/Factions/Veil.asset"
expect_refused conflict "a conflicting PR"

# The PR's own state: closed, wrong base, draft, fork, and a head GitHub reports differently.
open_pr bash -c 'echo "rate: 6" > "$1/src/Game/Assets/Prefabs/Gun.prefab"' _ "$AUTHOR"
tip="$(git --git-dir="$ORIGIN" rev-parse "refs/pull/$pr/head")"
for row in "not-open MERGED main false false" "base OPEN dev false false" "draft OPEN main true false" \
  "head-branch OPEN main false true"; do
  read -r reason state base draft cross <<< "$row"
  printf '%s\t%s\t%s\t%s\t%s\n' "$state" "$base" "$draft" "$cross" "$tip" > "$FIX/view-$pr"
  expect_refused "$reason" "a PR whose view is $state/$base/draft=$draft/fork=$cross"
done
printf 'OPEN\tmain\tfalse\tfalse\t%s\n' "$(main_sha)" > "$FIX/view-$pr"
expect_refused head-moved "a head that moved after GitHub reported it"
rm -f "$FIX/view-$pr"

# The merge turn excludes it: a holder that outlasts the wait refuses the landing.
bash "$POOL" lock merge-turn -- bash -c ': > "$1/held"; until [[ -e "$1/release" ]]; do sleep .05; done' _ "$TMP" &
holder=$!
until [[ -e "$TMP/held" ]]; do kill -0 "$holder" 2>/dev/null || fail "fixture: the turn holder exited early"; sleep .05; done
before="$(main_sha)"
land_balance
[[ "$rc" -eq 75 && "$(cat "$TMP/out")" == "LAND_BALANCE=refused:turn-held" ]] || fail "a held turn refuses with exit 75 (exit $rc)"
[[ "$(main_sha)" == "$before" ]] || fail "a held turn leaves main alone"
touch "$TMP/release"
wait "$holder"
holder=""
land_balance
[[ "$rc" -eq 0 && "$(git --git-dir="$ORIGIN" show main:src/Game/Assets/Prefabs/Gun.prefab)" == "rate: 6" ]] || fail "the landing goes through once the turn is free (exit $rc)"

echo "PASS test_pool_land_balance.sh"
