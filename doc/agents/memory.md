# Agent memory

> STATUS: living — what agent memory may and may not hold; pointed at from `AGENTS.md`.

Memory is the primary session's file directory
`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\`
(`MEMORY.md` = index, auto-loaded; worktree agents resolve a different dir and
must use this absolute path). It holds **feedback notes only** (`feedback_*`): the user's preferences, how
they like to work, corrections worth keeping. Allowed to drift; trimmed and
reassessed regularly. Nothing repo-critical lives here (ruling 2026-09-03), and
since 2026-09-22 no working state either:

- in-flight work — the pool (`./scripts/worktree_dashboard.sh`,
  `agent_worktree_pool.sh status`: leases, held work) plus open PRs;
- active arcs — `gh issue list --label arc --state open` and the board;
- handoffs — the spawn-chip prompt that starts the fresh session.

If a fact would hurt the repo when it drifts, it does not belong here: a
decision or result → the issue; a rule → `doc/agents/` or `AGENTS.md`; an
environment fact → `doc/agents/environment.md`; a runbook → the tool's own
README. Answer design questions with the `design-lookup` agent, never from
memory.
