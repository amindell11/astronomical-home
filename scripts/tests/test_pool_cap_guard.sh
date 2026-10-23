#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/pool_cap_guard.py

# The pool stops at agent-8. git worktree add for a later agent-N is refused.
# A mention of that name in some other command is not.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$SCRIPT_DIR/../pool_cap_guard.py"

fail() { echo "FAIL: $1" >&2; exit 1; }

verdict() {
  local out
  out="$(python3 -c 'import json,sys; print(json.dumps({"tool_name":"Bash","cwd":"D:/repo","tool_input":{"command":sys.argv[1]}}))' "$1" |
    python3 "$GUARD")" || fail "guard exited non-zero on: $1"
  if [[ -z "$out" ]]; then echo allow
  elif [[ "$out" == *'"permissionDecision": "deny"'*'agent-1 through agent-8'* ]]; then echo deny
  else echo "malformed: $out"
  fi
}
expect() {
  local got
  got="$(verdict "$2")"
  [[ "$got" == "$1" ]] || fail "expected $1, got $got for: $2"
}

expect deny 'git worktree add -b agent-9 D:/amind/git/agent-9 origin/main'
expect deny 'git -C D:/amind/git/astronomical-home worktree add -b agent-10 /d/amind/git/agent-10'
expect deny 'git worktree add -b agent-15 D:\amind\git\agent-15 origin/main'
expect allow 'git worktree add -b agent-8 D:/amind/git/agent-8 origin/main'
expect allow 'git worktree add -b agent-1 D:/amind/git/agent-1 origin/main'
expect allow 'git worktree list'
expect allow 'git status'
expect allow 'echo git worktree add -b agent-9 D:/amind/git/agent-9'
expect allow 'cd /d/amind/git/astronomical-home && git status'
expect deny 'cd /d/amind/git/astronomical-home && git worktree add -b agent-9 D:/amind/git/agent-9 origin/main'

echo "PASS: pool-cap guard"
