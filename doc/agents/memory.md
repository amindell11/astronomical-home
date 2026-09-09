# Agent memory

> STATUS: living — what agent memory may and may not hold; pointed at from `AGENTS.md`.

Memory is the primary session's file directory
`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\`
(`MEMORY.md` = index, auto-loaded; worktree agents resolve a different dir and
must use this absolute path). It holds **nothing repo-critical** (ruling
2026-09-03): the tracker and `doc/agents/` do. Two layers only:

- **Working memory** — `active_work_ledger.md` (live claims), the session
  handoff files a consuming session deletes, and one-line *links* to active
  arcs (`Arc → #N`). Never the arc's content.
- **Taste and interaction** — `user_*` / `feedback_*`: preferences, how the
  user likes to work, interaction notes (e.g. "easing back in"), tidbits the
  user says. Allowed to drift; trimmed and reassessed regularly.

If a fact would hurt the repo when it drifts, it does not belong here: a
decision or result → the issue; a rule → `doc/agents/` or `AGENTS.md`; an
environment fact → `doc/agents/environment.md`; a runbook → the tool's own
README. Answer design questions with the `design-lookup` agent, never from
memory.
