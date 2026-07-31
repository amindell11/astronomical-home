# pr-prep — astronomical-home bindings

Companion to the global `pr-prep` skill. When `/pr-prep` runs in this repo, apply
these concrete bindings on top of the generic phases.

## Open with the goal, in plain language

(Also in the global skill; repeated here so it's versioned with the repo.)
Your first message to the user — before any design discussion — states the
high-level goal of the PR in the simplest terms possible, assuming **no prior
knowledge of the plan**: what problem it solves and what is different once it
lands. Define every key term inline at first use, in the simplest concise form.
One paragraph, two at most. Conciseness and clarity are the bar — this is
orientation, not a plan summary.

## Where things live

- **Plans** — `doc/Feature_Plans/*.md`. A plan sequences its arc's slices;
  "pull a PR off the plan" means one of those slices. Slices carry both a
  descriptive name (`vocab-docfix`) and a positional label (`Slice-C`, `PR-4`)
  — see `doc/Glossary.md` → *arc & PR naming*; older plans may have only one
  form, so read the plan's own scheme, and add the missing label when you prep
  a slice that lacks one. Cross-reference
  the driving memory (`MEMORY.md` → Active Work / topic files) for the latest
  status, which often supersedes the plan doc.
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

## Vocabulary — a first-class brief section

Treat vocabulary as a section of the brief alongside forks, assumptions, and
blindsiders, not as a stylistic afterthought. A PR description reaching the user
after the fact is **not** enough: the point is to refresh the user's mental
schema *before* the design discussion leans on a term.

- **Terms this design leans on** — presented with the Phase 4 design map: a
  one-line refresher for each non-obvious existing term the forks are about to
  use. Pull the wording from `doc/Glossary.md`; if a term the design needs isn't
  there, that absence is itself worth saying.
- **New terms** — every term this design coins, defined at first use per root
  `CLAUDE.md` (simplest concise form, existing terms only, define downward).
  Registered into `doc/Glossary.md` when the brief freezes at Phase 6.
- **Naming lens (Phase 3)** — a name that collides with `doc/Glossary.md`'s
  collision table is a fork, not bikeshed: pick the qualifier deliberately or
  pick a different word.

## Where the frozen brief goes (Phase 7)

Append the decision brief to the PR's section in its `doc/Feature_Plans/*.md`
plan doc (or the driving topic memory if the plan doc is historical). Capture any
structural insight there too, per root `CLAUDE.md`. Keep it to the locked
decisions + one-line rationale for each fork — detail, not narration.

**Land it before the build starts** (`AGENTS.md` → Doc lifecycle → Landing).
Since the brief was user-approved in the prep session, it qualifies for the
worktree-loop skill's docs-only direct-to-main landing — no PR needed. A brief
scoped to the one PR being prepped may instead ride that PR's first commit; a
doc that grew into an arc governing several PRs lands on its own either way,
because tying it to any one slice makes the authority for the rest hostage to
that slice.

## Hand-off to implementation

The frozen brief feeds directly into the repo's default execution path: the
**agent-worktree-pr-loop** skill. pr-prep *is* a deepened version of that loop's
Step 1 ("Scope first"). Once the brief is locked and the user has confirmed
scope, proceed into the worktree loop — acquire an `agent-N` slot, build there
(optionally via the fresh implementing subagent, handed the plan + brief), and
open the PR. Don't start editing in the primary worktree.

## Chat title lifecycle

The worktree-loop skill's *Chat title lifecycle* section is the authority
(title grammar + Title-concierge retitle protocol). Prep-session stages:

- prepping: `prep | <Slice-X or PR-N> | <word-id>` — a broken-out prep chat is
  born with this title (whoever spawns it titles it so); if launched freeform,
  request the retitle as soon as prep starts.
- brief frozen, build not started: append ` — brief frozen`.
- the chat stays open tracking the whole arc: retitle to
  `<stage> | Arc | <arc-name> — <happening now> → next <next step>` and
  refresh it whenever the tracked state moves.

## Test-strategy lens (Phase 3)

Unity project: PlayMode/EditMode split matters. When triaging the **test
strategy** decision, note whether the change needs graphics (→ `RequiresGraphics`
quarantine, in-editor only) or runs headless, and prefer scoped test runs
(`-ScopeType Auto`) for iteration — the merge gate re-tests the landing tree
regardless.
