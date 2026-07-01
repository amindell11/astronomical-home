---
name: agent-worktree-pr-loop
description: Default workflow for coding tasks in this repo — scope with the user, build/test in a warm agent-N worktree, open a PR, iterate on review, then merge and reset on explicit approval. Use for any new implementation task, not only when the user names a slot or worktree explicitly.
metadata:
  project: astronomical-home
  primary-script: scripts/agent_worktree_pool.sh
---

# Agent Worktree + PR Loop

This is the **default** implementation workflow for this repo (see
`CLAUDE.md`). Use it for new coding tasks even if the user doesn't mention
"worktree", "agent-1/2/3", or "PR" — those are implementation details, not
prerequisites for using this flow. It also covers the narrower flows (PR
feedback, ad hoc slot use) described below.

## Visibility commands

Before starting work or when reporting status, use the dashboard for a full overview:

- `./scripts/worktree_dashboard.sh` — rich view of all slots: lock status, branch, changed files, PRs, ahead/behind main
- `./scripts/worktree_dashboard.sh --watch` — auto-refresh every 5s (suggest to user for monitoring)

For interactive exploration, suggest the user run `lazygit` in any worktree directory. Press `w` in lazygit to see all worktrees and switch between them.

## Core commands

- `./scripts/agent_worktree_pool.sh status`
- `./scripts/agent_worktree_pool.sh acquire <lease-id>`
- `./scripts/agent_worktree_pool.sh prepare <slot> origin/main`
- `./scripts/agent_worktree_pool.sh run-tests <slot> -- <unity_test_agent.ps1 args>`
- `./scripts/agent_worktree_pool.sh create-pr <slot>`
- `./scripts/agent_worktree_pool.sh submit <slot> origin/main -- <test args>`
- `./scripts/agent_worktree_pool.sh review-comments <slot>`
- `./scripts/agent_worktree_pool.sh revise <slot> -- <test args>`
- `gh pr merge <n> --squash --delete-branch=false` (only after explicit user go-ahead)
- `./scripts/agent_worktree_pool.sh finalize <slot> origin/main`
- `./scripts/agent_worktree_pool.sh release <slot>`

## Two distinct flows

### A) New task flow (from main) — the default

0. **Scope with the user first.** Before touching a worktree, restate the
   task in a few sentences: what will change, which files/systems are
   affected, and what's explicitly out of scope. Get an explicit go-ahead.
   Do this every time, even for tasks that look small — this step is what
   prevents building the wrong thing in an agent's context before a human
   ever sees it. If the task is ambiguous, ask before proceeding (use
   AskUserQuestion for concrete decision points).
1. Acquire a free slot: `./scripts/agent_worktree_pool.sh acquire <lease-id>`.
2. Implement changes in that slot worktree — directly, or by delegating to a
   sub-agent (Agent tool) scoped to that worktree path when the task is large
   enough to benefit from an isolated context.
3. Run `submit` to run tests and create PR (**lock is kept**):

```bash
./scripts/agent_worktree_pool.sh submit agent-<n> origin/main -- -Mode Both -ScopeType Workspace
```

   Only submit once tests are passing and you've self-verified the diff
   (read it back, sanity-check it does what was scoped in step 0).
4. Report back to the user in the required reporting format below and hand
   off for review.
5. **Review round-trip.** Wait for the user's review. If they leave PR
   comments or ask for changes in chat, use `review-comments` and `revise`
   (flow B) as needed. Repeat until they're satisfied.
6. **Merge only on explicit approval.** Once the user gives an explicit
   go-ahead to merge (e.g. "merge it", "ship it", "go ahead") — not merely
   approving the code with no merge instruction — squash-merge:

```bash
gh pr merge <n> --squash --delete-branch=false
```

   Never merge without that explicit signal. Never force-push or skip CI to
   get there.
7. Finalize: reset the slot to base and release the lock:

```bash
./scripts/agent_worktree_pool.sh finalize agent-<n> origin/main
```

8. Sync local main so the primary worktree reflects the merge:

```bash
git checkout main && git pull
```

### B) PR feedback flow (no reset)

1. Inspect unresolved comments:

```bash
./scripts/agent_worktree_pool.sh review-comments agent-<n>
```

2. Implement requested changes on same slot branch.
3. Push updates with `revise` (pull/rebase + tests + push):

```bash
./scripts/agent_worktree_pool.sh revise agent-<n> -- -Mode Smoke
```

## Branch naming

Each task gets its own remote branch: `task/<lease-id>`.
The local worktree stays on the `agent-N` branch; `submit` pushes to the
task-specific remote branch automatically. This ensures each task has its
own PR even when the same slot is reused.

## Guardrails

- Each task gets its own remote branch (`task/<lease-id>`) and PR.
  The local worktree stays on the `agent-N` branch; `submit` pushes to
  the task-specific remote branch automatically.
- Do **not** run `prepare` during feedback rounds unless user explicitly asks to restart from main.
- Do not run two agents in the same slot at once.
- Prefer targeted/smoke tests during iteration; run broader scope before handoff when requested.
- Do not merge (`gh pr merge`) without an explicit user go-ahead in the
  conversation. A merged code review comment ("LGTM") is not itself a merge
  instruction unless the user says so.

## Viewing diffs and history

For non-interactive contexts (agent reporting), use:
```bash
# Summary of what changed vs main
git -C "$(slot_path)" diff --stat origin/main
# Full diff
git -C "$(slot_path)" diff origin/main
# Commit log for the slot
git -C "$(slot_path)" log --oneline origin/main..HEAD
```

For interactive review, suggest the user open lazygit in the worktree:
```bash
lazygit -p D:/amind/git/agent-<n>
```

## Required reporting format

When completing a slot task, respond with:

- **Slot:** `<agent-n>`
- **Flow:** `new-task` or `review-revision`
- **PR:** `<url or existing/open status>`
- **Comments addressed:** `<count or bullets>`
- **Files changed:** `<paths>`
- **Tests:** `<command(s)>` + `passed/failed summary`
- **Unknowns/Risks:** `<explicit bullets>`

When starting or finishing, always run the dashboard and include its output:
```bash
./scripts/worktree_dashboard.sh
```
