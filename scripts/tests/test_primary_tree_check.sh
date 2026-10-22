#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/primary_tree_check.sh

# Hermetic regression for the session-start check's pull report and sound-work split. A bare repo
# stands in for origin and a clone for the primary tree, with an app worktree under its
# .claude/worktrees/ that the check runs from and never moves. The primary's blocked pull lists its
# collisions in git's own terms, checked against a real merge, and untracked sound paths are
# counted apart from strays. The post-merge hook prints the stray report after a pull in the
# primary and stays quiet in the app worktree.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$SCRIPT_DIR/../primary_tree_check.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }
g() { git -c user.name=t -c user.email=t@t "$@"; }
check() { (cd "$1" && bash "$CHECK" "${@:2}"); }
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

# --- current everywhere: the session check says nothing, the stray report lists the app worktree ---
out="$(check "$W/fresh")"
[[ -z "$out" ]] || fail "current tree reported something: $out"
out="$(check "$W/fresh" strays)"
[[ "$out" == $'Primary tree '*'!! .claude/worktrees/'*'PRIMARY_TREE_DIRTY=1' ]] || fail "current tree: $out"

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
[[ "$out" == *"The primary's main is 1 commit behind origin/main, and incoming commits change these uncommitted paths. Sync it with \`./scripts/agent_worktree_pool.sh sync-primary\`."* ]] ||
  fail "blocked pull not reported: $out"
[[ "$out" == *$'sync-primary takes them'*$'\n  audio/fmod/Assets/clip.wav\nDifferent from origin/main'*$'\n  .edited.txt' ]] ||
  fail "collisions not split into equal and different: $out"
[[ "$out" != *"  staged.txt"* && "$out" != *"  other.txt"* ]] || fail "a path git lets through was listed: $out"
out="$(check "$W/fresh" strays)"
[[ "$out" == *"Sound work in progress, not strays: 6 untracked paths"* ]] || fail "sound count: $out"
for stray in '?? notes/' '!! .claude/worktrees/'; do
  [[ "$out" == *"  $stray"* ]] || fail "stray $stray missing: $out"
done
[[ "$out" != *"  ?? audio/"* && "$out" != *"  ?? src/"* && "$out" != *.cache* && "$out" != *.asd* && "$out" != *results/* ]] ||
  fail "sound path, byproduct or allowlisted dir listed as a stray: $out"
[[ "$out" == *"Agents write there only to fast-forward main and for sound work"* ]] || fail "stray wording: $out"
[[ "$(tail -n1 <<< "$out")" == "PRIMARY_TREE_DIRTY=2" ]] || fail "trailer: $out"

# --- the hook's prediction matches git, and it never moved the primary ---
[[ "$(head_of "$P")" == "$base_head" ]] || fail "the check moved the primary's main"
if g -C "$P" -c pull.rebase=false merge -q --ff-only origin/main 2> /dev/null; then fail "git took a pull the check called refused"; fi
g -C "$P" add audio/fmod/Assets/clip.wav
out="$(check "$P")"
[[ "$out" != *"  audio/fmod/Assets/clip.wav"* && "$out" == *$'leave the edits where they are:\n  .edited.txt'* ]] ||
  fail "staged equal path still listed, or the differing edit dropped: $out"
put "$P/.edited.txt" shipped                         # equal now, but unstaged: git still refuses it
out="$(check "$P")"
[[ "$out" == *$'sync-primary takes them'*$'\n  .edited.txt' && "$out" != *"Different from origin/main"* ]] ||
  fail "unstaged equal edit not listed as equal: $out"
g -C "$P" add .edited.txt
out="$(check "$P")"
[[ "$out" != *"incoming commits change"* ]] || fail "a pull git would take reported as refused: $out"
g -C "$P" -c pull.rebase=false merge -q --ff-only origin/main || fail "git refused a pull the check called clean"
[[ "$(git -C "$P" show HEAD:other.txt)" == base && "$(cat "$P/other.txt")" == owner ]] || fail "unrelated edit lost"

# --- an untracked file with other content where main adds one: sync-primary refuses it ---
ship "ship a take" audio/fmod/Assets/take.wav main
put "$P/audio/fmod/Assets/take.wav" owner
out="$(check "$P")"
[[ "$out" == *$'sync-primary refuses while these sit there'*$'\n  audio/fmod/Assets/take.wav' && "$out" != *"Different from origin/main"* ]] ||
  fail "an untracked file with other content not listed as refused: $out"
rm "$P/audio/fmod/Assets/take.wav"

# --- post-merge: a pull in the primary, under the owner's pull settings, prints strays; an app-worktree merge doesn't ---
ship "for the hook" other3.txt y
git -C "$P" config core.hooksPath "$SCRIPT_DIR/../../.githooks"
g -C "$P" fetch -q origin
out="$(g -C "$W/fresh" merge --ff-only origin/main 2>&1)"
[[ "$out" != *PRIMARY_TREE_DIRTY* ]] || fail "a merge in the app worktree listed strays: $out"
out="$(g -C "$P" -c pull.rebase=true -c rebase.autostash=true pull -q 2>&1)"
[[ "$out" == *"Triage these now"*"PRIMARY_TREE_DIRTY=2"* ]] || fail "a pull in the primary listed no strays: $out"
git -C "$P" config --unset core.hooksPath

# --- a diverged main, then an unreachable origin ---
ship "plain change" other2.txt x
put "$P/local.txt" local && g -C "$P" add local.txt && g -C "$P" commit -qm local
out="$(check "$P")"
[[ "$out" == *"The primary's main has commits origin/main lacks"* ]] || fail "diverged main: $out"
g -C "$P" remote set-url origin "$TMP/missing.git"
out="$(check "$W/fresh" 2> /dev/null)"
[[ "$out" == *"Could not fetch origin main"* ]] || fail "unreachable origin: $out"

echo "PASS: primary-tree check pull report, sound-work split and post-merge stray report"
