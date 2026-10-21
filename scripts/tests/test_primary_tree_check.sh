#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/primary_tree_check.sh

# Hermetic regression for the session-start check's pull report and sound-work split. A bare repo
# stands in for origin and a clone for the primary tree, with an app session worktree under its
# .claude/worktrees/ that the check runs from and never moves. The primary's blocked pull lists its
# collisions in git's own terms, checked against a real merge, and untracked sound paths are
# counted apart from strays.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$SCRIPT_DIR/../primary_tree_check.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }
g() { git -c user.name=t -c user.email=t@t "$@"; }
check() { (cd "$1" && bash "$CHECK"); }
put() { mkdir -p "$(dirname "$1")" && printf '%s\n' "$2" > "$1"; }
head_of() { git -C "$1" rev-parse HEAD; }
ship() {
  local msg="$1"
  shift
  while (($#)); do put "$TMP/up/$1" "$2"; shift 2; done
  g -C "$TMP/up" add -A && g -C "$TMP/up" commit -qm "$msg" && g -C "$TMP/up" push -q origin main
}

git init -q --bare -b main "$TMP/origin.git"
g clone -q "$TMP/origin.git" "$TMP/up" 2> /dev/null
git -C "$TMP/up" symbolic-ref HEAD refs/heads/main
put "$TMP/up/.gitignore" $'results/\naudio/fmod/.cache/\n*.asd'
ship base AGENTS.md rules .claude/settings.json {} .edited.txt base staged.txt base other.txt base \
  audio/fmod/Assets/old.wav old src/Asteroids3D/Assets/Audio/keep.wav keep \
  src/Asteroids3D/Assets/Scripts/Ships/Audio/EngineAudio.cs engine \
  src/Asteroids3D/Assets/Scripts/Weapons/Gun.cs gun
g clone -q "$TMP/origin.git" "$TMP/primary"
P="$TMP/primary"
echo '.claude/worktrees/' >> "$P/.git/info/exclude"
W="$P/.claude/worktrees"
g -C "$P" worktree add -q -b claude/fresh "$W/fresh" main
base_head="$(head_of "$P")"

# --- current everywhere: only the stray trailer, no fetch noise ---
out="$(check "$W/fresh")"
[[ "$out" == $'Primary tree '*'!! .claude/worktrees/'*'PRIMARY_TREE_DIRTY=1' ]] || fail "current tree: $out"
[[ "$out" != *"would be refused"* ]] || fail "current tree reported a blocked pull: $out"

# --- origin moves: a shipped clip, an edit that collides, an edit already staged ---
ship "ship sound" audio/fmod/Assets/clip.wav clip .edited.txt shipped staged.txt shipped
put "$P/audio/fmod/Assets/clip.wav" clip            # untracked copy equal to the shipped one
put "$P/.edited.txt" owner                           # tracked dotfile edit that differs from the shipped one
put "$P/staged.txt" shipped && g -C "$P" add staged.txt && put "$P/staged.txt" owner   # staged equal, edited on top: goes through
put "$P/other.txt" owner                             # tracked edit no incoming commit touches
put "$P/audio/fmod/Assets/new.wav" new
put "$P/src/Asteroids3D/Assets/Audio/x.wav" x
put "$P/src/Asteroids3D/Assets/Scripts/Ships/Audio/EngineAudio2.cs" engine2
put "$P/src/Asteroids3D/Assets/Scripts/Weapons/Audio/GunAudio.cs" gunaudio
put "$P/src/Asteroids3D/Assets/Scripts/Weapons/Audio.meta" meta
put "$P/audio/fmod/.cache/state" cache
put "$P/audio/fmod/Assets/clip.wav.asd" asd
put "$P/notes/report.md" report
put "$P/results/run.json" run

out="$(check "$W/fresh")"
[[ "$(head_of "$W/fresh")" == "$base_head" ]] || fail "the check moved the app worktree: $out"
[[ "$out" == *"The primary's main is 1 commit behind origin/main, and \`git pull --ff-only\` there would be refused"* ]] ||
  fail "blocked pull not reported: $out"
[[ "$out" == *$'lets the pull through'*$'\n  audio/fmod/Assets/clip.wav\nDifferent from origin/main'*$'\n  .edited.txt\n'* ]] ||
  fail "collisions not split into equal and different: $out"
[[ "$out" != *"  staged.txt"* && "$out" != *"  other.txt"* ]] || fail "a path git lets through was listed: $out"
[[ "$out" == *"Sound work in progress, not strays: 6 untracked paths"* ]] || fail "sound count: $out"
for stray in '?? notes/' '!! results/' '!! .claude/worktrees/'; do
  [[ "$out" == *"  $stray"* ]] || fail "stray $stray missing: $out"
done
[[ "$out" != *"  ?? audio/"* && "$out" != *"  ?? src/"* && "$out" != *.cache* && "$out" != *.asd* ]] ||
  fail "sound path or byproduct listed as a stray: $out"
[[ "$out" == *"Agents write there only to fast-forward main and for sound work"* ]] || fail "stray wording: $out"
[[ "$(tail -n1 <<< "$out")" == "PRIMARY_TREE_DIRTY=3" ]] || fail "trailer: $out"

# --- the hook's prediction matches git, and it never moved the primary ---
[[ "$(head_of "$P")" == "$base_head" ]] || fail "the check moved the primary's main"
if g -C "$P" -c pull.rebase=false merge -q --ff-only origin/main 2> /dev/null; then fail "git took a pull the check called refused"; fi
g -C "$P" add audio/fmod/Assets/clip.wav
out="$(check "$P")"
[[ "$out" != *"  audio/fmod/Assets/clip.wav"* && "$out" == *$'leave the edits where they are:\n  .edited.txt'* ]] ||
  fail "staged equal path still listed, or the differing edit dropped: $out"
put "$P/.edited.txt" shipped                         # equal now, but unstaged: git still refuses it
out="$(check "$P")"
[[ "$out" == *$'lets the pull through'*$'\n  .edited.txt\n'* && "$out" != *"Different from origin/main"* ]] ||
  fail "unstaged equal edit not listed as equal: $out"
g -C "$P" add .edited.txt
out="$(check "$P")"
[[ "$out" != *"would be refused"* ]] || fail "a pull git would take reported as refused: $out"
g -C "$P" -c pull.rebase=false merge -q --ff-only origin/main || fail "git refused a pull the check called clean"
[[ "$(git -C "$P" show HEAD:other.txt)" == base && "$(cat "$P/other.txt")" == owner ]] || fail "unrelated edit lost"

# --- a diverged main, then an unreachable origin ---
ship "plain change" other2.txt x
put "$P/local.txt" local && g -C "$P" add local.txt && g -C "$P" commit -qm local
out="$(check "$P")"
[[ "$out" == *"The primary's main has commits origin/main lacks"* ]] || fail "diverged main: $out"
g -C "$P" remote set-url origin "$TMP/missing.git"
out="$(check "$W/fresh" 2> /dev/null)"
[[ "$out" == *"Could not fetch origin main"* && "$(tail -n1 <<< "$out")" == PRIMARY_TREE_DIRTY=* ]] ||
  fail "unreachable origin: $out"

echo "PASS: primary-tree check pull report and sound-work split"
