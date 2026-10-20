#!/usr/bin/env bash
# SessionStart hook: start the session from a current base, and surface what needs triage.
#
# 1. An app session worktree (under <primary>/.claude/worktrees/) can start behind origin/main.
#    With no commits of its own and a clean tree it is fast-forwarded. Claude Code has already
#    loaded AGENTS.md by then, so the output says when the range changed it.
# 2. When the primary's main is behind origin/main and `git pull --ff-only` there would be
#    refused, the colliding paths are listed in two groups: those already equal to origin/main,
#    which staging lets through, and those that differ, which wait on the owner.
# 3. Clean primary = no untracked files outside SOUND and no ignored files outside ALLOW below.
#    Untracked SOUND paths are sound work in progress (doc/agents/audio-pipeline.md → Where sound
#    work happens), counted but not listed. Tracked modifications are never listed as strays.
# The primary tree is resolved from --git-common-dir, so the checks read the same tree from a
# slot, a session worktree or the primary itself. Checks 2 and 3 leave the primary's files and
# index untouched.
#
# Interface: exit 0 always (a hook failure must not block a session).
#   stdout  prose for the session (Claude Code adds it to context), then the trailer
#           PRIMARY_TREE_DIRTY=<stray count>; nothing to report prints the trailer alone.
#   stderr  git errors only.
# Never runs Unity. One `git fetch origin main`, capped at FETCH_SECONDS; with no origin remote,
# checks 1 and 2 are skipped.
set -uo pipefail

ALLOW=(
  'src/Asteroids3D/Library/' 'src/Asteroids3D/Temp/' 'src/Asteroids3D/Logs/'
  'src/Asteroids3D/obj/' 'src/Asteroids3D/UserSettings/' 'src/Asteroids3D/*.csproj'
  '*.sln' '*.DotSettings' '.worktree-pool/' '.agents/' '.claude/scheduled_tasks.lock'
  'doc/design/.obsidian/workspace.json' 'doc/design/.obsidian/cache/' '__pycache__/' '*/__pycache__/'
  'audio/fmod/.cache/' 'audio/fmod/.user/' 'audio/fmod/.unsaved/' '*.asd'
  'src/Asteroids3D/Assets/Resources/FMOD/FMODStudioCache.asset*' 'src/Asteroids3D/fmod_editor.log'
)
SOUND=(
  'audio/*' 'src/Asteroids3D/Assets/Audio/*'
  'src/Asteroids3D/Assets/Scripts/Audio/*' 'src/Asteroids3D/Assets/Scripts/*/Audio/*'
  'src/Asteroids3D/Assets/Scripts/*/Audio.meta'
)
MAX_LISTED=40
FETCH_SECONDS=20

matches() {
  local path="$1" pattern
  shift
  for pattern; do
    # shellcheck disable=SC2053  # the pattern is a glob on purpose
    [[ "$path" == $pattern ]] && return 0
  done
  return 1
}

list() {
  printf '  %s\n' "${@:1:MAX_LISTED}"
  if (($# > MAX_LISTED)); then echo "  ... and $(($# - MAX_LISTED)) more"; fi
}

plural() {
  if (($1 == 1)); then echo "$1 $2"; else echo "$1 ${2}s"; fi
}

commits() {
  plural "$(git rev-list --count "$1")" commit
}

read_set() {
  local -n into="$1"
  local path
  while IFS= read -r -d '' path; do into[$path]=1; done
}

# Hashed as `git add` would store it, so equal to origin/main means staging lets the pull through.
worktree_blob() {
  if [[ -f "$1/$2" ]]; then git -C "$1" hash-object -- "$2"; fi
}

fast_forward_session() {
  local primary="$1" top old new
  top="$(git rev-parse --show-toplevel)" || return 0
  [[ "$top" == "$primary/.claude/worktrees/"* ]] || return 0
  old="$(git rev-parse HEAD)" new="$(git rev-parse origin/main)" || return 0
  [[ "$old" != "$new" ]] && git merge-base --is-ancestor "$old" "$new" || return 0
  [[ -z "$(git status --porcelain)" ]] || return 0
  # App worktrees check out every LFS file as a pointer; skipping the smudge keeps that true.
  if ! GIT_LFS_SKIP_SMUDGE=1 git merge -q --ff-only "$new" > /dev/null; then
    echo "Could not fast-forward this session worktree: it is $(commits "$old..$new") behind origin/main."
    return 0
  fi
  echo "Fast-forwarded this session worktree $(commits "$old..$new") to origin/main (${old:0:9}..${new:0:9})."
  if ! git diff --quiet "$old" "$new" -- AGENTS.md CLAUDE.md; then
    echo "AGENTS.md changed in that range, and the copy in your context predates it: re-read AGENTS.md before acting."
  fi
}

report_primary_main() {
  local primary="$1" main path
  local -a incoming=() equal=() differ=()
  local -A unstaged=() off_head=() off_new=() untracked=()
  [[ "$(git -C "$primary" symbolic-ref -q HEAD)" == refs/heads/main ]] || return 0
  main="$(git rev-parse refs/heads/main)"
  git merge-base --is-ancestor origin/main "$main" && return 0
  if ! git merge-base --is-ancestor "$main" origin/main; then
    echo "The primary's main has commits origin/main lacks, so \`git pull --ff-only\` there cannot fast-forward. Tell the owner."
    return 0
  fi
  mapfile -d '' -t incoming < <(git diff --name-only --no-renames -z "$main" origin/main)
  read_set unstaged < <(git -C "$primary" diff --name-only --no-renames -z)
  read_set off_head < <(git -C "$primary" diff --cached --name-only --no-renames -z)
  read_set off_new < <(git -C "$primary" diff --cached --name-only --no-renames -z origin/main)
  read_set untracked < <(git -C "$primary" ls-files --others --exclude-standard -z)
  for path in "${incoming[@]}"; do
    # git refuses these; a path staged to equal origin/main already goes through.
    [[ ${unstaged[$path]+x}${untracked[$path]+x} || (${off_head[$path]+x} && ${off_new[$path]+x}) ]] || continue
    if [[ "$(worktree_blob "$primary" "$path")" == "$(git rev-parse -q --verify "origin/main:$path")" ]]; then
      equal+=("$path")
    else
      differ+=("$path")
    fi
  done
  ((${#equal[@]} + ${#differ[@]})) || return 0
  echo "The primary's main is $(commits "$main..origin/main") behind origin/main, and \`git pull --ff-only\` there would be refused: incoming commits change these uncommitted paths."
  if ((${#equal[@]})); then
    echo "Already equal to origin/main, so staging them (\`git add\`) lets the pull through (doc/agents/audio-pipeline.md → Where sound work happens):"
    list "${equal[@]}"
  fi
  if ((${#differ[@]})); then
    echo "Different from origin/main, so the pull waits on these. Tell the owner, and leave the edits where they are:"
    list "${differ[@]}"
  fi
}

report_strays() {
  local primary="$1" entry status path sound=0
  local -a dirty=()
  while IFS= read -r -d '' entry; do
    status="${entry:0:2}" path="${entry:3}"
    case "$status" in
      '??') matches "$path" "${SOUND[@]}" && { ((++sound)); continue; } ;;
      '!!') matches "$path" "${ALLOW[@]}" && continue ;;
      *) continue ;;
    esac
    dirty+=("$status $path")
  done < <(git -C "$primary" status --porcelain=v1 -z --ignored --untracked-files=normal)

  if ((sound)); then
    echo "Sound work in progress, not strays: $(plural "$sound" "untracked path") under audio/, src/Asteroids3D/Assets/Audio/ or a Scripts Audio/ folder"
    echo "(doc/agents/audio-pipeline.md → Where sound work happens). They wait for the ship; leave them."
  fi
  if ((${#dirty[@]})); then
    echo "Primary tree $primary is not clean: these untracked (??) or ignored (!!) paths are outside the allowlist."
    echo "Triage these now. Agents write there only to fast-forward main and for sound work (AGENTS.md, Default workflow):"
    echo "move each item to its home (a slot, an issue, an evidence/* or research branch, a PR), or ask the owner."
    echo "Never delete one without the owner's yes; it may be the only copy."
    list "${dirty[@]}"
  fi
  echo "PRIMARY_TREE_DIRTY=${#dirty[@]}"
}

main() {
  local common primary
  common="$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || {
    echo "PRIMARY_TREE_DIRTY=0"
    return
  }
  primary="$(dirname "$common")"
  if git remote get-url origin > /dev/null 2>&1; then
    if GIT_TERMINAL_PROMPT=0 timeout "$FETCH_SECONDS" git fetch -q origin main; then
      fast_forward_session "$primary"
      report_primary_main "$primary"
    else
      echo "Could not fetch origin main, so this session's base and the primary's main went unchecked."
    fi
  fi
  report_strays "$primary"
}

# Bash reads a script as it runs; parsing main whole first lets the fast-forward rewrite this file.
main "$@"; exit 0
