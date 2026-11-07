#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/build_delegation_guard.py

# Hermetic regression for the build-delegation hook. Synthetic main-session transcripts carry the
# last assistant entry's usage (the context) and earlier Edit/Write tool uses with their results;
# the guard is fed hook JSON and must deny a main-session slot source edit only when the context
# and the inline source lines both pass their budgets. Target paths are never touched.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$SCRIPT_DIR/../build_delegation_guard.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
T="$(cd "$TMP" && { pwd -W 2>/dev/null || pwd; })"

fail() { echo "FAIL: $1" >&2; exit 1; }

SRC='D:/amind/git/agent-3/src/Asteroids3D/Assets/Scripts/Ship.cs'

# transcript <name> <context tokens> <ok edit lines> [<errored edit lines>]
transcript() {
  python3 - "$T/$1.jsonl" "$2" "$3" "${4:-0}" "$SRC" <<'EOF'
import json, sys
path, context, ok, errored, src = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
entries = []
def use(i, name, target, lines, is_error):
    body = "\n".join(f"line {n}" for n in range(lines))
    args = {"file_path": target, "content": body} if name == "Write" else \
           {"file_path": target, "old_string": "x", "new_string": body}
    entries.append({"type": "assistant", "message": {"usage": {"input_tokens": 9},
                    "content": [{"type": "tool_use", "id": f"t{i}", "name": name, "input": args}]}})
    entries.append({"type": "user", "message": {"content": [
        {"type": "tool_result", "tool_use_id": f"t{i}", "is_error": is_error, "content": "ok"}]}})
use(1, "Edit", src, ok // 2, False)
use(2, "Write", src.replace("Ship.cs", "Hull.cs"), ok - ok // 2, False)
use(3, "Edit", src, errored, True)
use(4, "Write", "D:/amind/git/agent-3/scripts/tool.py", 500, False)
use(5, "Edit", "D:/amind/git/astronomical-home/src/Asteroids3D/Assets/Scripts/Ship.cs", 500, False)
entries.append({"type": "assistant", "message": {"usage": {
    "input_tokens": 1, "cache_read_input_tokens": context - 101, "cache_creation_input_tokens": 100},
    "content": [{"type": "text", "text": "x" * 400000}]}})
entries.append({"type": "system", "content": "after the last assistant entry"})
with open(path, "w", encoding="utf-8") as f:
    for e in entries:
        f.write(json.dumps(e) + "\n")
EOF
}

# verdict <transcript name> <tool> <file_path> <pending lines> [<agent_id>]
verdict() {
  local out
  out="$(python3 - "$T/$1.jsonl" "$2" "$3" "$4" "${5:-}" <<'EOF' | python3 "$GUARD"
import json, sys
transcript, tool, target, lines, agent = sys.argv[1:6]
body = "\n".join("y" for _ in range(int(lines)))
args = {"file_path": target, "content": body} if tool == "Write" else \
       {"file_path": target, "old_string": "y", "new_string": body}
hook = {"hook_event_name": "PreToolUse", "tool_name": tool, "tool_input": args, "transcript_path": transcript}
if agent:
    hook.update(agent_id=agent, agent_type="general-purpose")
print(json.dumps(hook))
EOF
  )" || fail "guard exited non-zero for $*"
  if [[ -z "$out" ]]; then echo allow
  elif [[ "$out" == *'"permissionDecision": "deny"'* ]]; then echo "deny $out"
  else echo "malformed: $out"
  fi
}
expect() {
  local want="$1" got
  shift
  got="$(verdict "$@")"
  [[ "${got%% *}" == "$want" ]] || fail "expected $want, got $got — for $*"
}

transcript high 250000 30 60
transcript low 150000 100
transcript small 250000 10

# --- over budget at high context: 30 earlier lines (the errored 60 do not count) + 20 pending ---
expect deny high Edit "$SRC" 20
got="$(verdict high Write "$SRC" 20)"
[[ "$got" == *"context is 250,000 tokens"* && "$got" == *"reach 50 lines"* ]] || fail "reason lacks the numbers: $got"
[[ "$got" == *"scripts/grok_worker.py --slot"* && "$got" == *'model: \"opus\"'* ]] || fail "reason lacks the way out: $got"
expect deny high Edit 'd:\amind\git\agent-3\src\Asteroids3D\Assets\Scripts\Ship.cs' 20
expect deny high Write 'D:/amind/git/agent-8/src/Asteroids3D/Assets/UI/Hud.uss' 20
expect deny small Write "$SRC" 41

# --- allowed ---
expect allow high Edit "$SRC" 10
expect allow small Edit "$SRC" 30
expect allow high Edit "$SRC" 20 a1b2c3
expect allow high Edit 'D:/amind/git/astronomical-home/src/Asteroids3D/Assets/Scripts/Ship.cs' 20
expect allow high Edit 'D:/amind/git/agent-3/scripts/build_delegation_guard.py' 20
expect allow high Edit 'D:/amind/git/agent-9/src/Asteroids3D/Assets/Scripts/Ship.cs' 20
expect allow high Edit 'D:/amind/git/agent-3/src/Asteroids3D/Assets/Scripts/Ship.cs.meta' 20
expect allow low Edit "$SRC" 20
expect allow missing Edit "$SRC" 20

echo "PASS: build-delegation guard"
