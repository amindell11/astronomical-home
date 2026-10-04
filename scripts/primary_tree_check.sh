#!/usr/bin/env bash
# SessionStart hook: surface what makes the primary tree unclean, so the session triages it.
#
# Clean primary = no untracked files and no ignored files outside ALLOW below; tracked
# modifications are the owner's and never reported. The primary tree is resolved from
# --git-common-dir, so the check reads the same tree from a slot or the primary itself.
#
# Interface: exit 0 always (a hook failure must not block a session).
#   stdout  the dirty listing with triage instructions (Claude Code adds it to context),
#           then the trailer PRIMARY_TREE_DIRTY=<count>; a clean tree prints the trailer alone.
#   stderr  git errors only.
# Never runs Unity; one `git status` call.
set -uo pipefail

ALLOW=(
  'src/Asteroids3D/Library/' 'src/Asteroids3D/Temp/' 'src/Asteroids3D/Logs/'
  'src/Asteroids3D/obj/' 'src/Asteroids3D/UserSettings/' 'src/Asteroids3D/*.csproj'
  '*.sln' '*.DotSettings' '.worktree-pool/' '.agents/' '.claude/scheduled_tasks.lock'
  'doc/design/.obsidian/workspace.json' 'doc/design/.obsidian/cache/' '__pycache__/' '*/__pycache__/'
)
MAX_LISTED=40

allowed() {
  local pattern
  for pattern in "${ALLOW[@]}"; do
    # shellcheck disable=SC2053  # the pattern is a glob on purpose
    [[ "$1" == $pattern ]] && return 0
  done
  return 1
}

common="$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || {
  echo "PRIMARY_TREE_DIRTY=0"
  exit 0
}
primary="$(dirname "$common")"

dirty=()
while IFS= read -r -d '' entry; do
  status="${entry:0:2}" path="${entry:3}"
  case "$status" in
    '??') ;;
    '!!') allowed "$path" && continue ;;
    *) continue ;;
  esac
  dirty+=("$status $path")
done < <(git -C "$primary" status --porcelain=v1 -z --ignored --untracked-files=normal)

if ((${#dirty[@]})); then
  echo "Primary tree $primary is not clean: these untracked (??) or ignored (!!) paths are outside the allowlist."
  echo "Triage these now. Agents never write into the primary tree (AGENTS.md, Default workflow):"
  echo "move each item to its home (a slot, an issue, an evidence/* or research branch, a PR), or ask the owner."
  echo "Never delete one without the owner's yes; it may be the only copy."
  printf '  %s\n' "${dirty[@]:0:MAX_LISTED}"
  ((${#dirty[@]} > MAX_LISTED)) && echo "  ... and $((${#dirty[@]} - MAX_LISTED)) more"
fi
echo "PRIMARY_TREE_DIRTY=${#dirty[@]}"
