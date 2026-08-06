# CLAUDE.md

See `AGENTS.md` for Unity code conventions (lookups, null checks, folder taxonomy),
Obsidian/design-doc conventions, and test-artifact standards — read its
`## Unity code conventions` before writing Unity code.
Rules here are the always-loaded universal core; workflow detail lives in `.claude/skills/`. Add a rule here only after a repeated observed failure — the same evidence bar code scaffolding must meet.

## Fix ladder

Chase every failure to its root cause before writing anything. Then classify it:

**Operating error** — bad input at an untrusted boundary (user, file, network, serialized/inspector data): parse, don't validate. Check once at the boundary, convert to a trusted type, proceed on trust inside. Never a fallback default.

**Programmer error** — our own code or wiring violates an invariant: climb this ladder. Enter it only for an observed failure or an explicit user pull. A speculative finding (review comment about a hypothetical, a "could happen") gets a written reply, not code; a real finding outside the current change's scope gets a deferral (board card + reply), not folded in.

1. **Unrepresentable.** Restructure so the mistake cannot compile or cannot be authored (types, Initialize-injection, sealed construction). Take this rung when the encoding is natural; don't contort the design to reach it.
2. **Earliest deterministic failure.** Constructor/Initialize throw, `OnValidate`, bootstrap validation — this is the top rung that *exists* for anything the compiler can't see (serialized fields, scene data).
3. **Cost gate.** If rungs 1–2 exceed the current change's scope, stop — present narrow vs structural with the trade-off and let the user pick fix-now or defer. Never downgrade silently.
4. **Loud failure at use.** A throw that halts and names the violated invariant. Log-and-continue is not loud — it's a guard wearing a costume.
5. **Guards** — a check that absorbs a bad state and keeps running: prohibited for programmer errors.

One rung per fix. A structural fix does not also get guards or hypothetical-edge tests; if you believe a fix needs two rungs, that's a budget question — ask.

When proposing any fix, name the root cause and the rung you chose; when narrow and structural diverge, present both.

## Scope conservation

The scoped statement bounds the diff, not just the intent. Prefer the smallest diff that satisfies it.
Before submit, re-read the scope against the diff: anything a reader of the scope wouldn't expect either comes out or gets flagged for confirmation at the same bar as the original scoping.
No features beyond what was asked. No abstractions for single-use code. No error handling for scenarios that cannot occur. Touch only what you must.
If the diff grows to a multiple of what the scope implies, stop and reclassify before pushing.

## Comments

Code is self-documenting; a comment is a last resort for a non-obvious *why* the code cannot express.
One line means ≤ ~15 words. No `<summary>` on self-naming members. Never narrate *what*, never past-state framing, never commented-out code.
Ratchet: apply the standing rule to the hunks you touch. Whole-file sweeps happen only in dedicated hygiene PRs — never fold them into feature PRs.
Review/build narration belongs in the PR description, not the code.

## Vocabulary

`doc/Glossary.md` is the authority for this project's coined terms; coining or shifting one updates it in the same PR.
Always qualify a collision-table word ("eval gate", not "gate") unless that word's glossary row grants a bare reading in the context you're writing in. In a title, qualify regardless.
Vocab ratchet: fix deprecated forms in the hunks you touch; whole-file sweeps only in hygiene PRs.
One home per term: the glossary carries only what the code cannot say — a constraint, a decision, a gotcha. Where code answers "what is this?", the entry points at the symbol instead of restating it.
**Definition at first use.** Before deploying a new term anywhere — brief, design discussion, one-off fix — define it inline in the simplest concise form, using existing terms and general concepts. Define downward: never define a new term by way of another new term.
**Re-orientation.** Recast cosmetic drift silently. State the reading you took for any collision-table word, even when the parse feels certain. Ask when genuinely ambiguous, or when the divergence would change what you do next. Always frame it as your interpretation, never as the other person's error; max one explicit flag per message. Repair symmetric misreads before proceeding. After roughly three recasts of the same form, propose making that form canonical — once.

## Design & agent-doc ratchets

The `codebase-design` user skill's vocabulary — module, interface, seam, adapter, depth — is canonical for design discussion; `doc/Glossary.md` rows point at it. In hunks you touch, name seams and interfaces with this vocabulary. A shallow pass-through or single-adapter seam met in touched code gets a board card per deferral conventions — never an in-place restructure inside a feature PR; deepening happens in dedicated PRs.
When editing agent-consumed docs (CLAUDE.md, AGENTS.md, skills, memory index), apply the `writing-for-agents` user skill to the sections touched: hunt no-ops, prune sediment, sharpen pointers. Whole-doc sweeps only in dedicated hygiene PRs.

## Default workflow

`.claude/skills/agent-worktree-pr-loop/SKILL.md` is the single authority for the coding-task loop. Invariants:
- Scope is confirmed with the user before building.
- Build and test in a pooled worktree, never the primary tree.
- Design docs land on main before the work they govern builds (`AGENTS.md` → Doc lifecycle).
- PR when green.
- Merge ONLY via `./scripts/agent_worktree_pool.sh merge <slot>`, and only on an explicit user merge instruction (definition in the skill). Sole exception: user-approved docs-only changes may commit directly to main (skill → "Docs-only landing").
- Finalize the slot after merge.
- Chat titles follow the lifecycle grammar (skill → "Chat title lifecycle"): retitle via the Title concierge at every ledger-writing transition; a plain title marks a discussion chat.

## Cross-agent work ledger

`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\active_work_ledger.md` — worktree agents must use this exact absolute path.
Read it at session start and before acquiring a slot; write on claim, PR-open, block, and merge.
Rows are one line, claims only — merged rows are deleted; their story lives in the PR description and memory topic files.

## Deferrals & project board

Deferrals go on the Obsidian board `D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md` as title-only cards linking to a memory/plan doc.
All context lives in the linked doc, never on the card. Conventions: `AGENTS.md` → "Design docs & work tracking".

## Dependency & wiring philosophy

Follow these when adding any new dependency; prefer zero new wiring over new seams:

1. **Per-ship dependencies enter a component exactly once, through `Initialize(...)` parameters** (see `Scout.Initialize`, `Navigator.Initialize`) — never ad-hoc `Set<Thing>()` setters per feature; a new per-ship dependency extends the Initialize signature.
2. **Keep knowledge at its abstraction level.** A composer (Scout, AICommander) only instantiates and sequences its parts; domain math and configuration (scan envelopes, cost shapes, query extents) live inside the part that uses them. A field that exists only to be forwarded downward belongs downward.
3. **Do not thread world/session-scoped state through per-ship wiring** (Commander/UnitService pass-throughs, service-interface additions) just to hand it to a component. How world-scoped state should reach consumers is deliberately open — multiple arena instances per process are planned (RL training), so process-wide statics are equally suspect. Until that design lands: keep any such seam as narrow as possible (`ArenaContext.ObstacleField` is the shape), mark it interim, don't copy it to new systems, and raise the question with the user rather than inventing a pattern.
4. **The smell to catch mid-diff:** if wiring ONE new dependency touches bootstrap + a service interface + Commander/UnitService + the consuming component, stop and reclassify before pushing.
5. **Refs bind and observe; signals cause.** A serialized/held reference exists to bind (Initialize-style identity/config injection) or observe (poll state, read a target) — never so one peer can command another at runtime. Runtime causation between peers rides an event / sector-bus token the actee subscribes to; command calls are legitimate only downward (owner→owned, caller→service) or during setup/teardown orchestration. The smell: a ref whose only use is telling its target to *do* something.
6. **A new caller of a shared resource goes through that resource's coordinator, not around it.** When the coordinator's primitive doesn't fit the new caller, generalize the primitive (pass the specifics in) — do not build a parallel path beside it. Absorbing a second caller into the existing seam is consolidation, not the speculative abstraction scope-conservation cautions against; an approach that keeps knowledge of the shared resource's protocol out of the caller is the win, even when it means touching the shared tool. (Infra-layer sibling of #2, learned when RL drivers `Popen`ed Unity outside the access coordinator and orphaned owner leases.)
   - **Corollary — outputs that become inputs.** When one tool's output becomes another tool's input (a file a launcher reads back, a path or format a second process depends on), that location/format is a contract the *producer* owns. A consumer that reconstructs where or how the producer wrote — re-deriving a path, re-parsing a layout it didn't author — is building a parallel path beside the owner. Hand the fact down the shared seam (tell the producer the path, or have it emit one) instead of reconstructing it downstream. (Learned when a training-gate design nearly had a Python launcher reconstruct the player's `persistentDataPath` rather than pass the output dir in.)

## Session hygiene

Approvals are per-action and never stretch into standing authorization — re-ask at each consequential step (merge, long-running or expensive launches).
Past heavy context (~300k tokens), do not merge: write the handoff and let a fresh session drive the merge.
Stopping a background monitor orphans its tail.exe/grep.exe children on Windows, and they keep tailed files locked (WinError 32 on delete/rename). taskkill the orphans before relaunching anything that recreates those logs.
`core.hooksPath` belongs to `scripts/install_hooks.sh` (→ `.githooks`) — never override it, and never reach for `-c core.hooksPath=/dev/null` to skip a hook. git-lfs installs its hooks into whatever that path names, so off Git Bash the value resolves repo-relative and materializes a junk `dev/null/` directory in the tree.
