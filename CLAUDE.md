# CLAUDE.md

See also `AGENTS.md` for Obsidian/design-doc conventions and test-artifact standards.

## Default workflow: agent-worktree PR loop

For **any new coding task** in this repo (bug fix, feature, refactor — not pure
Q&A or read-only exploration), the default execution path is the pooled
worktree + PR loop, not direct edits in the main working tree. Load and follow
`.claude/skills/agent-worktree-pr-loop/SKILL.md`.

Summary of the loop (details in the skill file):

1. **Scope first.** Before acquiring a worktree slot, restate the task back to
   the user in a few sentences — what will change, which files/systems are
   touched, what's out of scope — and get explicit confirmation. Always do
   this, even for tasks that look small; skipping it is the main failure mode
   this workflow is meant to prevent.
2. **Build in a warm worktree.** Acquire an `agent-N` slot and do the
   implementation and testing there (optionally via a sub-agent), not in the
   primary worktree.
3. **PR once green.** Once tests pass and the diff is self-verified, run
   `submit` to push and open a PR. Report back using the skill's required
   reporting format.
4. **Review round-trip.** Wait for the user's review. If they leave PR
   comments or ask for changes in chat, use `revise` to address them and
   re-push. Repeat until the user says it's good.
5. **Merge on explicit approval.** Only after the user gives an explicit go
   (e.g. "merge it", "ship it", "looks good") — not just "looks good" about
   the code with no merge instruction implied elsewhere — squash-merge the PR:
   `gh pr merge <n> --squash --delete-branch=false`. Never merge without that
   explicit signal, and never force-push or bypass CI to get there.
6. **Finalize.** After merge, run
   `./scripts/agent_worktree_pool.sh finalize <slot> origin/main` to reset the
   slot to main and release the lock, then pull `origin/main` into the local
   primary worktree (`git checkout main && git pull`) so local main matches.

This applies by default — the user doesn't need to say "use the worktree
pool" or name a slot for it to kick in. Exceptions: trivial doc/comment-only
edits the user explicitly asks to be made directly, or explicit instruction
to work in place instead.

### Comment hygiene across the PR lifecycle

While a PR is in flight, comments that explain *what changed and why* — the bug
that was fixed, the reasoning behind a change, before/after context — are
encouraged; they help review. But those are review-time scaffolding, not
permanent documentation. As soon as the PR is approved to merge (and before you
squash-merge), strip that changelog-style narration from the code, leaving only
brief, concise comments that describe the *current* implementation. A reader of
`main` should never see comments framed around a past state ("was 10f, now uses
projectile speed", "fixed the leak by…") — only what the code does now.

## Dependency & wiring philosophy

How state reaches code in this project — follow these when adding any new
dependency, and prefer zero new wiring over new seams:

1. **Per-ship dependencies enter a component exactly once, through
   `Initialize(...)` parameters** (see `Scout.Initialize`, `Navigator.Initialize`).
   Never add ad-hoc `Set<Thing>()` setters per feature — if a component needs a
   new per-ship dependency, extend its Initialize signature.
2. **Keep knowledge at its abstraction level.** A composer (Scout, AICommander)
   only instantiates and sequences its parts; domain math and configuration
   (scan envelopes, cost shapes, query extents) live inside the part that uses
   them. If a field on a high-level object only exists to be forwarded
   downward, it belongs downward.
3. **Do not thread world/session-scoped state through per-ship wiring**
   (Commander/UnitService pass-throughs, service-interface additions) just to
   hand it to a component. How world-scoped state SHOULD reach consumers is a
   deliberately open question — multiple arena instances per process are
   planned (RL training), so process-wide statics are equally suspect long-term.
   Until that design lands: keep any such seam as narrow as possible, mark it
   interim (see `ObstacleFields`), don't copy it to new systems, and raise the
   question with the user rather than inventing a pattern.
4. **The smell to catch mid-diff:** if wiring ONE new dependency touches
   bootstrap + a service interface + Commander/UnitService + the consuming
   component, stop and reclassify before pushing.
