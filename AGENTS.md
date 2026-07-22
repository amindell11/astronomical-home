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
- **The board is a first-class, agent-writable tracking artifact — actively
  maintain it, don't merely suggest.** Add items, move them between columns,
  tick sub-items, and tag them as work progresses. Match the Kanban markdown
  exactly: `- [ ]` items under a `## Column` header, tab-indented sub-items,
  `#Tags`. Leave unrelated items and the trailing `%% kanban:settings %%`
  block untouched — write your item, don't reorganize the file.
- **Deferrals live on the board.** When the user says to defer / punt / park
  something, capture it as a board item in the appropriate column (a Dev Pool,
  `To Do`, or nested under the parent item it relates to) with the right
  `#Tags`. The board is that deferred work's canonical home.
- **Board cards are title-only — a few scannable words, no description.**
  `- [ ] PlayerRig decomposition`, not a sentence. Never write a rationale,
  clause, or file-level detail onto the card; if you're tempted to append
  "— because…", that text belongs in the linked memory/plan doc. Put **all**
  deep rationale, trade-offs, and file-level detail in agent memory
  (`.claude/.../memory/`) and link the two: the card carries a link to the
  memory/plan-doc, and the memory file names the board item. The board says
  *what / for-when*; memory says *why / how*. (Live in-flight claims are a third
  thing — those go in the active-work ledger, see `CLAUDE.md`.)

### Doc lifecycle

`doc/Feature_Plans/` holds two kinds of docs. **Transient briefs** (the
default): decision briefs and plans for a single arc; the PR that completes
the arc DELETES its brief — git history is the archive, the PR body carries
the build story, the memory topic file the arc narrative. **Living docs**
(the exception): roadmaps and standing design references; each carries,
directly under its title, a line `> STATUS: living — <one-line why this
outlives its arc>` (live-arc briefs carry `> STATUS: live arc — <what's in
flight>`). A doc with no STATUS line is a transient brief by definition.
Shelved arcs: the brief travels on the preserved branch, not main. Session
handoffs are memory material (the memory directory), never repo docs; the
consuming session deletes them. Header ratchet: an agent absorbing a doc
whose STATUS contradicts reality fixes the header (header only) in its PR.

## Agent memory

This repo is backed by a persistent, file-based agent memory — durable facts,
decisions, and the *why/how* behind board items. It lives **outside** the repo,
in the primary session's memory directory:

`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\`

- **`MEMORY.md`** in that directory is the index: one line per memory, loaded
  into the primary session's context automatically at session start. Each fact
  is its own `.md` file (with frontmatter) linked from the index; add a fact by
  writing the file and appending a one-line pointer to `MEMORY.md`. Prefer
  updating an existing file over creating a near-duplicate.
- **The directory is keyed to the working-tree path** (`D--amind-git-astronomical-home`).
  An agent running in an `agent-N` worktree resolves a *different* memory dir
  and will **not** auto-load the primary session's memory. Such agents must read
  and write the **absolute path above**, not their own memory dir. This is the
  same reason the active-work ledger
  (`…/memory/active_work_ledger.md`) is always referenced by absolute path — see
  `CLAUDE.md` → "Cross-agent work ledger".
- **Three tracking surfaces, don't conflate them:** agent memory holds the
  durable *why/how*; the Obsidian **project board** holds title-only backlog /
  status cards; the **active-work ledger** holds live, right-now claims/locks.
  Board cards and ledger rows link out to the memory file that carries their
  detail.

## Working style

- Flag unknowns and ambiguities explicitly on every task.
- Be colloquial and collaborative: talk through what we're doing together
  clearly, without becoming stiff or overly formal.

## Test artifacts & conventions

- Standardize Unity test artifacts to `results/unity-tests-agent` (pass an
  explicit `outDir`/`-OutDir`).
- Unity access is coordinated per project through `scripts/unity_access.ps1`:
  runs on different worktree projects overlap, and only Unity startup
  serializes through a machine-wide boot lane. Prefer batch tests; they drive
  the whole protocol automatically. Use a tracked interactive editor only when
  batch mode cannot verify the behavior, and close/release it immediately
  afterward. An untracked main-worktree editor is user-owned: ask the user to
  close it, never terminate it. Inspect owners, the boot lane, and the FIFO
  queue with `./scripts/unity_access.ps1 -Action Status`.
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
- Offline AI-behavior analysis scripts live in `scripts/ai-analysis/`
  (`analyze_utility.py`, `find_patterns.py`) — they read the JSONL that
  `UtilityLogger` (`Assets/Scripts/AI/Editor/UtilityLogger.Editor.cs`) writes.
