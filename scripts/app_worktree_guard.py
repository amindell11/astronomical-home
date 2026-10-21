"""PreToolUse hook (mcp__ccd_session__start_session): refuse a session start that makes an app worktree.

The Claude desktop app gives a started session its own git worktree under
<primary>/.claude/worktrees/ when the call asks for one (`use_worktree: true`), and by default
when the calling session already sits in one. Agent work never happens there; sessions open in the
caller's folder and build in a pool slot (AGENTS.md, Default workflow).

Interface (Claude Code hook contract), exit 0 on well-formed hook JSON:
  stdin   the hook JSON; reads `cwd` and `tool_input`.
  stdout  nothing when allowed; when refused, one JSON line with
          hookSpecificOutput.permissionDecision "deny" and a permissionDecisionReason naming
          the fix (`use_worktree: false`).

Passes: starts at another place (`target` cloud/ssh, `cwd`, `host`, `preset`), and calls without
a brief (a title-only offer, a proposal started as proposed), which were judged when they carried it.
"""

import json
import sys

ELSEWHERE = ("cwd", "host", "preset")


def in_app_worktree(cwd):
    return "/.claude/worktrees/" in cwd.replace("\\", "/").casefold() + "/"


def makes_worktree(inp, in_app):
    if inp.get("target") in ("cloud", "ssh") or any(inp.get(k) for k in ELSEWHERE):
        return False
    if inp.get("context") == "fork":
        return in_app
    if "prompt" not in inp:
        return False
    wt = inp.get("use_worktree")
    return wt is True or (wt is None and in_app)


def main():
    data = json.load(sys.stdin)
    in_app = in_app_worktree(data.get("cwd") or "")
    if not makes_worktree(data.get("tool_input") or {}, in_app):
        return 0
    where = ("this session is in an app worktree, so a default or forked start makes another"
             if in_app else "`use_worktree: true` makes one")
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": "deny",
        "permissionDecisionReason": (
            f"app-worktree guard: refused — {where} under .claude/worktrees/ in the primary tree, and "
            "agent work never happens there (AGENTS.md, Default workflow). Start it fresh with "
            "`use_worktree: false`: it opens in this folder and acquires a pool slot for any build."),
    }}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
