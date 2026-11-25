#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/app_worktree_guard.py

# Hermetic regression for the start_session hook. The guard is fed hook JSON from a session in the
# primary folder and from one in an app worktree, and must deny exactly the starts that make an
# app worktree: an explicit `use_worktree: true`, or a default or forked start from an app worktree.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$SCRIPT_DIR/../app_worktree_guard.py"

fail() { echo "FAIL: $1" >&2; exit 1; }

PRIMARY='D:\repo'
APP='D:\repo\.claude\worktrees\gentle-fox-1a2b3c'

verdict() {
  local out
  out="$(python3 -c 'import json,sys; print(json.dumps({"tool_name": "mcp__ccd_session__start_session", "cwd": sys.argv[1], "tool_input": json.loads(sys.argv[2])}))' "$1" "$2" |
    python3 "$GUARD")" || fail "guard exited non-zero on: $2"
  if [[ -z "$out" ]]; then echo allow
  elif [[ "$out" == *'"permissionDecision": "deny"'*'use_worktree: false'* ]]; then echo deny
  else echo "malformed: $out"
  fi
}
expect() {
  local got
  got="$(verdict "$2" "$3")"
  [[ "$got" == "$1" ]] || fail "expected $1, got $got — cwd=$2 input: $3"
}

brief='"title": "t", "prompt": "p", "initiation": "own_initiative"'

# --- from the primary folder: only an explicit request makes a worktree ---
expect deny "$PRIMARY" "{$brief, \"use_worktree\": true}"
expect allow "$PRIMARY" "{$brief, \"use_worktree\": false}"
expect allow "$PRIMARY" "{$brief}"
expect allow "$PRIMARY" "{$brief, \"context\": \"fork\"}"

# --- from an app worktree: the default and a fork make another ---
expect deny "$APP" "{$brief}"
expect deny "$APP" "{$brief, \"context\": \"fork\"}"
expect deny "$APP" "{$brief, \"use_worktree\": true}"
expect allow "$APP" "{$brief, \"use_worktree\": false}"
expect deny 'd:/repo/.claude/worktrees/gentle-fox-1a2b3c/src' "{$brief}"

# --- other places, and calls without a brief, pass ---
expect allow "$APP" "{$brief, \"target\": \"cloud\"}"
expect allow "$APP" "{$brief, \"target\": \"ssh\", \"host\": \"box\"}"
expect allow "$APP" "{$brief, \"cwd\": \"D:\\\\other\", \"use_worktree\": true}"
expect allow "$APP" "{$brief, \"preset\": \"mine\"}"
expect allow "$APP" '{"title": "t", "initiation": "offer"}'
expect allow "$APP" '{"proposal_id": "task_1"}'
expect deny "$APP" '{"proposal_id": "task_1", "prompt": "p"}'

echo "PASS: app-worktree guard"
