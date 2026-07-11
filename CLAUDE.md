# CLAUDE.md

See also `AGENTS.md` for Obsidian/design-doc conventions and test-artifact standards.

## Root-cause discipline

When addressing feedback or fixing a bug, **always chase the root cause — do not
stop at the surface symptom.** Follow the failure all the way to its source, and
then one step further: ask whether the bug reveals something wrong with the
*underlying organization* of the code — a leaky abstraction, a fragile seam, a
missing invariant, state reaching code the wrong way. If it does, propose a
wider-scoped fix that makes the whole *class* of bug impossible (or at least
loud) going forward, not just the single instance in front of you.

It's not about winning the one battle — patch the symptom and move on — it's
about winning the next one, and the one after that, and the war overall. When a
narrow fix and a structural fix diverge, surface both to the user with the
trade-off rather than silently taking the quick patch. Capture any structural
insight in the relevant plan doc or memory so the lesson outlives the fix.

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
   the code with no merge instruction implied elsewhere — squash-merge the PR
   through the gate: `./scripts/agent_worktree_pool.sh merge <slot>`. Never
   raw `gh pr merge`: the pool command re-tests against current main when
   main moved after the branch's last test run (a stale-based branch can be
   green on its own base yet break main with zero textual conflict). Never
   merge without that explicit signal, and never force-push or skip the
   gate's test run to get there. Before
   merging, sweep unresolved PR comments: fix trivial ones directly and
   re-push; for involved ones, flag them to the user with a proposed fix
   rather than merging over them silently. As part of that same pre-merge
   sweep, run a `/simplify` pass on the diff (quality-only cleanup, not a bug
   hunt) alongside the comment-hygiene strip, and fold its fixes in before
   merging. **For every one of these pre-merge passes (simplify,
   comment-hygiene strip, unresolved-comment fixes), report back a short
   summary of what you changed and why** — unless you already discussed that
   specific change with the user. They should never find edits landing on the
   PR just before merge that they can't account for.
6. **Finalize.** After merge, run
   `./scripts/agent_worktree_pool.sh finalize <slot> origin/main` to reset the
   slot to main and release the lock, then pull `origin/main` into the local
   primary worktree (`git checkout main && git pull`) so local main matches.

This applies by default — the user doesn't need to say "use the worktree
pool" or name a slot for it to kick in. Exceptions: trivial doc/comment-only
edits the user explicitly asks to be made directly, or explicit instruction
to work in place instead.

## Comments & self-documenting code

**Comments are strongly discouraged. Make the code self-documenting instead —
always.** A comment is a last resort, justified only when the code genuinely
cannot carry the meaning on its own. Before writing one, remove the need for it:
a clearer name, a named local or constant instead of a magic value, a small
well-named helper, a simpler structure. Write a comment only when it is
**absolutely necessary** to understand the code — and then keep it to one tight
line.

The only legitimate (rare) case is a non-obvious ***why*** the code itself can't
express: a workaround for an external/engine bug, a deliberately chosen
invariant, a subtle ordering or timing requirement, a unit/coordinate-frame
gotcha. Never comment the ***what*** the code already states plainly. Delete
narration, section-banner comments, restated method signatures,
`// TODO`-as-comment, and commented-out code.

**Active cleanup campaign:** the codebase has accumulated excessive comments.
Until that is tamed, **every PR going forward includes a pass that deletes
redundant/obvious comments in the code your diff touches** (scope it to the
files you're already changing — not a repo-wide sweep). See memory
[[feedback-comments-self-documenting]].

### Comment hygiene across the PR lifecycle

While a PR is in flight, comments that explain *what changed and why* — the bug
that was fixed, the reasoning behind a change, before/after context — are
encouraged; they help review. But those are review-time scaffolding, not
permanent documentation. As soon as the PR is approved to merge (and before you
squash-merge), strip that changelog-style narration from the code, leaving only
brief, concise comments that describe the *current* implementation. A reader of
`main` should never see comments framed around a past state ("was 10f, now uses
projectile speed", "fixed the leak by…") — only what the code does now.

## Cross-agent work ledger

To keep parallel work visible — both concurrent worktree slots and successive
sessions — maintain a shared, live ledger of in-flight tasks at this fixed
**absolute** path:

`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\active_work_ledger.md`

It lives in the primary session's auto-memory, so the primary session loads it
automatically via `MEMORY.md`. But that memory dir is keyed by working-tree
path: an agent running in an `agent-N` worktree gets a *different* memory dir
and will **not** auto-load it. Worktree agents must therefore read and write
**this exact absolute path**, not their own memory dir.

**Read the ledger:**
- At the start of any coding session, before scoping new work — so you know
  what's in-progress, in-review, blocked, or parked, and don't collide with it.
- Again immediately before acquiring a worktree slot.

**Write to the ledger (re-read it right before each edit so concurrent edits
merge cleanly):**
- **Before acquiring a slot / starting a task:** add a row (or flip an existing
  one) to `🟡 in-progress` with the slot·branch and a link to the plan file
  (`doc/Feature_Plans/*.md`) or the driving memory.
- **On PR open:** status `🔵 in-review`, record the PR number.
- **On block or park:** `⛔ blocked` / `🅿️ parked` with a one-line reason.
- **On merge + local-main sync:** status `✅ merged`, then delete the row (or
  move it to the ledger's Archive) once local `main` reflects the merge.

The ledger is *live state*, not project history — durable narrative still lives
in the `Active Work` section of `MEMORY.md`. When they disagree about what is
happening right now, the ledger wins. Keep rows short and self-contained; one
row per task.

## Deferrals & the project board

When the user says to **defer / punt / park** something, record it on the
Obsidian **project board** — it is a first-class, agent-writable tracking
artifact, not a read-only reference:

`D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md`

- Write the board item as an **extremely short kanban card — a title only, no
  description.** A few words the user can scan at a glance (e.g. `- [ ] PlayerRig
  decomposition`), with the right `#Tags` and a link to where the detail lives.
  **Never** write a sentence, rationale, or file-level detail onto the card
  itself — if you're tempted to add "— because…" or a clause explaining it, that
  text belongs in the linked memory/plan doc, not the card. Match the Kanban
  markdown (`- [ ]` under a `## Column`, tab-indented sub-items) in the
  appropriate column — a Dev Pool, `To Do`, or nested under its parent item —
  and leave unrelated items and the `%% kanban:settings %%` block untouched.
- Put **all context** — rationale, trade-offs, file-level detail, what was
  tried — in the linked agent memory or plan doc, never on the card. **Link the
  two**: the card carries a link to the memory file / plan doc, and the memory
  file names the board item.

Three tracking surfaces, don't conflate them:
- **Active-work ledger** (memory) — live, right-now claims/locks; per-worktree.
- **Project board** (Obsidian) — backlog, deferrals, and status across the
  project; title-only cards, detail linked out.
- **Agent memory** — the durable *why/how* behind board items and decisions.

Full board conventions: `AGENTS.md` → "Design docs & work tracking".

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
