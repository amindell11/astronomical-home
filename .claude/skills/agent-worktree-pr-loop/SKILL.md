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
- `./scripts/agent_worktree_pool.sh acquire <lease-id> [slot]` — name a slot when you have a reason (warm Unity Library from related work, the ledger/dashboard shows affinity, or avoiding a slot with an open editor); a named slot that isn't free fails rather than falling back, so pick from the dashboard, don't guess. Omit for auto-pick (free slots before stale reclaims).
- `./scripts/agent_worktree_pool.sh prepare <slot> origin/main`
- `./scripts/agent_worktree_pool.sh run-tests <slot> <unity_test_agent.ps1 args>` (NO `--` — run-tests forwards args directly, e.g. `run-tests agent-4 -Mode Both -ScopeType Workspace`; the `--` separator is only for `submit`/`revise`, which take a base_ref first)
- `./scripts/agent_worktree_pool.sh create-pr <slot>`
- `./scripts/agent_worktree_pool.sh submit <slot> origin/main -- <test args>`
- `./scripts/agent_worktree_pool.sh review-comments <slot>`
- `./scripts/agent_worktree_pool.sh revise <slot> -- <test args>`
- `./scripts/agent_worktree_pool.sh merge <slot>` (only after explicit user go-ahead; re-tests against current main if it moved, then squash-merges — never call `gh pr merge` directly)
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
   AskUserQuestion for concrete decision points). **Read the active-work
   ledger** (see `CLAUDE.md` → "Cross-agent work ledger", absolute path in
   the primary session's memory dir) so you don't collide with in-flight work.
1. Acquire a free slot: `./scripts/agent_worktree_pool.sh acquire <lease-id>`.
   Then **claim it in the ledger**: add a `🟡 in-progress` row with the
   slot·branch and a link to the plan file / driving memory (re-read the
   ledger right before editing so concurrent edits merge cleanly).
2. Implement changes in that slot worktree — directly, or by delegating to a
   sub-agent (Agent tool) scoped to that worktree path when the task is large
   enough to benefit from an isolated context.
3. Run `submit` to run tests and create PR (**lock is kept**):

```bash
./scripts/agent_worktree_pool.sh submit agent-<n> origin/main -- -Mode Both -ScopeType Workspace
```

   Only submit once tests are passing and you've self-verified the diff
   (read it back, sanity-check it does what was scoped in step 0). Once the
   PR is open, **flip the ledger row to `🔵 in-review` and record the PR
   number.**
4. Report back to the user in the required reporting format below and hand
   off for review.
5. **Review round-trip.** Wait for the user's review. If they leave PR
   comments or ask for changes in chat, use `review-comments` and `revise`
   (flow B) as needed. Repeat until they're satisfied.
6. **Sweep open comments, then merge only on explicit approval.** Once the
   user gives an explicit go-ahead to merge (e.g. "merge it", "ship it", "go
   ahead") — not merely approving the code with no merge instruction — do a
   final comment sweep *before* merging so nothing gets buried:

   a. Pull every unresolved comment:
      `./scripts/agent_worktree_pool.sh review-comments agent-<n>`.
   b. For each unaddressed comment, decide and act:
      - **Trivial** (typo, rename, comment hygiene, a one-line guard, obvious
        cleanup) → just fix it on the slot branch and `revise`.
      - **Involved** (behavior change, design question, non-obvious tradeoff,
        anything you're unsure how the user wants resolved) → do **not**
        silently merge over it. Surface it to the user with a one-line summary
        of the comment and a concrete proposed fix, and get direction first.
   c. **Run a simplification pass on the diff.** Invoke `/simplify` (or the
      `code-simplifier` agent) scoped to the changed code to catch
      reuse/simplification/altitude cleanups the review round-trip missed.
      Fold its fixes into the slot branch and `revise`. This sits alongside
      the comment-hygiene pass — strip changelog-style narration from
      comments so `main` shows only what the current code does (see
      `CLAUDE.md` → "Comment hygiene across the PR lifecycle"). Keep the pass
      quality-only; it is not a bug hunt (`/code-review` is for that), and
      surface anything non-trivial it turns up rather than silently reshaping
      behavior right before merge.
   d. Only once no unaddressed comment remains — or the user has explicitly
      waved the remaining ones through — and the simplify pass is folded in,
      squash-merge through the gate:

```bash
./scripts/agent_worktree_pool.sh merge agent-<n>
```

   The gate exists because `submit`/`revise` test the branch on the base it
   last synced to — if main moved since, two individually-green PRs can land
   a broken main with no textual conflict (see #105/#106: a required ctor
   param added under a test that predated it). `merge` checks whether
   `origin/main` is contained in the slot branch; if not, it merges main in,
   re-runs the full suite, pushes, and only then squash-merges — so the
   tested tree is the tree that lands. Never call `gh pr merge` directly.

   Never merge without that explicit signal. Never force-push or skip the
   gate's test run to get there, and never merge past an unaddressed
   non-trivial comment without flagging it.
7. Finalize: reset the slot to base and release the lock:

```bash
./scripts/agent_worktree_pool.sh finalize agent-<n> origin/main
```

8. Sync local main so the primary worktree reflects the merge:

```bash
git checkout main && git pull
```

9. **Clear the ledger row.** Once local `main` reflects the merge, mark the
   row `✅ merged` and delete it (or move it to the ledger's Archive).

### B) PR feedback flow (no reset)

1. Inspect unresolved comments:

```bash
./scripts/agent_worktree_pool.sh review-comments agent-<n>
```

2. Implement requested changes on same slot branch.
3. Push updates with `revise` (pull/rebase + tests + push). Valid `-Mode`
   values are `Both`/`EditMode`/`PlayMode` (`Smoke` is a `-ScopeType`, not a
   mode):

```bash
./scripts/agent_worktree_pool.sh revise agent-<n> -- -Mode Both -ScopeType Workspace
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
- Do not merge without an explicit user go-ahead in the conversation. A
  merged code review comment ("LGTM") is not itself a merge instruction
  unless the user says so.
- Merge ONLY via `./scripts/agent_worktree_pool.sh merge <slot>` — never raw
  `gh pr merge`. The pool command is the compile/test gate on main: it
  re-tests against current main when main moved after the branch's last test
  run, which raw `gh pr merge` silently skips.
- Before merging, sweep unresolved PR comments (step 6): fix trivial ones
  directly and `revise`; for involved ones, flag them to the user with a
  proposed fix rather than merging over them silently. Also run a `/simplify`
  pass and the comment-hygiene strip on the diff as part of that pre-merge
  sweep.
- Keep the active-work ledger current: claim on acquire, `🔵 in-review` on PR
  open, `⛔ blocked`/`🅿️ parked` if work stalls, and clear the row after merge
  + main sync. It is the one place a concurrent agent or a later session can
  see this slot is taken.

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
