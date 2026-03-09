# AGENTS.md

Use `obsidian-scout` **only** for design/documentation requests in this repo.
Do **not** use `obsidian-scout` for code reconnaissance, refactor scouting, or implementation planning.

## Expectations
- For design/doc requests: research relevant notes in the Obsidian vault before proposing changes.
- For design/doc requests: cite note/file paths for non-obvious claims.
- For design/doc requests: respect Obsidian conventions (wikilinks, embeds, aliases, anchors, frontmatter).
- For all tasks: flag unknowns and ambiguities explicitly.
- Standardize Unity test artifacts to `results/unity-tests-agent` (including `unity_test_run` via explicit `outDir`).
- For PlayMode tests, prefer inheriting from `Tests.PlayMode.Common.PlayModeWorldFixture` when it makes sense (ensures GamePlane/test arena setup and cleanup).
- For requests mentioning `agent-1`/`agent-2`/`agent-3`, warm worktrees, PR review comments, or slot-based task flow, load and follow `.pi/skills/agent-worktree-pr-loop/SKILL.md`.
- Use `./scripts/worktree_dashboard.sh` for quick multi-slot visibility before and after tasks.
- For interactive git exploration, suggest `lazygit` (press `w` for worktree panel). Prefer lazygit over opening additional IDEs for git history/diff review.
