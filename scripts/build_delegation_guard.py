"""PreToolUse hook (Edit, Write): refuse inline source edits in a slot once the session is large.

A large main session hands a PR's build to a build worker (agent-worktree-pr-loop, Step 2):
building inline there re-spends the whole context on every edit. Small fixes stay inline.

Interface (Claude Code hook contract), exit 0 on well-formed hook JSON:
  stdin   the hook JSON; reads agent_id, tool_name, tool_input and transcript_path.
  stdout  nothing when allowed; when refused, one JSON line with
          hookSpecificOutput.permissionDecision "deny" and a permissionDecisionReason naming
          both measurements and the hand-off to a build worker.

Only a main-session call (no agent_id) that edits or writes a SLOT_SOURCE file is measured:
  context       the last assistant entry's input + cache-read + cache-creation tokens
  inline lines  each earlier Edit/Write of a SLOT_SOURCE file in the transcript whose result
                was not an error (Edit: the longer of old and new string; Write: the content),
                plus this call's
Refused when context >= CONTEXT_BUDGET and inline lines > INLINE_LINE_BUDGET. A missing or
unreadable transcript cannot be measured, so the call is allowed.
"""

import json
import os
import posixpath
import re
import sys

CONTEXT_BUDGET = 200_000
INLINE_LINE_BUDGET = 40
TAIL_CHUNK = 1 << 18

TOOLS = {"Edit", "Write"}
SLOT_SOURCE = re.compile(
    r"^d:/amind/git/agent-[1-8]/src/.+\.(cs|inputactions|asmdef|shader|hlsl|compute|uss|uxml)$", re.I)
CONTEXT_KEYS = ("input_tokens", "cache_read_input_tokens", "cache_creation_input_tokens")


def is_slot_source(path):
    return bool(path) and bool(SLOT_SOURCE.match(posixpath.normpath(path.replace("\\", "/"))))


def changed_lines(tool, tool_input):
    if tool == "Edit":
        return max(len((tool_input.get("old_string") or "").splitlines()),
                   len((tool_input.get("new_string") or "").splitlines()))
    return len((tool_input.get("content") or "").splitlines())


def parse(line):
    try:
        return json.loads(line)
    except ValueError:
        return None


def assistant_context(line):
    if b'"assistant"' not in line or b'"usage"' not in line:
        return None
    entry = parse(line)
    if not entry or entry.get("type") != "assistant":
        return None
    usage = (entry.get("message") or {}).get("usage")
    return sum(usage.get(k) or 0 for k in CONTEXT_KEYS) if usage else None


def context_tokens(transcript):
    # Read backward in chunks: transcripts reach tens of MB.
    with open(transcript, "rb") as f:
        pos = f.seek(0, os.SEEK_END)
        rest = b""
        while pos > 0:
            step = min(TAIL_CHUNK, pos)
            pos -= step
            f.seek(pos)
            lines = (f.read(step) + rest).split(b"\n")
            rest = lines.pop(0) if pos > 0 else b""
            for line in reversed(lines):
                tokens = assistant_context(line)
                if tokens is not None:
                    return tokens
    return 0


def inline_lines(transcript):
    edits, succeeded = {}, set()
    with open(transcript, "rb") as f:
        for line in f:
            if b'"tool_use"' not in line and b'"tool_result"' not in line:
                continue
            content = ((parse(line) or {}).get("message") or {}).get("content")
            for block in content if isinstance(content, list) else ():
                if not isinstance(block, dict):
                    continue
                if block.get("type") == "tool_use" and block.get("name") in TOOLS:
                    args = block.get("input") or {}
                    if is_slot_source(args.get("file_path")):
                        edits[block.get("id")] = changed_lines(block["name"], args)
                elif block.get("type") == "tool_result" and not block.get("is_error"):
                    succeeded.add(block.get("tool_use_id"))
    return sum(n for use_id, n in edits.items() if use_id in succeeded)


def main():
    data = json.load(sys.stdin)
    tool, tool_input = data.get("tool_name"), data.get("tool_input") or {}
    transcript = data.get("transcript_path")
    if (data.get("agent_id") or tool not in TOOLS or not is_slot_source(tool_input.get("file_path"))
            or not transcript):
        return 0
    try:
        context = context_tokens(transcript)
        if context < CONTEXT_BUDGET:
            return 0
        lines = inline_lines(transcript) + changed_lines(tool, tool_input)
    except OSError:
        return 0
    if lines <= INLINE_LINE_BUDGET:
        return 0
    reason = (f"build-delegation guard: this session's context is {context:,} tokens (budget "
              f"{CONTEXT_BUDGET:,}) and its inline source edits in slots reach {lines} lines with this "
              f"call (budget {INLINE_LINE_BUDGET}). Hand the build to a build worker "
              "(agent-worktree-pr-loop, Step 2): `python scripts/grok_worker.py --slot <slot> --brief "
              "<file> --verify \"<cmd>\"` by default, or an Opus subagent (Agent tool, model: \"opus\") "
              "scoped to the slot. Small fixes under the budget stay inline. When no build worker can "
              "run, ask the user to `/compact`; inline edits pass once the context is under the budget.")
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                             "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
