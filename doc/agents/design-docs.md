# Design docs & work tracking

> STATUS: living — branch-triggered reference for design/doc work, tracker usage, and the doc lifecycle; pointed at from `AGENTS.md`. Tracker label/body/board mechanics: `issue-tracker.md`.

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
  `gh` as work progresses.
- **Deferrals live on the tracker.** When the user says to defer / punt /
  park something, capture it as an issue — that issue is the deferred work's
  canonical home. Labels, body law (the repo is public — rationale goes in
  the linked memory/plan doc, never the body), and sub-issue/blocking
  mechanics: the tracker doc.

## Doc lifecycle

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
