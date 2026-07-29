# pr-prep — astronomical-home bindings

Companion to the global `pr-prep` skill. When `/pr-prep` runs in this repo, apply
these concrete bindings on top of the generic phases.

## Where things live

- **Plans** — `doc/Feature_Plans/*.md`. Most plans sequence their PRs (PR-1,
  PR-2, …); "pull a PR off the plan" means one of those numbered slices. Cross-
  reference the driving memory (`MEMORY.md` → Active Work / topic files) for the
  latest status, which often supersedes the plan doc.
- **In-flight work** — read the active-work ledger at
  `C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\active_work_ledger.md`
  during Phase 3's "interaction with in-flight work" lens, so the PR you're
  prepping doesn't collide with a concurrent slot.
- **Design philosophy** — root `CLAUDE.md`. Two sections are load-bearing during
  triage:
  - *Dependency & wiring philosophy* — drives the **seams & wiring** lens. New
    per-ship deps enter through `Initialize(...)`, never ad-hoc setters; config
    lives at the level that uses it; don't thread world/session state through
    per-ship wiring. A decision that would touch bootstrap + a service interface
    + Commander/UnitService + the consuming component at once is a **fork (③)**,
    not a no-brainer — surface it.
  - *Root-cause discipline* — when a plan's PR patches a symptom, one of your
    forks is often "narrow fix vs structural fix that kills the class." Raise it.

## Where the frozen brief goes (Phase 7)

Append the decision brief to the PR's section in its `doc/Feature_Plans/*.md`
plan doc (or the driving topic memory if the plan doc is historical). Capture any
structural insight there too, per root `CLAUDE.md`. Keep it to the locked
decisions + one-line rationale for each fork — detail, not narration.

Write it in a pool slot and **land it before the build starts** (`AGENTS.md` →
Doc lifecycle → Landing). A brief scoped to the one PR being prepped may instead
ride that PR's first commit; a doc that grew into an arc governing several PRs
lands on its own, because tying it to any one slice makes the authority for the
rest hostage to that slice.

## Hand-off to implementation

The frozen brief feeds directly into the repo's default execution path: the
**agent-worktree-pr-loop** skill. pr-prep *is* a deepened version of that loop's
Step 1 ("Scope first"). Once the brief is locked and the user has confirmed
scope, proceed into the worktree loop — acquire an `agent-N` slot, build there
(optionally via the fresh implementing subagent, handed the plan + brief), and
open the PR. Don't start editing in the primary worktree.

## Test-strategy lens (Phase 3)

Unity project: PlayMode/EditMode split matters. When triaging the **test
strategy** decision, note whether the change needs graphics (→ `RequiresGraphics`
quarantine, in-editor only) or runs headless, and prefer scoped test runs
(`-ScopeType Auto`) for iteration — the merge gate re-tests the landing tree
regardless.
