---
name: agent-worktree-pr-loop
description: Manage warm Unity agent worktrees and PR feedback loops in this repo. Use when asked to start a new agent task in agent-1/agent-2/agent-3, create/update PRs, address review comments, or run the worktree pool pipeline.
metadata:
  project: astronomical-home
  primary-script: scripts/agent_worktree_pool.sh
---

# Agent Worktree + PR Loop

Use this skill for this repo's pooled worktree workflow.

## Core commands

- `./scripts/agent_worktree_pool.sh status`
- `./scripts/agent_worktree_pool.sh acquire <lease-id>`
- `./scripts/agent_worktree_pool.sh prepare <slot> origin/main`
- `./scripts/agent_worktree_pool.sh run-tests <slot> -- <unity_test_agent.ps1 args>`
- `./scripts/agent_worktree_pool.sh create-pr <slot>`
- `./scripts/agent_worktree_pool.sh submit <slot> origin/main -- <test args>`
- `./scripts/agent_worktree_pool.sh review-comments <slot>`
- `./scripts/agent_worktree_pool.sh revise <slot> -- <test args>`
- `./scripts/agent_worktree_pool.sh finalize <slot> origin/main`
- `./scripts/agent_worktree_pool.sh release <slot>`

## Two distinct flows

### A) New task flow (from main)

1. Acquire a free slot.
2. Implement changes in that slot worktree.
3. Run `submit` to run tests and create PR (**lock is kept**):

```bash
./scripts/agent_worktree_pool.sh submit agent-<n> origin/main -- -Mode Both -ScopeType Workspace
```

4. Wait for PR review feedback. Use `review-comments` and `revise` (flow B) as needed.
5. Once the PR is merged, run `finalize` to reset the slot and release the lock:

```bash
./scripts/agent_worktree_pool.sh finalize agent-<n> origin/main
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

## Required reporting format

When completing a slot task, respond with:

- **Slot:** `<agent-n>`
- **Flow:** `new-task` or `review-revision`
- **PR:** `<url or existing/open status>`
- **Comments addressed:** `<count or bullets>`
- **Files changed:** `<paths>`
- **Tests:** `<command(s)>` + `passed/failed summary`
- **Unknowns/Risks:** `<explicit bullets>`
