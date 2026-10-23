"""PreToolUse hook (Bash, PowerShell): refuse a worktree past agent-8.

The pool is agent-1 through agent-8. acquire parks the longest-idle slot when
those eight are leased. A ninth checkout is another Unity Library.

Interface (Claude Code hook contract), exit 0 on well-formed hook JSON:
  stdin   the hook JSON; reads tool_input.command.
  stdout  nothing when allowed; when refused, one JSON line with
          hookSpecificOutput.permissionDecision "deny" and a
          permissionDecisionReason naming the cap.
"""

import json
import re
import shlex
import sys

SEPARATORS = {";", "&&", "||", "|"}
WRAPPERS = {"sudo", "command", "time", "builtin", "exec", "env"}
GIT_OPTS = {"-C", "-c", "--git-dir", "--work-tree"}
ASSIGN = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
AGENT = re.compile(r"(?:^|/)agent-(\d+)$", re.I)


def over_cap_name(token):
    token = token.replace("\\", "/").rstrip("/")
    match = AGENT.search(token)
    if match and int(match.group(1)) > 8:
        return f"agent-{int(match.group(1))}"
    return None


def segments(tokens):
    current = []
    for token in tokens:
        if token in SEPARATORS:
            if current:
                yield current
            current = []
        else:
            current.append(token)
    if current:
        yield current


def command_tokens(segment):
    index = 0
    while index < len(segment):
        if ASSIGN.match(segment[index]) or segment[index] in WRAPPERS:
            index += 1
            continue
        break
    return segment[index:]


def worktree_add_over_cap(tokens):
    if not tokens:
        return None
    head = tokens[0].replace("\\", "/").rsplit("/", 1)[-1]
    if head not in ("git", "git.exe"):
        return None
    index = 1
    while index < len(tokens):
        if tokens[index] in GIT_OPTS and index + 1 < len(tokens):
            index += 2
            continue
        if tokens[index].startswith("-"):
            index += 1
            continue
        break
    if index + 1 >= len(tokens) or tokens[index] != "worktree" or tokens[index + 1] != "add":
        return None
    for token in tokens[index + 2:]:
        name = over_cap_name(token)
        if name:
            return name
    return None


def adding_over_cap(cmd):
    try:
        tokens = shlex.split(cmd, posix=True)
    except ValueError:
        if re.search(r"\bgit\b", cmd) and re.search(r"\bworktree\b", cmd) and re.search(r"\badd\b", cmd):
            match = re.search(r"agent-(\d+)", cmd)
            if match and int(match.group(1)) > 8:
                return f"agent-{int(match.group(1))}"
        return None
    for segment in segments(tokens):
        name = worktree_add_over_cap(command_tokens(segment))
        if name:
            return name
    return None


def main():
    try:
        data = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    cmd = (data.get("tool_input") or {}).get("command") or ""
    over = adding_over_cap(cmd)
    if not over:
        return 0
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": "deny",
        "permissionDecisionReason": (
            f"pool-cap guard: refused a worktree for {over}. The pool is agent-1 through "
            "agent-8. acquire parks the longest-idle slot that has no Unity editor and no "
            "merge gate."),
    }}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
