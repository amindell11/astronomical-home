# AGENTS.md

Cross-tool agent guidance for this repo. `CLAUDE.md` points here; keep shared
conventions in this one file so every tool (Claude Code, Cursor, …) reads a
single source of truth.

## Design docs & work tracking

This game is designed in an Obsidian vault **outside** the repo. Treat these
two notes as primary context — for design/doc work, and for knowing what to
build next:

- **Design overview:** `D:/amind/Documents/Obsidian Vault/Astronomical/OVERVIEW.md`
  — the game's vision and core pillars (skill-ceiling combat, loot/rewards,
  LLM-driven AI dialogue; 2.5D top-down rogue-like). Read it, and the notes it
  wikilinks (`[[Sectors]]`, `[[Combat]]`, `[[World]]`, …), before proposing or
  implementing design-facing changes. If it conflicts with an assumption,
  follow the doc and call out the conflict.
- **Project board:** `D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md`
  — an Obsidian Kanban board and the source of truth for work status. Columns:
  `To Do`, `BUGS`, `Doing`, `Mid Dev Pool`, `High Dev Pool`, `Feature Goals`,
  `Done`, `Meh`, `Archive`. Items carry `#Tags` (e.g. `#AI`, `#Ship`,
  `#Testing`, `#Architecture`) and can nest sub-items.

How to use them to track work:
- When starting a task, check whether it maps to a board item. Pull bug work
  from `## BUGS`, active features from `## Doing`, queued work from `## To Do`.
  Ground the task in the board's wording and tags rather than inventing scope.
- For design/doc requests, research the vault directly (don't speculate) and
  cite note/file paths for non-obvious claims. Respect Obsidian conventions
  (wikilinks, embeds, aliases, anchors, frontmatter).
- The board is the user's — they edit it in Obsidian. Don't rewrite it
  unprompted; reference items and *suggest* status moves ("this maps to the
  `#AI` wingman item under Doing") rather than silently editing the file.

## Working style

- Flag unknowns and ambiguities explicitly on every task.
- Be colloquial and collaborative: talk through what we're doing together
  clearly, without becoming stiff or overly formal.

## Test artifacts & conventions

- Standardize Unity test artifacts to `results/unity-tests-agent` (pass an
  explicit `outDir`/`-OutDir`).
- For PlayMode tests, prefer inheriting from
  `Tests.PlayMode.Common.PlayModeWorldFixture` when it makes sense (ensures
  GamePlane/test-arena setup and cleanup).
- See `TESTING.md` for the test suite guide. Every fixture is tagged with one
  **domain** category (`Sectors`, `Weapons`, `MPC`, …) plus optional
  `Smoke`/`Slow`; run a feature slice with `-TestCategory <Domain>` instead of
  the whole suite. Give new fixtures exactly one domain tag.

## Workflow

- The agent-worktree PR loop (`.claude/skills/agent-worktree-pr-loop/SKILL.md`)
  is the **default** workflow for coding tasks — see `CLAUDE.md`. It applies
  whether or not the request mentions `agent-1`/`agent-2`/`agent-3`,
  worktrees, or PRs by name.
- Use `./scripts/worktree_dashboard.sh` for quick multi-slot visibility before
  and after tasks.
- For interactive git exploration, suggest `lazygit` (press `w` for the
  worktree panel). Prefer lazygit over opening additional IDEs for git
  history/diff review.

## Agent config layout (source of truth)

Claude Code is the active agent tool for this repo.

- **Skills** live under `.claude/skills/`. That is the canonical home — do not
  duplicate a skill's body into another tool's folder; if a second tool needs
  discovery, leave a one-line pointer, not a copy.
- **`.cursor/`** holds Cursor IDE rules only and reads this file for shared
  conventions.
- Offline AI-behavior analysis scripts live in `scripts/ai-analysis/`
  (`analyze_utility.py`, `find_patterns.py`) — they read the JSONL that
  `UtilityLogger` (`Assets/Scripts/AI/Editor/UtilityLogger.Editor.cs`) writes.
