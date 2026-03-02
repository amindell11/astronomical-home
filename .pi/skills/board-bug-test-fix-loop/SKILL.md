---
name: board-bug-test-fix-loop
description: End-to-end bug workflow for Astronomical: select a bug from Engineering Project Board (or ask user), create a test-first plan, implement repro tests, run Unity tests, summarize results, ask for resolution direction, then dispatch workers to fix.
metadata:
  project: astronomical
  board-default: D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md
---

# Board Bug → Test → Fix Loop

Use this skill when the user wants a reproducible bug workflow driven by the project board.

## Required context order

1. Read Engineering Project Board first.
2. Use repo code/tests and board details as implementation context.

## Workflow contract

Execute in this order every time:

1. **Select bug**
   - If user named a bug, use it.
   - Else pick one open item from `## BUGS` on the board.
   - If no clear bug exists, ask user to choose.

2. **Collect detailed repro context from user**
   - Immediately ask: **"Please describe this bug in as much detail as possible so I can direct reproduction. Include expected behavior, actual behavior, exact steps, frequency, environment, and any logs/screenshots."**
   - If user provides partial info, ask focused follow-ups before planning.

3. **Create plan**
   - Create a plan with test-first steps:
     - characterize expected behavior
     - add failing/characterization tests to reproduce
     - run targeted tests
     - summarize failures + likely root cause files
     - ask user how bug should be resolved (behavior intent)

4. **Implement tests first**
   - Add or update tests only.
   - Do not change production behavior before reproduction evidence is captured.

5. **Run tests**
   - Run smallest targeted Unity tests first (`unity_test_run` when available).
   - Capture pass/fail totals and failing assertion snippets.

6. **Report + decision gate**
   - Return concise summary:
     - selected bug
     - files changed (tests/docs)
     - test command and totals
     - top failure(s)
   - Ask: **"How should this bug be resolved?"**
   - Wait for user direction before implementing fixes.

7. **Dispatch fix agents**
   - After user confirms intended behavior, dispatch workers.
   - Prefer parallel workers when bug splits cleanly (e.g., test expectation vs production fix).
   - Suggested model split:
     - scoped/mechanical fixes: Haiku 4.5
     - deeper/systemic fixes: Sonnet 4.5
   - Require each worker to: make minimal changes, run targeted tests, return changed files + output.

8. **Integrate + verify**
   - Apply/keep validated changes.
   - Re-run targeted tests.
   - Return final summary and any remaining risks.

## Output format (user-facing)

Use this compact structure:

- **Bug:** `<name>`
- **Context checked:** `Project Board.md`, relevant repo code/tests
- **Plan:** `<plan id or checklist>`
- **Tests:** `<command>` → `passed X / failed Y`
- **Key failures:** `<1-3 bullets>`
- **Question:** `How should this bug be resolved?`

After fix dispatch:

- **Agents dispatched:** `<agent/model/purpose>`
- **Changes applied:** `<file list>`
- **Verification:** `<command + totals>`
- **Status:** `resolved / partially resolved / blocked`

## Notes

- Prefer updating tests when failure is expectation mismatch.
- Prefer production changes only after user confirms intended behavior.
- Keep iteration tight: targeted tests, minimal diffs, clear evidence.
