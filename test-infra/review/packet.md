# Task: grade this repo's test infrastructure

You are an independent reviewer. Grade the test infrastructure of the repo at
`D:\amind\git\astronomical-home` against the rubric in
`reports/test-infra-rubric.md`. Read that file first and follow its scope,
criteria, anchors, evidence rules and output spec exactly, with the overrides
below.

Project context: solo-developer Unity project (Asteroids3D) built largely by
coding agents. Machinery must earn its place; simpler means fewer moving parts.
The owner cares most about two things: (a) green must mean green — no false
passes; (b) minimalism — maximum proven behavior per line of test code, no
pointless, self-satisfying, or change-detector tests. Read `AGENTS.md`,
`TESTING.md` and `doc/agents/testing.md` for the repo's own conventions.

## Overrides to the rubric's "Run constraints" (this run is STATIC-ONLY)

All worktree pool slots are leased and two Unity editors are live, so:

- Do NOT run any tests (no Unity, no `scripts/tests/*`, no
  `unity_test_agent.ps1`), no pool commands (`agent_worktree_pool.sh`), no
  `unity_access.ps1` actions other than reading the source.
- Do NOT modify, create or delete any file in the repo, file issues, or open
  PRs. Read-only commands are fine: reading files, `git log` / `git show` /
  `git grep`, and `gh issue view` / `gh pr view` / `gh issue list`.
- Anything the rubric wants probed hands-on (criterion 1 probes, criterion 10
  command checks, timing under load) is judged from the code instead. Trace the
  code path and say what would happen; mark each such conclusion
  `static — unverified` and give the exact command that would verify it.
- For prior-art checks, `reports/test-infra-review/issue-index.txt` lists every
  issue (number, state, title, labels). Use `gh issue view <N>` for bodies if
  the network is available; otherwise cite by title and say the body was unread.
- Do not read any other file under `reports/` that starts with
  `test-infra-grade` — a second reviewer is working independently and the two
  reports are compared afterward.

## Depth expectations

- Read test code in bulk, not just a sample of names: the Unity suite is ~185
  files / ~28.5k lines, the script suite ~4.7k lines. Criterion 2's mutation
  sample is a minimum of 10 tests spread over at least 6 domain categories;
  criterion 5 (Economy) needs named cut candidates with `file:line`, not
  generalities.
- For the change-detector signal in criterion 5, use git history: test files
  modified in commits whose production change was a refactor/rename.
- A score without cited evidence is invalid. A finding without a concrete
  scenario is an observation and must be labelled as one.
- Prefer fewer, well-evidenced findings over a long list. Say plainly where the
  infrastructure is good; do not manufacture problems.

## Output

Produce the report exactly as the rubric's "Output" section specifies
(scorecard with weighted total, findings, cut list, add list, net ledger,
issue-ready suggestions), in Markdown. End with a short "Confidence and
limits" section: what you read, what you did not, and which conclusions are
static-unverified.
