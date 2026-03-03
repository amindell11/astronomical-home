---
name: refactor-pipeline
description: Scout → plan → execute refactor pipeline for targeted goals or broad package reviews.
---

## refactor-code-scout

Analyze this refactor request: {task}.
- Classify as targeted refactor vs broad package review.
- This is a code refactor scout step: do NOT use `obsidian-scout`/Obsidian vault notes unless explicitly requested by the user.
- Use embedded principles in your agent prompt (do not fetch historical plan docs unless explicitly requested).
- Produce a concise evidence-backed handoff.

## refactor-planner

Using the scout output below, create a phased refactor plan with risk controls and test strategy.

Scout output:
{previous}

## agent-worktree-finalizer

Use the agent-worktree-pr-loop skill to execute the refactor plan.
- Acquire a free slot (agent-1/agent-2/agent-3) and implement the plan in that worktree following the skill's guardrails.
- Run targeted validation first and broaden the scope as needed while keeping Unity test artifacts under results/unity-tests-agent.
- When ready to finalize, call `./scripts/agent_worktree_pool.sh finalize <slot> origin/main -- <test args>` (default `-Mode Both -ScopeType Workspace`) so the slot is reset, tests run, PRs created/updated, and the lock released.
- Report progress/results using the skill's required format and flag unknowns/risks explicitly.

Plan:
{previous}
