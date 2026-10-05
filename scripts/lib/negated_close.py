"""Negated and stray close: closing keywords GitHub parses as closing #N where the author meant otherwise.

GitHub's keyword parser ignores negation, so "Does not close #617" closes #617 on merge, and it
ignores quotation too, so a body quoting another PR's keyword closes that PR's issue. The CLI
therefore accepts one shape only: a line that starts, after optional whitespace, with `Closes #N`.
Callers: merge_reconcile.sh (import: NEGATED_CLOSE in the mismatch check) and
agent_worktree_pool.sh create-pr/submit (CLI: refuse the body before the PR exists).

CLI: python3 negated_close.py < body.md
  Exit 0 no match · 2 match. Each offending line and a suggested rewording go to stderr.
"""
import re
import sys

# GitHub's keyword set only: "closing"/"resolving" are not keywords, so a negated one closes nothing.
KEYWORD = r"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?):?\s+((?:[\w.-]+/[\w.-]+)?#\d+)\b"
NEGATED_CLOSE = re.compile(r"(?:not|n['’]t|never)\s+" + KEYWORD, re.IGNORECASE)
ANY_CLOSE = re.compile(r"\b" + KEYWORD, re.IGNORECASE)
DECLARED_CLOSE = re.compile(r"^\s*Closes (?:[\w.-]+/[\w.-]+)?#\d+\b")


def main():
    body = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    hit = False
    for lineno, line in enumerate(body.splitlines(), 1):
        negated = list(NEGATED_CLOSE.finditer(line))
        declared = DECLARED_CLOSE.match(line)
        exempt = [m.span() for m in negated] + ([declared.span()] if declared else [])
        stray = [m for m in ANY_CLOSE.finditer(line)
                 if not any(lo <= m.start() < hi for lo, hi in exempt)]
        if not negated and not stray:
            continue
        hit = True
        print(f"line {lineno}: {line.strip()}", file=sys.stderr)
        for m in negated:
            ref = m.group(1)
            print(f"  GitHub closes {ref} on merge despite the negation; write \"Relates to {ref}\" or \"Refs {ref}\" instead.", file=sys.stderr)
        for m in stray:
            ref = m.group(1)
            print(f"  GitHub closes {ref} on merge wherever a closing keyword names it, quoted or not; write \"Refs {ref}\", or start a line with \"Closes {ref}\" if this PR ends it.", file=sys.stderr)
    return 2 if hit else 0


if __name__ == "__main__":
    sys.exit(main())
