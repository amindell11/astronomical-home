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
