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
- **Issue tracker:** GitHub Issues on this repo — the source of truth for
  backlog, bugs, and deferrals. `doc/agents/issue-tracker.md` is the binding
  the tracker-shaped skills (wayfinder, to-tickets) consult: label
  vocabulary (`pri:now/next/later`, triage states, `wayfinder:*`), body law,
  dependency/frontier mechanics, Projects-board sync.

How to use them to track work:
- When starting a task, check whether it maps to an issue
  (`gh issue list --label bug`, `--label pri:now`, …). Ground the task in the
  issue's wording and labels rather than inventing scope.
- For design/doc requests, research the vault directly (don't speculate) and
  cite note/file paths for non-obvious claims. Respect Obsidian conventions
  (wikilinks, embeds, aliases, anchors, frontmatter).
- **The tracker is a first-class, agent-writable artifact — actively
  maintain it, don't merely suggest.** Create, label, close, and comment via
  `gh` as work progresses; prefer native sub-issues and blocked-by
  dependencies over body-text conventions (mechanics in the tracker doc).
- **Deferrals live on the tracker.** When the user says to defer / punt /
  park something, capture it as an issue: `needs-triage` + best-guess
  priority label. An issue is that deferred work's canonical home.
- **Issue bodies carry no deep rationale — the repo is public.** Rationale,
  trade-offs, and file-level detail go in agent memory
  (`.claude/.../memory/`) or the plan doc, linked from the body; the memory
  file names the issue number. The tracker says *what / for-when*; memory
  says *why / how*; live in-flight claims go in the active-work ledger (see
  `CLAUDE.md`). The tracker doc's **body law** defines the legal body
  shapes — thin deferral, to-tickets slice (behavioural spec + acceptance
  criteria; the tracker owns an arc's slice breakdown, plan docs point at
  it), wayfinder map/ticket.

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

**Landing.** A doc lands on main *before* the work it governs builds. An arc
doc governing several PRs lands as its own docs-only PR, merged before the
first implementing slot is acquired; a brief scoped to exactly one PR may ride
that PR's first commit. Never author either in the primary worktree — a doc
written there is invisible to the branch that needs it, and the copy that
reaches a slot is an untracked twin with no merge base.

## Agent memory

This repo is backed by a persistent, file-based agent memory — durable facts,
decisions, and the *why/how* behind tracker issues. It lives **outside** the repo,
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
  durable *why/how*; **GitHub Issues** hold title-plus-link backlog /
  status items; the **active-work ledger** holds live, right-now claims/locks.
  Issues and ledger rows link out to the memory file that carries their
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

## Unity code conventions

- **Expensive lookups in `Awake()` only** — `GetComponent*`, `GameObject.Find*`,
  `Camera.main` never run in `Start`/`OnEnable`/`Initialize`/update loops or any
  runtime path. Cache in `Awake`; `Initialize(...)` only assigns injected
  references (see `CLAUDE.md` → dependency wiring).
- **Unity null checks use the engine's lifetime-aware operators** — `if (obj)`,
  `obj != null`, `?.`/`??` on `UnityEngine.Object` types; never `is null` /
  `is not null` on them (bypasses the destroyed-object check). Plain C# types
  may use `is null` freely.
- **Prefer early returns** (inverted ifs) over nested blocks.
- `[SerializeField]` tooltips are documentation for the inspector, not code
  comments — the comments policy in `CLAUDE.md` does not apply to them.
- **Folder taxonomy carries the object relationships** — leaf folders of
  roughly 2–8 files grouped by domain (the `Combat/Projectiles/Audio` grain),
  never by tier or catch-all ("Agent", "Core", "Misc"). A PR adding a file to
  a package root or a 10+-file folder either names the domain subfolder or
  creates it.
- **One primary type per file, file named for that type.** Small satellite
  types (a row struct, an enum, a summary) may ride with their owner; a file
  whose name matches none of its types is the smell.
- **Structure ratchet:** apply to files and folders you touch; whole-package
  re-taxonomies are dedicated hygiene PRs, never folded into feature PRs.

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
