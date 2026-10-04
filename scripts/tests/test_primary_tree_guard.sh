#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/primary_tree_guard.py scripts/primary_tree_check.sh

# Hermetic regression for the primary-tree hooks. A temp repo stands in for the primary tree, with
# a sibling slot worktree and a worktree nested inside it. The guard is fed hook JSON and must deny
# destructive commands whose effective directory or target may be the primary tree (the 2026-10-03
# wipe's failed-cd chain among them) while allowing the same commands in a slot. The check must
# list the primary's untracked and non-allowlisted ignored files, from either worktree.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$SCRIPT_DIR/../primary_tree_guard.py"
CHECK="$SCRIPT_DIR/../primary_tree_check.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
unset CLAUDE_PROJECT_DIR

fail() { echo "FAIL: $1" >&2; exit 1; }
native() { (cd "$1" && { pwd -W 2>/dev/null || pwd; }); }

git init -q "$TMP/primary"
git -C "$TMP/primary" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
git -C "$TMP/primary" worktree add -q --detach "$TMP/slot"
git -C "$TMP/primary" worktree add -q --detach "$TMP/primary/nested"
P="$(native "$TMP/primary")"
S="$(native "$TMP/slot")"
N="$(native "$TMP/primary/nested")"
ROOT="$(native "$TMP")"

verdict() {
  local out
  out="$(python3 -c 'import json,sys; print(json.dumps({"tool_name": sys.argv[1], "cwd": sys.argv[2], "tool_input": {"command": sys.argv[3]}}))' "$@" |
    python3 "$GUARD")" || fail "guard exited non-zero on: $3"
  if [[ -z "$out" ]]; then echo allow
  elif [[ "$out" == *'"permissionDecision": "deny"'* ]]; then echo deny
  else echo "malformed: $out"
  fi
}
expect() {
  local want="$1" got
  shift
  got="$(verdict "$@")"
  [[ "$got" == "$want" ]] || fail "expected $want, got $got — cwd=$2 command: $3"
}
deny() { expect deny Bash "$@"; }
allow() { expect allow Bash "$@"; }

# --- destructive git where the effective directory is the primary ---
deny "$P" 'git clean -fdx'
deny "$S" "git -C \"$P\" reset --hard"
deny "$P" 'git rm -rfq .'
deny "$P" 'git rm -r --cached src'
deny "$P" 'git checkout -q --orphan evidence/x'
deny "$P" 'git checkout -- .'
deny "$P" 'git checkout .'
deny "$P" 'git checkout -f main'
deny "$P" 'git restore .'
deny "$P" 'git -C "" clean -fd'
deny "$S" 'git -C "$UNSET_DIR" clean -fdx'
deny "$S" 'git --work-tree=/x clean -fdx'
deny "$P" "git clean -fd 'unterminated"
deny "$P" 'git stash'
deny "$P" 'git stash push -m x'
deny "$P" 'git stash pop'
deny "$P" 'git stash drop stash@{0}'

# --- the wipe: a ;-joined cd that may fail (missing, or deleted earlier) leaves the shell in the primary ---
deny "$P" "W=\"$S/wt\"; git worktree add -q --detach \"\$W\" origin/main; cd \"\$W\"; git checkout -q --orphan e; git rm -rfq . ; git clean -fdxq"
deny "$P" "cd \"$S/missing\"; git clean -fd"
deny "$P" "cd \"$S/missing\" && true; git clean -fd"
deny "$P" "rm -rf \"$S/x\"; cd \"$S\"; git clean -fd"
deny "$S" "cd \"$P\" && git clean -fd"
deny "$S" "bash -c 'cd \"$P\" && git clean -fdx'"
if [[ "$P" =~ ^([A-Za-z]):(.*)$ ]]; then
  deny "$S" "git -C /${BASH_REMATCH[1],,}${BASH_REMATCH[2]} clean -fdx"
fi

# --- recursive deletes reaching into the primary ---
deny "$P" 'rm -rf src'
deny "$S" "rm -rf \"$P/src\""
deny "$S" "rm -rf \"$ROOT\""
deny "$S" 'rm -rf "$SOME_UNSET/x"'
deny "$P" 'rm -r -f *'
expect deny PowerShell "$P" 'Remove-Item -Recurse -Force src'
expect deny PowerShell "$S" "Set-Location \"$P\"; Remove-Item -r x"
expect deny PowerShell "$S" "Remove-Item -Path \"$P\\src\" -Recurse"

# --- allowed: slots, nested worktrees, safe shapes, mentions of the words ---
allow "$S" 'git clean -fdx'
allow "$N" 'git clean -fdx'
allow "$P" "git -C \"$S\" clean -fdx"
allow "$P" "cd \"$S\" && git reset --hard"
allow "$P" "cd \"$S\"; git clean -fd"
allow "$P" 'git checkout main && git pull'
allow "$P" 'git clean -n'
allow "$P" 'git restore --staged x'
allow "$P" 'rm stray.txt'
allow "$P" "rm -rf \"$S/src/Asteroids3D/Library/BurstCache/\""
allow "$P" 'ev="$(mktemp -d)" && git worktree add -q --detach "$ev" && git -C "$ev" checkout -q --orphan e && git -C "$ev" rm -rfq .'
allow "$P" 'echo "git clean -fdx"'
allow "$P" 'git commit -m "docs: never git clean the primary"'
allow "$P" $'git commit -F - <<\'EOF\'\nrm -rf src\ngit clean -fdx\nEOF'
allow "$P" 'git status'
allow "$P" 'git stash list'
allow "$P" 'git stash show --stat stash@{0}'
allow "$S" 'git stash'
allow "$P" "cd \"$S\" && git reset --hard HEAD 2>&1 | tail -1 && git checkout HEAD -- f"
allow "$P" "rm -rf \"$S/Library/BurstCache/\" 2>/dev/null"
expect allow PowerShell "$P" "Remove-Item -Recurse \"$S\\Temp\""
expect allow PowerShell "$P" "Remove-Item -Recurse -Force $S\\Temp -ErrorAction SilentlyContinue"

# --- the check lists the primary's dirt, from either worktree ---
git -C "$TMP/primary" worktree remove "$TMP/primary/nested"
printf 'results/\nsrc/Asteroids3D/Library/\n*.sln\n' > "$TMP/primary/.gitignore"
mkdir -p "$TMP/primary/src/Asteroids3D" && touch "$TMP/primary/src/Asteroids3D/tracked.cs"
git -C "$TMP/primary" add .gitignore src
git -C "$TMP/primary" -c user.name=t -c user.email=t@t commit -q -m ignore
out="$(cd "$TMP/slot" && bash "$CHECK")"
[[ "$out" == "PRIMARY_TREE_DIRTY=0" ]] || fail "clean primary reported dirt: $out"

mkdir -p "$TMP/primary/results/run" "$TMP/primary/src/Asteroids3D/Library" "$TMP/primary/notes"
touch "$TMP/primary/results/run/a.json" "$TMP/primary/src/Asteroids3D/Library/x" \
  "$TMP/primary/src/Asteroids3D/A.sln" "$TMP/primary/notes/report.md"
echo "tracked edit" >> "$TMP/primary/.gitignore"
git -C "$TMP/primary" worktree add -q --detach "$TMP/primary/nested"
for from in slot primary; do
  out="$(cd "$TMP/$from" && bash "$CHECK")"
  [[ "$out" == *"?? notes/"* ]] || fail "untracked notes/ missing (from $from): $out"
  [[ "$out" == *"!! results/"* ]] || fail "ignored results/ missing (from $from): $out"
  [[ "$out" == *"?? nested/"* ]] || fail "nested worktree missing (from $from): $out"
  [[ "$out" == *"Triage these now"* ]] || fail "no triage instruction (from $from): $out"
  [[ "$out" != *Library* && "$out" != *A.sln* ]] || fail "allowlisted path reported (from $from): $out"
  [[ "$out" != *".gitignore"* ]] || fail "tracked modification reported (from $from): $out"
  [[ "$(tail -n1 <<<"$out")" == "PRIMARY_TREE_DIRTY=3" ]] || fail "trailer (from $from): $out"
done

echo "PASS: primary-tree guard and check"
