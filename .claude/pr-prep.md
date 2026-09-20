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

- **Plans** — the arc's GitHub issue (label `arc`) is the global skill's "plan
  doc": its body is the brief and sequences the arc's slices (sub-issues, or a
  list in the body). "Pull a PR off the plan" means one of those slices. Slices
  carry both a descriptive name (`vocab-docfix`) and a positional label
  (`Slice-C`, `PR-4`) — see `doc/Glossary.md` → *arc & PR naming*; add the
  missing label when you prep a slice that lacks one. The issue and its
  comments are the authority for status; `MEMORY.md` → Active arcs holds only
  the links to find it. Read the issue by section, and ask the `design-lookup`
  agent for design history (`doc/agents/design-docs.md` → Reading design).
- **In-flight work** — read the active-work ledger at
  `C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\active_work_ledger.md`
  during Phase 3's "interaction with in-flight work" lens, so the PR you're
  prepping doesn't collide with a concurrent slot.
- **Design philosophy** — root `AGENTS.md`. Two sections are load-bearing during
  triage:
  - *Dependency & wiring philosophy* — drives the **seams & wiring** lens. New
    per-ship deps enter through `Initialize(...)`, never ad-hoc setters; config
    lives at the level that uses it; don't thread world/session state through
    per-ship wiring. A decision that would touch bootstrap + a service interface
    + Commander/UnitService + the consuming component at once is a **fork (③)**,
    not a no-brainer — surface it.
  - *Fix ladder* — when a plan's PR patches a symptom, one of your
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
  `AGENTS.md` → Vocabulary. The brief lists them; the `doc/Glossary.md` edit
  rides the implementing PR (worktree-loop skill → Step 4, the `Vocab:` line).
- **Naming lens (Phase 3)** — a name that collides with `doc/Glossary.md`'s
  collision table is a fork, not bikeshed: pick the qualifier deliberately or
  pick a different word.

## Where the frozen brief goes (Phase 6)

Overrides the global skill's plan-doc default: the brief goes on the tracker,
never under `doc/` or into memory (`doc/agents/design-docs.md` → Where design
lives).

- **New arc** — the brief is the arc issue's body; mint the issue per
  `doc/agents/issue-tracker.md` (Body law, Labels, Projects board sync).
- **Slice of an existing arc** — a comment on the slice's sub-issue when it
  has one, otherwise on the arc issue.

Section it by question — one `##` per fork or ruling — holding the locked
decision plus a one-line rationale: detail, not narration. Post it before the
first implementing slot is acquired.

## Hand-off to implementation

The frozen brief feeds directly into the repo's default execution path: the
**agent-worktree-pr-loop** skill. pr-prep *is* a deepened version of that loop's
Step 1 ("Scope"). Once the brief is locked and the user has confirmed
scope, proceed into the worktree loop — acquire an `agent-N` slot, build there
(optionally via the fresh implementing subagent, handed the issue carrying the
brief), and open the PR. Don't start editing in the primary worktree.

## Chat title lifecycle

The worktree-loop skill's *Chat title lifecycle* section is the authority for
the title grammar and the self-retitle call (`set_session_title`,
`session_id: "self"`). Prep adds two transitions that write no ledger row —
retitle yourself at them too:

- prep starts: `prep | <Slice-X or PR-N> | <word-id>` — a broken-out prep chat
  is born with this title; one launched freeform retitles itself here.
- brief frozen, build not started: append ` — brief frozen`.

A chat that stays open tracking the whole arc takes the skill's `Arc` form and
refreshes it whenever the tracked state moves.

## Test-strategy lens (Phase 3)

When triaging the **test strategy** decision, name the level (EditMode vs
PlayMode) and whether the proving test needs a graphics device. A
`RequiresGraphics` test is excluded from every merge-gate run, so it never
counts toward merge proof; it runs only as a filtered `-WithGraphics` batch run
or in an interactive editor (`doc/Diagnosis_Loop_Cookbook.md` → Flaky /
full-run-only failures). Iterate with scoped runs (`-ScopeType Auto`); they
record no merge proof — the merge gate wants a full-suite pass on the exact
landing tree (worktree-loop skill → Step 6).
