"""Negated close: a closing keyword GitHub still parses as closing #N although a negation precedes it.

GitHub's keyword parser ignores negation, so "Does not close #617" closes #617 on merge.
Callers: merge_reconcile.sh (import: the mismatch check) and agent_worktree_pool.sh
create-pr/submit (CLI: refuse the body before the PR exists).

CLI: python3 negated_close.py < body.md
  Exit 0 no match · 2 match. Each offending line and a suggested rewording go to stderr.
"""
import re
import sys

# GitHub's keyword set only: "closing"/"resolving" are not keywords, so a negated one closes nothing.
NEGATED_CLOSE = re.compile(
    r"(?:not|n['’]t|never)\s+(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?):?\s+#(\d+)\b",
    re.IGNORECASE,
)


def main():
    body = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    hit = False
    for lineno, line in enumerate(body.splitlines(), 1):
        numbers = NEGATED_CLOSE.findall(line)
        if not numbers:
            continue
        hit = True
        print(f"line {lineno}: {line.strip()}", file=sys.stderr)
        for n in numbers:
            print(f"  GitHub closes #{n} on merge despite the negation; write \"Relates to #{n}\" or \"Refs #{n}\" instead.", file=sys.stderr)
    return 2 if hit else 0


if __name__ == "__main__":
    sys.exit(main())
