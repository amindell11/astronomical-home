# Glossary

> STATUS: living — shared vocabulary; updated in the same PR that coins or shifts a term.

Coined terms accumulate faster than shared definitions do. This file is the
authority for what this project's words mean, and the place to look when a term
reads like it might mean two things.

## How to use this

**Two standing rules.**

1. **Never bare — unless the row grants it.** Every word in the collision table
   carries a qualifier by default: "merge gate", "worktree slot", "command
   churn". Some rows name exactly one context where the bare word is legal
   (bare "envelope" = firing envelope, "harness" = matrix harness, "seed" = RNG
   seed, "arc" = the work sense, "slot" = worktree slot in workflow text). The
   exception belongs to the row, not to the writer — if the row grants none,
   qualify. In a doc title or heading, always qualify regardless.
2. **Words, not tenses.** This file settles competing *words* for one thing.
   Inflections of a settled root are all canonical and need no entry of their
   own: grill / grilled / grilling / grill session / grill-settled are one term.
3. **One home per term.** An entry carries only what the code cannot tell you —
   a constraint, a decision, a gotcha, or a concept spanning several symbols.
   Where the code already answers "what is this?", point at the symbol instead
   of restating it. Two copies of one definition is the drift this file exists
   to prevent, and a symbol that needs a line of its own gets it at the symbol
   (that line is a non-obvious *why*, so the comments policy already allows it).

**Definition at first use.** Before deploying a new term anywhere — brief,
design discussion, one-off bug fix — define it inline in the simplest concise
form, using existing terms and general concepts. **Define downward:** never
define a new term by way of another new term.

**Re-orientation.** When someone's usage drifts from this file:

- Cosmetic drift (a deprecated synonym, unambiguous): recast silently — the
  canonical term simply appears in the reply.
- Any collision-table word: state the reading you took, even when the parse
  feels certain — "(taking 'gate' as the merge gate here)".
- Genuine ambiguity, or a divergence that would change what happens next:
  **ask** rather than assume — "which lane, the boot lane or the harness
  lane?"
- Always phrased as your own interpretation, never as the other person's error.
  Max one explicit flag per message: flag the load-bearing drift, recast the rest.
- Repair symmetric misreads — if a reply presupposes a misread of a term *you*
  used, fix that before proceeding.
- After roughly three recasts of the same form, propose making that form
  canonical, once. The vocabulary serves its speakers, not the reverse.

**Keeping it current.** Coining or shifting a term updates this file in the same
PR. Deprecated forms get fixed in hunks you already touch (the ratchet);
whole-file sweeps belong in dedicated hygiene PRs.

---

## 1. Collision table — never write these bare

| Word | Live senses | Rule |
|---|---|---|
| **gate** | merge gate · cost gate (fix-ladder rung 3) · go/no-go gate · anti-churn gate · scoping gate · owner's gate (a ship art stage approval) · "gated off" code conditionals | Always qualified. Bare "the gate" is legal only in pool-merge context (= merge gate), and never in a title. |
| **lane** | boot lane · remote lane (the second Unity box, `remote_lane.sh`) · harness lane · watch/capture lane · audit lane · access-queue lane · firing lane (lane clearing; a situation's allies in lane) · decision lane (nav, fire, ability, jump and recall, the lanes a `BrainDecision` carries) · LANE slot (the MPC sentence term, always caps) | Always qualified. |
| **pool** | worktree pool · ship resource pool (`PoolDifferential`) · object pool (`SimplePool`) · Dev Pool issue labels (`mid-dev-pool`/`high-dev-pool`, ex-board columns) | Always qualified. |
| **token** | bus/signal token · obs obstacle token (`ObstacleTokenCap`) · threat token · LLM context token | Always qualified. |
| **term** | intent/cost term (a weighted sentence-slot cost the MPC solves — #485) · activation term (`ActivationTerm`, the AND-ed predicate atoms of sector activation rules) | Always qualified. An intent-grammar doc may read bare "term" = intent/cost term only after declaring the carve-out (Stage A brief precedent). |
| **slot** | worktree slot (`agent-N`) · weapon slot · ordnance slot (§2 → *weapon slot*, *ordnance slot*) · obs slot-block grammar · MPC terminal-cost slot · sentence slot, instance or class (a typed intent-sentence position) · anatomy slot (§2 → *anatomy*) · keystone slot (§2 → *keystone*) · line slot (a moment's lines on the companion's line sheet) | Qualify outside pool-loop context; bare "slot" = worktree slot in workflow text only. |
| **pin** | pin test (freeze a value) · pinned seeds/hypers · ram-pin exploit | Qualify. "ram-pin" always hyphenated for the physics exploit. |
| **fixture** | NUnit test fixture · sector fixture · fixture link (§2 → *fixture link*) | Always qualified. |
| **seed** | RNG seed · `SeedScope` stream · sealed held-out seeds (1001–1020) · `SeedMode.BorderEscape` | Bare "seed" = RNG seed. |
| **archetype** | opponent archetype (`OpponentArchetype`) · chassis archetype · ship-prefab archetype | Qualify — the first two both feed the matchup matrix. |
| **adopt** | sector adoption (the placed object IS the runtime object) · coordinator Adopt (seize an orphan process) | Qualify: "sector-adopt" / "coordinator Adopt". |
| **authority** | strafe/lateral authority (physics) · design authority (which doc rules) · merge authority (who may merge) | Always qualified. |
| **churn** | command churn (facing) · anti-churn gate (diff size) · define churn (Sentis, retired) · churn-discard (bloat) · sort-churn | Always qualified. |
| **smoke** | `Smoke` NUnit category · `-ScopeType Smoke` | Qualify. Smoke is a **ScopeType, never a Mode**. |
| **floor** | characterization floor · radius floor · speed floor (missile evasion's closing-speed floor, `ArchetypeBrain.closingSpeedFloor`) | Always qualified. |
| **mirror** | mirrored second `EpisodeRunner` · yaml branch-tip mirror · name mirror (§2 → *Art pipeline*) · Blender Mirror modifier | Always qualified. |
| **lock** | geometry lock (§2 → *Art pipeline*) · pool locks (slot lock, merge-turn lock) · two-tier lock · "design locked" (pr-prep, §2 → *locked / frozen*) · missile lock (`LockOnSensor`) | Always qualified. |
| **driver** | Python drivers (`training/rl/`) · `EpisodeLoopDriver` | Qualify. "Driver:" is retired as a doc-header word. The interactive game's driver is a *host* (`GameHost`), not a driver. |
| **harness** | matrix harness (`Harness`) · determinism/sweep/ram-bench harness · test harness | Bare "harness" = matrix harness; qualify the others. |
| **arc** | multi-PR work arc · enemy arc exposure (retired with `ExposureCost`; prose only) | The work sense dominates; combat docs say "exposure arc". |
| **stage / phase** | see §2 → *stage*, *phase*, *tier*, *batch*, *ship art stage*, *sector phase* — six schemes, each naming a different **kind** of sequence | Never a bare number: "stage (iii)", not "stage 3" or "phase 3". Bare "stage" = ship art stage inside the `ship-art-pipeline` skill and `doc/agents/art-pipeline.md` only. Bare "phase" = sector phase inside `WaveDirector` and `Substrate/Sectors/Elements/Phases/` only. |
| **composition** | `IEpisodeComposition` · composition root (DI) · prefab-vs-runtime composition · capture-scene composition | Always qualified. |
| **envelope** | firing envelope · kinematic envelope · scan envelope · MPC travel envelope · shake envelope (§2) | Bare "envelope" = firing envelope; qualify the others. |
| **guard** | the prohibited runtime check (fix-ladder rung 5, pejorative) · a benign regression/test guard · infra guard | The pejorative sense wins in fix-ladder context. Tests say "regression test", not "guard". |
| **anchor** | field anchor / subject (the transform an asteroid field streams and LODs against) / null anchor · archive anchors (file locations) · arena root · anchored intent / enemy anchor (the MPC-resolved frame reference) | Always qualified. |
| **module** | deep module (design vocabulary, §2 → *design vocabulary*) · ship module (chassis/module/loadout) · `-ScopeType Module` (test scope) | Qualify: "deep module" / "ship module" / "Module scope". |
| **mod** | lull mod (§2 → *lull mod*): a weapon mod or a ship mod · the colloquial short form of ship module | Bare "mod" = a lull mod of either kind inside the loot code (`Loot/`, the lull pick); prose names the kind ("weapon mod", "ship mod") wherever the kind matters; a ship module is never shortened to "mod". In a title, qualify regardless. |
| **bench** | benchmark run (bench run, bench config, ram-bench harness) · benched work (`bench/<topic>`, §2 → *benched*) | Bare "bench" = benchmark run. Set-aside work is always "benched". |
| **layer** | sky layer (one drawn backdrop layer — background, far nebula, starfield, close nebula — a renderer under a locale's `LocaleSky` root) · Unity layer (`LayerIds`; notably `Sky`, the render layer every sky layer sits on, which only the flight and hangar cameras' culling masks include) · starfield depth layer (inside `StarField.shader`) · paint mask (§2 → *Art pipeline*; never "paint layer") · shake layer (§2) | Always qualified: "sky layer" vs "the `Sky` layer". |
| **charge** | charge-up / spent charge (`ChargeTime`, the `Charge` parameter) · concussion charge (`Grenade`, `IChargeFlight`, the target marker) | Always qualified in prose: "charge-up", "spent charge", "concussion charge". |
| **environment** | Unity Lighting "Environment" settings (skybox, ambient) · Gizmo View "Environment" category · environment art (`Visuals/Environment/`: asteroids, stations, props) | Never for the locale in new prose — say "locale". Qualify the rest. |

---

## 2. Canonical terms

Format: **term** — definition. *(authority)*

### Workflow & process

- **fix ladder** — the five-rung classification for programmer-error fixes, one
  rung per fix: unrepresentable → earliest deterministic failure → cost gate →
  loud failure → guards (prohibited). *(AGENTS.md § Fix ladder)*
- **operating vs programmer error** — the triage above the ladder: untrusted
  boundary input is parsed once at the boundary; our own invariant violations
  climb the ladder. *(AGENTS.md)*
- **cost gate** — fix-ladder rung 3: the structural fix exceeds the current
  scope, so stop and present narrow-vs-structural to the user. Never downgrade
  silently.
- **guard** (pejorative) — a check that absorbs a bad state and keeps running;
  prohibited for programmer errors. Log-and-continue is a guard wearing a costume.
- **scope conservation** — the confirmed scope bounds the *diff*, not just the
  intent; re-read the diff against it before submit. It never licenses violating
  a design value to touch fewer files. *(AGENTS.md)*
- **fork** — a consequential design decision with named alternatives, surfaced
  to and resolved by the user before building. Not the git sense. *(pr-prep)*
- **locked / frozen** — a fork the user resolved / a brief the implementer builds
  from without re-deciding. The user may still reopen; downstream agents may not.
- **no-brainer assumption** — too obvious to be a fork, but recorded in the brief
  anyway so the implementer cannot silently deviate. A cheap option that conflicts
  with a documented principle is a fork, not a no-brainer.
- **proximity** — the recurring failure mode where the locally cheapest option
  beats a documented principle and hides under "(Recommended)". Surface the
  tension out loud instead.
- **blindsider** — a foreseeable ambush of the *committed* design, hunted in a
  dedicated post-lock pass. Findings meet the fix-ladder entry bar or they are
  noise. *(pr-prep)*
- **entry bar** — what a proposed check, fix, or review finding must clear before
  becoming code: an observed failure or an explicit user pull. (Retired form:
  "entry gate".)
- **speculative** — a review comment about a hypothetical; gets a written reply,
  never code.
- **decision brief** — pr-prep's frozen artifact: scope + fork resolutions +
  assumptions + blindsider resolutions; lives on the arc's issue. *(doc/agents/design-docs.md)*
- **grill** — a user-driven adversarial interrogation that can overturn a
  direction; distinct from pr-prep (plan-seeded) and design consult (external
  agent). All inflections canonical. *(grill-me skill)*
- **counsel** — a multi-seat, multi-model design panel with per-seat value
  charters; its settled principles bind later design within their jurisdiction.
  One spelling throughout, including the `ai-counsel` MCP tool. (Retired:
  "council".)
- **design consult** — handing a brief or diff to a fresh external agent for a
  second opinion, results routed back through fix-ladder triage.
- **evidence bar / rule-of-three** — machinery earns its place by observed need;
  generalize on the third instance.
- **arc** — a multi-PR narrative with a declared end: its issue's
  `## Exit criterion`. *(doc/agents/issue-tracker.md → Body law)*
- **active arc / waiting arc** — an open arc or wayfinder map at `pri:now` /
  any other open one. Three active is the suggested ceiling: arcs wait on the
  user's attention, not on build capacity. "Waiting", never "parked": parked
  work is closed. *(doc/agents/issue-tracker.md → Body law)*
- **slice** — a sub-unit of an arc, each getting its own short pr-prep,
  planned one ahead: each slice's pr-prep has rewritten the plan behind it.
- **build slice / prototype slice** — the two slice types a brief opens with.
  Discarding a prototype slice is an acceptable result, and unlike a
  `wayfinder:prototype` ticket's artifact its PR merges when accepted.
  *(.claude/pr-prep.md → Arc planning)*
- **pass** — a bounded one-shot sweep with no successor (hygiene pass, texture
  pass). Retired for this sense: "program", "package", "series".
- **arc & PR naming** — arcs and slices carry BOTH a descriptive, branch-style
  name (`vocab`, `vocab-docfix`) and a positional label within their plan
  (`Slice-C`, `PR-4`): the word name is the identity, the label is the rank.
  Plans assign both; chat titles show both. Branches and leases use only the
  word path (`task/vocab-seed`) — labels never appear in git refs. A leaf
  number for one named unit spanning several PRs (`vocab-docfix-1..3`) still
  appears only at build time. Once a PR merges, its GitHub number is a
  historical fact and never renumbers. Max three levels; a fourth means you
  have two arcs. (Amended 2026-07-31: positional labels un-retired — the
  2026-07-29 word-only ruling left slices without a quick rank/order handle.
  Plans written under it gain labels lazily, as their slices are next touched.)
- **SHIPPED / CLOSED** (arc status) — every planned PR merged, nothing left over /
  the arc ended deliberately with residuals deferred, named on the same line.
  (Retired: "COMPLETE".)
- **stage** (roman i–iii) — a chapter of a research campaign; each ends in a
  decision. Sequential, complete all.
- **phase** (letters A/B) — a segment inside **one continuous run** (Phase A =
  3.5M steps scripted, Phase B = 1.5M hybrid). Retired: "Phase 0–N" as a chapter
  scheme — those were arc slices and take branch-names today.
- **tier** (0–3) — a rung of the screening ladder, where you **stop as soon as
  something falsifies**. Escalation, not a sequence to complete.
- **batch** (letters) — unordered parallel buckets of independent work.
- **prime mark** (`X′`) — superseded scope or section; the successor is
  authoritative and must be named at that document's first use of the mark.
- **ratchet** — apply a standing rule only to hunks you touch; whole-file sweeps
  live in dedicated hygiene PRs. Instances: comment ratchet, header ratchet,
  vocab ratchet, structure ratchet (folder taxonomy / one-type-per-file /
  namespace and asmdef naming, `doc/agents/unity-conventions.md`), design ratchet and agent-doc ratchet
  (AGENTS.md → Design & agent-doc ratchets).
- **rescue sweep** — salvaging valuable strays (scratch probes, orphaned docs)
  into an infra-hygiene PR rather than losing them to a slot reset.
- **tracking surfaces** — GitHub Issues/PRs = what / for-when / why / status;
  the pool (slot leases, `held/*` branches) = right-now claims; memory =
  feedback notes only. Never conflate. *(doc/agents/memory.md)*
- **parking lot** — deferred *discussion* items, not work items; add on park,
  delete on resolution. *(memory)*
- **handoff** — a brief a new session reads cold to take over separate work (a
  child issue's owner, a worker); it rides the prompt that starts that session.
  A session's own long task compacts instead (re-ruled 2026-10-19; AGENTS.md →
  Session hygiene).
- **build worker** — the agent that writes a PR's implementation from the
  session's build brief: Grok through `scripts/grok_worker.py` by default, an
  Opus subagent as the fallback.
  *(agent-worktree-pr-loop → Step 2; #1252)*
- **chief of staff** — the session that owns one issue with child issues, such
  as a roadmap over its arcs or an arc over its slices. Short form: **chief**.
  A user-level skill outside this repo sets its rules, and #1040 carries the
  why and the trial's 2026-10-28 verdict.
  *(`~/.claude/skills/chief-of-staff/SKILL.md`; #1040)*
- **DRI** — Directly Responsible Individual. Here the individual is a session,
  the one answerable for carrying an issue's work through to the end. It
  answers for follow-through only, because the user makes the decisions. A
  *chief of staff* is the DRI for its issue. *(chief-of-staff skill; #1040)*
- **slot / pool / lease** (workflow senses) — a pooled `agent-N` worktree / the
  pool machinery / the durable claim on a slot. *(agent_worktree_pool.sh)*
- **held** (work) — slot work set aside while it waits on the user; mechanics
  are `hold` / `resume` in the pool script's `--help`. The issue stays open and
  carries the resume line; `pool status` lists the held lease. *(#559)*
- **benched** — set aside until a dependency or missing knowledge unlocks it;
  expected to finish. Branch `bench/<topic>` on origin, kept. Issue closed,
  first line `Benched <date> — reopen when …`. Comments
  before 2026-09-21 opening `Shelved <date>` mean benched. *(#559)*
- **parked** (work) — not worth pursuing; kept for posterity, not expected to
  resume. Branch `park/<topic>` on origin. Issue closed as not planned, first
  line `Parked <date> — why`, no label. The probe-code
  step *scratch → promote → park* is a separate sense. *(#559)*
- **warm** (slot) — its Unity Library is already built; a reason to name a slot
  on acquire instead of auto-picking.
- **primary tree** — `D:/amind/git/astronomical-home`, as against the `agent-N`
  slots; the owner's checkout and the home of pool state. Agents write nothing
  else there but main's fast-forward and sound work (AGENTS.md → Default
  workflow). **Clean primary**: no untracked files outside the sound-work paths
  and no ignored files outside the allowlist, both lists in
  `scripts/primary_tree_check.sh`; tracked edits don't count.
  Short forms: **prim tree**, **primary**. "Main" is exclusively the git branch.
- **app worktree** — the per-session worktree the Claude desktop app makes under
  `.claude/worktrees/` in the primary tree, on a `claude/<name>` branch. No agent
  work happens in one; `scripts/app_worktree_guard.py` refuses session starts
  that would make one. *(#1058)*
- **merge gate** — the full-suite test gate inside `merge <slot>` and `land`,
  the pool's fallback beside GitHub auto-merge on a green `land-check`.
- **`land`** — the pool command that lands a PR no slot holds: the merge gate
  on the hosted path, in a borrowed slot, on a *recorded instruction* or the
  *auto-merge class*. Always
  in code font: the plain word keeps its everyday sense ("land it" is
  consent). *(agent_worktree_pool.sh `land`; #830)*
- **balance-only landing** — merging a PR that only edits existing Settings
  assets or prefabs with no merge gate: no tests, review or recorded
  instruction, only the user's merge instruction. A change that adds a file
  or edits a test is not one. *(agent_worktree_pool.sh `land-balance`)*
- **`land-check`** — the commit status GitHub auto-merge waits on: green
  hosted proof on the PR head, no unresolved thread or open merge-order PR,
  and nothing owed unless the user's instruction is recorded. Posted by
  `.github/workflows/land-check.yml` from main's copy; the head need not
  contain main. Always in code font:
  the plain phrase means checking anything before landing.
  *(agent_worktree_pool.sh `land-check`)*
- **merge turn** — the pool-wide right to run a merge gate, held by one gate at
  a time from before its fetch through `gh pr merge`. Waiting gates take it in
  arrival order (**turn ticket**). Any other push to main takes it with no
  ticket, whenever it is free: `land-docs`, `land-balance`, or a command run under
  `lock merge-turn`. A waiter gives up only after watching one holder keep the
  turn for the cap; a line that keeps moving times nobody out. Machine-local:
  a base move from any other clone is caught only by the gate's base re-check.
  *(`with_merge_turn`, agent_worktree_pool.sh; #639)*
- **turn ticket** — a waiting merge gate's recorded arrival; the line for the
  merge turn is the live tickets in arrival order. A ticket is live only while
  its gate holds an OS lock on it — never by a timer or a pid check — so a dead
  waiter cannot block the line, and a re-run gate arrives anew at the back.
  Always "turn ticket": the Unity access coordinator's queue ticket is another
  thing. *(`MERGE_TURN_LINE_PL`, agent_worktree_pool.sh; #639)*
- **merge-grade proof / tested-tree proof** — a recorded tree hash from a green
  full run, produced on this machine or as **remote proof**. Scoped runs never
  produce one.
- **remote proof** — proof the merge gate accepts from a GitHub-hosted run, as
  `success` commit statuses on the landing commit: `merge-proof/headless`
  (merge-grade test proof; trailer stamps the landing tree) and
  `merge-proof/resharper` (the **hosted ratchet**). The merge gate never
  accepts either for a landing diff that touches `.github/` — that PR can edit
  the workflow that proves it. *(`verified_remote_status`, agent_worktree_pool.sh)*
- **hosted ratchet** — the ReSharper ratchet run inside the hosted
  headless-suite job, on the solution files that job's test boot wrote. Its
  trailer stamps the landing tree AND the base tree it diffed against, so a
  moved or non-main base is no proof and the local ReSharper ratchet runs.
  *(.github/workflows/headless-suite.yml)*
- **headless suite** — the merge gate's test selection run with no GPU and
  without the heavy art/audio files (light LFS checkout). A test that needs
  one of those files cannot live in it.
  *(.github/workflows/headless-suite.yml)*
- **script-suite selection** — the merge gate runs only the `scripts/tests/`
  files the landing diff selects through their **covers lines**, falling back
  to every file on the run-everything triggers. Always qualified: bare "test
  selection" is the Unity run. *(doc/agents/script-contracts.md sec.4)*
- **covers line** — a script test file's `# covers:` header naming the
  scripts it runs or loads. An entry matching no file refuses the suite. Not
  "coverage": that word is the merge gate's full-coverage proof.
  *(read_covers_line, agent_worktree_pool.sh)*
- **inert diff** — a behaviour-neutral delta (docs-only, comment-only) that
  extends existing proof without a fresh run.
- **consent / merge instruction** — an explicit "merge it". Praise is not
  consent. It binds the commit the user saw: a later delta needs a fresh
  instruction, except that a *recorded instruction* survives an inert diff
  (`land` decides).
- **recorded instruction** — the user's merge instruction for one PR at one
  commit, kept as a PR comment that `drain_pick.sh` parses (`land-facts`,
  `merge-queue`, `decision`). Sessions write it through `instruct`, which
  takes the PR's head when no commit is named, or post the same comment from
  a cloud thread. The user never types it; the parser still accepts a typed
  prefix (`Merge instruction: abc1234`). It
  *covers* the landing tree when merging its commit with base gives that tree,
  or one an inert diff away; `land` needs a covering one unless the PR is in
  the *auto-merge class*. Every session is the same GitHub account, so policy
  alone keeps it the user's.
  *(scripts/drain_pick.sh `instruct`; #830)*
- **spend** — compute expenditure needing its own explicit approval; a run is a
  run, not a PR.
- **disposition table** — the per-review-round table, one row per comment:
  Fixed (rung N) / Rebutted / Deferred.
- **ready queue** — open issues labelled `ready-for-agent`, unblocked,
  unassigned, carrying a build-scope block (a `Ready proposal` comment the user
  saw before labelling — see *readiness proposal* — or a body in slice-issue
  shape). A labelled issue with no build-scope block stays out of the queue: it
  gets a proposal instead of a build. A *cloud batch* admits items by their
  `unity:*` label and claims each with the assignee plus `drain:building`.
  *(#617, #830, scripts/drain_pick.sh)*
- **decisions view** — the board view "Decisions": the open issues and PRs
  whose `Decision` field is set, each row the one line the user is asked. An
  item enters with a `Question` comment or pipeline-PR state (tried or `eyes`
  items, a merge no recorded instruction names) and leaves on a ruling comment,
  the state change, or a close. Askers only comment: the *decision reconcile*
  is the field's only writer. Interactive sessions still ask in their chat.
  *(scripts/drain_pick.sh `decision`; #830)*
- **triage sweep** — the on-demand or daily triage run over the open tracker,
  one evidenced verdict per issue. Recurs, so not a *pass*.
  *(.claude/skills/issue-triage)*
- **on-event triage** — the mechanical triage run on issue opened / edited:
  board add + *board kind* sort, one-priority rule, retry / premise /
  dead-pointer checks; writes labels and at most one comment, and treats issue
  text as data. *(.claude/skills/issue-triage/on-event.md)*
- **board kind** — the board's Status read as the sort of attention an issue
  needs: Mechanical, Complex, Judgement, Visual or Bugs, and Triage while
  unsorted. Agents write it only while Status is empty or Triage; any other
  column is the owner's.
  *(rubric: .claude/skills/issue-triage/on-event.md)*
- **merge reconcile** — the mechanical triage run on each push to main: for
  every squash-merged PR in the push, a Shipped note on the issues it closes,
  a Touched note on the open issues its body cites, a listing
  of `#N` citations of the closed issues left in the agent docs, and a warning
  when the body disclaims a close the PR performs. Never closes or reopens an
  issue; idempotent on re-run. *(scripts/merge_reconcile.sh)*
- **decision reconcile** — the Actions job that evaluates one issue or PR with
  `drain_pick.sh decision` on each comment, close or PR change and writes its
  board `Decision` field, with an hourly `--all` backstop. It adds items, PRs
  included, and never removes one. *(scripts/decision_reconcile.sh)*
- **sweep lead** — a mechanical reason for the *triage sweep* to research an
  open issue since a watermark: a merged PR cites it, an issue it cites
  closed, a path it names is gone, or it was itself updated. With `--since`,
  an issue with none is *quiet*: a report row, no subagent. Computed from PR
  and issue data, never from the *merge reconcile*'s notes. Gap: a PR that
  obsoletes an issue without citing it leaves no lead — only a full sweep
  catches it. Always "sweep lead" outside the triage skill (the Gunner's
  firing lead is unrelated). *(scripts/sweep_leads.sh)*
- **readiness proposal** — the *triage sweep*'s queued `Ready proposal <date>`
  comment proposing `ready-for-agent`; its `Unity:` field, one of four values,
  mints the matching `unity:*` label on the same `Apply:` line. Once the user
  applies the label it is the issue's build-scope block.
  *(.claude/skills/issue-triage/comment-formats.md)*
- **drain pipeline** — build, verify and merge of *ready queue* items as three
  steps, each started from queue state on GitHub and never from a chat
  handoff, so a fresh session continues where a dead one stopped. *(#830)*
- **cloud batch** — the *drain pipeline*'s build step: one hand-started cloud
  session that picks and claims every item the *ready queue* admits, then
  builds them in parallel. One at a time, which is what makes a claimed issue
  with no PR a dead batch's. Bare "batch" reads this way only inside that
  skill section; elsewhere it is the letter-bucket scheme above or Unity's
  batch mode.
  *(agent-worktree-pr-loop → Cloud batch)*
- **cloud build** — one item's build inside a *cloud batch*, ending in a PR,
  opened ready for review, that closes the issue.
  *(agent-worktree-pr-loop → Cloud batch)*
- **owed-local checklist** — the `### Owed local` list in a *cloud build*'s PR
  body: the tests the hosted suite cannot run, written by the builder. Its
  ticks and result lines are written only through `drain_pick.sh result`,
  except an `eyes` item, which a person ticks. `None.` means nothing was ever
  owed, as against owed and all ticked. *(grammar: scripts/drain_pick.sh `owed`)*
- **pipeline PR** — an open PR against `main` whose body has a `### Owed local`
  heading. The heading is the whole membership test: draft state, the closing
  issue and its labels play no part, so a PR built by hand joins by writing
  the section. *(scripts/drain_pick.sh)*
- **verify queue** — the *pipeline PRs* with an owed `unity` or `script` item
  no run has tried on the head commit. An item tried there (failed or not run)
  leaves the queue until the head moves. *(scripts/drain_pick.sh `verify-queue`)*
- **verify task** — the desktop scheduled task `verify`, started by hand: one
  pass over the *verify queue* in one borrowed slot. The only *drain pipeline*
  session that boots Unity. *(agent-worktree-pr-loop → Verify task)*
- **main arm** — re-running a failed owed item on `origin/main` in the same
  slot. The same failure there makes it pre-existing: an issue is filed, and
  the item stays tried for the user to rule on. *(agent-worktree-pr-loop →
  Verify task)*
- **merge queue** — the *pipeline PRs* whose checklist is `None.` or all
  ticked, with their facts (among them `instructed`, and `class`: the
  *auto-merge class* as GitHub shows it) and landing order; it never says who
  may merge. Always qualified ("the pipeline's merge queue", or the verb
  `merge-queue`): it is neither GitHub's merge-queue feature nor the line for
  the *merge turn*. *(scripts/drain_pick.sh `merge-queue`)*
- **auto-merge class** — the PRs `land` merges with no *recorded
  instruction*; its conditions are in `land`'s `--help`. Each such merge is
  labelled `auto-merged`, and the *pipeline digest* lists it until the user
  removes the label on review: the user's review comes after the merge. It
  means "nothing owed", so discharged PRs stay out; it widens only on
  evidence. *(agent_worktree_pool.sh `gate_class`; #830)*
- **merge task** — the desktop scheduled task `merge`, started by hand: one
  pass over the pipeline's merge queue, landing each instructed or class PR
  through `land`. *(agent-worktree-pr-loop → Merge task)*
- **pipeline digest** — the Markdown report of what waits on the user and on a
  session the user starts, relayed as printed. Bare "digest" reads this way
  only in drain-pipeline text. *(scripts/drain_pick.sh `digest`)*
- **chunk-down** — replacing a class of remembered failures with a deterministic
  tool ("preflight, don't remember"). *(postmortem)*

### Design vocabulary

- **deep module / interface / seam / adapter / depth** — the design-vocabulary
  set; the authority is the `codebase-design` user skill (`~/.claude/skills`),
  not this file. Gists: behaviour concentrated behind a small interface / every
  fact a caller must know, not just the type surface / where an interface
  lives — the place behaviour can vary without editing there (Feathers sense) /
  a concrete thing satisfying an interface at a seam / leverage per unit of
  interface a caller must learn. Canonical for design discussion. Seam
  discipline in one line: one adapter is a hypothetical seam, two adapters is
  a real one.

### Harness & balance

- **opponent archetype** — the law an `ArchetypeBrain` flies when no temper
  picks its tactics: the matrix lane's weapon-pair shooters and test pilots.
  *(OpponentArchetype)*
- **replicate** — one complete re-execution of an identical harness run
  (same seed set, episodes/seed, tree) in a fresh boot, differing only by
  mechanical sim nondeterminism. NOT a new seed draw (that samples different
  episodes) and NOT a cross-tree re-run (that confounds code drift with draw luck).
- **marksmanship probe** — the harness probe that counts, per episode, what the
  shooter's weapon fired and what it hit. A hit is one damage event on the
  target, so a missile's contact and splash are not told apart, and a shot
  still in flight when the target dies is fired and never hits.
  *(MarksmanshipSampler)*
- **matrix lane** — the harness lane that fights two scripted ships, either as a
  weapon pair or as one reference ship against each ship in a roster. A roster
  block fields 1, 2, or 3 copies of that ship, and can replace the reference
  pilot's break-off shield for the run. Its rows are a snapshot, not a constant:
  judge a change by running the lane on the base and the head with the same
  seeds, and quote the replicate gap between two identical runs as the noise.
  *(#1120 · MatrixLane.cs)*
- **armed matrix** — every unordered pair from the weapons a matrix lane was
  given, a weapon against itself included. A draw counts half a win, and a
  weapon's rate against the others leaves its self pair out.
  *(#1120 · MatrixProbe.cs)*
- **command churn** — commanded facing movement per decision (measured 48°)
  exceeding the **slew budget** (yaw rate × decision period = 36°/decision). The
  cause.
- **facing thrash** — the visible symptom command churn produces. (Retired:
  "yaw-thrash". Not to be confused with **twitch**, reserved for the MPC
  obstacle×tactical defect, or **chatter**, a metric that provably does not
  capture thrash.)
- **plan fast-forward** — the MPC warm start advancing more plan time per
  solve than sim time has elapsed: the solver re-plans every fixed step
  (0.02 s) but consumes one rollout step (0.1 s) per solve, so the plan runs
  5× ahead of reality. Observed 2026-08-06; evidence in
  #486 §The churn diagnosis. *(Mpc.ShiftSequenceForward)*
- **seed sets** — two roles, do not mix them: the `train` selector runs seed 1
  (`HarnessSeeds.TrainingRunSeed`); **1001–1020 is the sealed held-out set**,
  the default — reserved for defensible claims, and a knob change resets the
  protocol rather than re-opening it.
- **brawl** — the degenerate equilibrium where optimal range is provably ~0, so
  the game prices no decision. The motivation for the rules change.

### Game & sim

- **substrate** — the session-shaped package both clients share: `Session`,
  sectors, the three services and the presentation flag, and the plane, tag and
  layer conventions. Rooted at `Substrate/`; the interactive game (`Game/`) and
  the matrix harness (`Harness/`) are its two clients.
- **session tier** — the lifecycle layer (`Substrate/Sessions`) whose one
  type, the self-orchestrating `Session`, composes a session's services and
  cycles sectors; a *host* paces it and hands each load the *hero* it built —
  the session owns no rig and no policy. The matrix harness does not use it; it
  composes the same per-ship services through `ShipServices.Compose`.
- **host** — the scene entry point that owns pacing and lifecycle for one
  client: the *game host* (`GameHost`, `Game/`) builds the viewport (the
  observer camera, with the starfield backdrop and reverb zone riding it, and
  the hangar camera that stands in for it behind the hangar) and
  the optional *player rig*, and owns the clock, hangar, sector chaining, hull
  carry-over, death recap, win screen and reset policy over one `Session`; the
  *harness host* (`HarnessHost`) sequences
  compositions and episode blocks for a lane client. Always qualified — bare "host" also names the pool
  worktree machine.
- **player rig** — what the interactive game puts into a session for the human:
  the player ship and its commander, the HUD (overlay, UI and minimap cameras),
  the pending loadout, the damage ledger, the run tally, the spawn log and the death hook. Built once by the
  game host against the viewport it owns, injected into every sector load, torn
  down at session exit. A host with no rig assigned has no player.
  *(`PlayerRig`, `Game/`)*
- **hero** — the main character of a session: the ship a sector lays out around
  and the session resets to the sector's start point. The player in the
  interactive game, possibly an AI in a scenario.
  *(`Session.LoadSector(hero)`, `SectorBuildContext.Hero`)*
- **start point** — where a sector begins: an authored plane position the sector declares,
  baked by the manifest sync from an optional marker child (the sector root
  otherwise). A fact of the sector, not of whoever flies: the field carves its
  permanent clearing around it even with no hero. *(`Sector.StartPoint`)*
- **session frame** — the in-plane frame a session's authored content is placed
  in (offset + `Place`); zero for the single-arena game, a per-arena offset for
  anything fanning sessions across one plane. A session fact, distinct from the
  obstacle field an AI ship senses, which rides the spawn call on its own.
  *(`SessionFrame`, `Substrate/Sessions/Session.cs`)*
- **session scene** — the scene a `Session` keeps active from construction to
  teardown. Unity puts an object created without a parent into the active
  scene, so such an object dies with the session. The session builds only its
  arena root there; the host's viewport, UI, rig and music stay under the host,
  which outlives its session. `SessionSceneLease` unloads the scene when its
  host is destroyed without a teardown. The active scene is process-wide, so
  with several sessions alive every unparented object lands in the newest one's
  scene. A host created while another session is alive lands there too and
  dies with that session's scene, unless it calls `DontDestroyOnLoad` as
  `GameHost` and `PreviewHost` do. *(`Session`, `SessionSceneLease`)*
- **sector** — a bounded open-space field of POIs. The load-bearing decisions:
  it builds **deterministically** from a serialized manifest, and there is **one
  concrete class**, configured by prefab — variation never arrives as a subclass.
  *(Sector.cs)*
- **sector spine** — the sector's main objective chain, set by its spine module.
  `SectorSpineModule`'s step names ARE the bus tokens (`spine:ready-to-extract`);
  `PhaseSpineModule` latches none. *(SectorSpineModule.cs, PhaseSpineModule.cs)*
- **activation rule / term / latch** — `predicate(terms) → effect`; terms are
  AND-ed, evaluated as a standing *level* rather than an enter-edge, and latched
  Active on first satisfaction. *(ActivationRule.cs)*
- **presence ≠ arming ≠ firing** — exists in world / trigger listening /
  predicate fired. "Present-but-inert" is the named middle state.
- **encounter** — the *fat* activation rule: rule + lazily-spawned content +
  local objective + on-complete events. A thin rule on a fixture is not an
  encounter.
- **wave director** — the continuous sector spawner. It sends waves inside its
  sector phases. Dead products are despawned as it goes. Content, not a sector
  module, so product ownership and teardown stay the spawner's. *(WaveDirector)*
- **wave** — one rival leading minions, arriving together from one bearing just off
  screen. The rival takes the arc's centre and the minions take the slots either side.
  A rival whose temper ambushes arrives concealed ahead of the player instead, when
  that point is clear. A phase that sends waves draws how many minions fly with the
  rival uniformly from fewest to most, inclusive. Waves arrive on the phase's wave-every beat, trimmed to
  the wave caps; with no room for a rival, the beat sends a reinforcement instead. Nothing arrives in a lull, and survivors stay.
  *(WaveDirector.WaveDue)*
- **reinforcement** — what a beat sends when the rival cap is full: the beat's
  drawn minions, trimmed to the minion cap, arriving together from one bearing to
  join the live rival with the fewest followers (on a tie, the one nearest where
  they arrive), each at the free place nearest it. A reinforcement is not a wave
  and raises no `WaveSent`. With no minion room, the beat is dropped. Entourage,
  ruled by the game lead on 2026-11-23: a full rival slot no longer drops the
  beat, so clearing minions buys only a window until the next beat. Once an elite
  has fought for 30 s (its grace), each beat of the last sector phase sends a
  reinforcement to the elite instead, until the elite dies (ruled by the game
  lead on 2026-11-23, so a kited elite fight brings minions).
  *(WaveDirector.TryReinforce, WaveDirector.FreePlace, WaveDirector.eliteGraceSeconds)*
- **wave every** — the beat a sector phase sends its waves on.
  *(SectorPhase.waveEvery)*
- **wave cap** — a sector's two limits on what its waves fill to: the rival cap
  (live rivals) and the minion cap (live minions, hunt patrols included). A full
  rival cap turns the beat into a reinforcement for a live rival, so minions never
  arrive without a rival; a full minion cap sends the rival alone. Nothing is held
  or owed: a trimmed or dropped beat is not made up later.
  *(SectorEntry.rivalCap, SectorEntry.minionCap, WaveDirector.RivalRoom, WaveDirector.MinionRoom)*
- **earned lull** — the rest of a sector phase once its last wave has arrived and
  nothing is alive, patrols included. A dropped wave counts as arrived. It moves
  no phase boundary. *(WaveDirector.EarnedLull)*
- **closing lull** — the scheduled lull after a sector's last phase, before its
  elite. A sector with no elite has none.
  *(SectorPhaseSchedule, SectorPhaseMark.Closing)*
- **sector phase** — one timed row of a wave director's table. The row holds
  whether it sends waves, how many minions fly with each wave's rival, and its
  wave timing. The player-facing HUD says "PHASE". *(SectorPhase)*
- **survival trial** — a sector whose goal is surviving its sector phases and
  then reaching the extraction zone (`PhaseSpineModule`). A run chains three,
  each faster-paced: extracting moves the run on, extracting from the last wins
  it, and the game host's player-death path ends it. All three fly one layout,
  and their rows hold what differs. *(TrialSector prefab, `SurvivalTrialRun`)*
- **hunt** — a combat sector's opening, before its first sector phase. Patrols
  fly a route and hold fire until the alarm. A patrol sees the player inside a
  close radius in every direction, or inside a longer radius and a forward cone,
  when no asteroid is in between. One spotted meter fills while any patrol sees
  the player and drains while none does. A hit on a patrol fills part of the
  meter. A patrol that sees the player, or that the player hits, leaves its route
  and chases. Once it loses the player it searches along the player's last heading
  and then around that point. It keeps searching until the meter drains, then
  returns to the nearest point on its route. Other patrols stay on their routes.
  The alarm sounds when the meter fills or the search clock runs out (its length is the run row's). Killing a
  patrol leaves the meter to drain. A gap of seconds follows the alarm; the hunt's
  *release* ends it. The wave director holds its opening lull until the release,
  so phase numbering is unchanged, and stations stay lit through the gap.
  *(SectorHunt, HuntFeelProfile, `SectorEntry.searchSeconds`)*
- **sight-limited** — a sector ship sees the player within its scout's sight
  range when no rock is between them, and loses the player after the scout's
  lose-sight seconds out of sight, or at once when the player conceals. It circles
  the place it last had the player for its laps, then takes the player back unless
  the player is dead or concealed. A ship that never came back would
  stall the sector, because waves refill only while the count of live ships is
  under the wave cap. WaveDirector asks when it sends a wave, and SectorHunt
  asks when the alarm sounds. RingSpawner, the harness spawners and
  EliteEncounter never ask, so their ships keep an enemy at any range. Ruled
  on #1589.
  *(EnemyTracker.LimitToSight, Scout.sightRange, Scout.loseSightAfter)*
- **faction** — the side a sector's enemies belong to; each survival trial
  names one, and its wave director and hunt field only that faction's ships. It
  flies three ranks, each its own chassis in its own hull paint; the mesh carries
  the rank's size (ruled on #868, 2026-11-21). The **minion** has 70% hull,
  never fires its keystone, and aims worse than the other ranks (ruled on #1589,
  2026-11-17). Hunt patrols and elite escorts fly it. The **rival** leads every
  wave. The **elite** is the third rank. The ranks are ruled on #1589. A
  presented run deals the factions to its sectors in a fresh order at its
  start, so any faction can come first or last; a sector's pacing and elite
  strength stay with its place in the run, and sector 3 always ends at the boss
  (arye, 2026-11-23, replacing #1320's fixed mapping).
  *(Faction.Rival, Faction.Minion, Faction.EliteShip, FactionRank, SectorFaction)*
- **elite** — a sector's end-of-sector enemy: its faction's elite chassis, with
  a heavy weapon pair and stacked run mods. A lull follows the
  last phase, and the elite is summoned when that lull ends. Extraction opens
  on its death. Sector 3's elite is the boss, which escalates at hull
  thresholds (escorts, heavier fire). Past its grace, every elite draws
  reinforcements on the last sector phase's beat.
  An escalation step stacks weapon mods only, so the dynamics the AI captured at
  spawn stay true. Not an *encounter*: no activation rule arms it; the wave
  director owns it and drives it. *(EliteEncounter, EliteProfile,
  PhaseSpineModule.StepElite)*
- **unlocked ship** — a ship the hangar lets the player fly: the starter plus
  the ship each faction names as its elite's unlock, once a died or extracted run
  record shows the player killing that elite (ruled on #868, 2026-11-21). No save
  file; a kill counts once that record is written. An abandoned run earns nothing
  from the file. *(ShipUnlocks, Faction.unlocks, ItemSubset.starter)*
- **station** — a *find* the player flies into. Every kind shares one core:
  the placement, the clearing in the field, the find marker and the arrival
  events. Going dark at the hunt's release is the default. A kind may keep its
  supply and relight. A kind adds only its effect, which draws on each station's
  supply; a station goes dark when its supply runs out.
  *(FindStation, FindStationBody, StationKind)*
- **station dress** — how the sector's faction dresses its *stations*: one
  dressed model per kind, with the faction's parts and hatching baked in, and
  palettes that each station rolls from its placed position, never from
  `UnityEngine.Random`. The hatch strength lives in the model's Ink mask, so a
  palette has no hatch field. Its window light is an emission calibrated against
  the game's grading, not the target colour. No station is undressed in play: a
  row that places stations its faction can't dress fails the row check. Service
  lights keep their find colour in every dress.
  *(StationDress, StationPalette, StationModel, `Faction.stationDress`)*
- **repair station** — a fly-in zone each combat sector places, as many as its
  run row names, each on its own clear point away from the start, marked as a
  *find*. While the player sits in it, it repairs hull from a supply of about
  one hull, then goes dark. The hunt's release darkens it, and its supply stays
  for the whole sector. It relights in each lull, scheduled or earned, and stays
  lit for a grace window into the next phase. Hull carries into the next sector.
  The first *station* kind. *(RepairStation, RepairStationFeelProfile,
  `SectorEntry.repairStations`)*
- **beacon cache** — what a lull beacon's claim grants:
  one roll among the authored mod bonuses that fit the hero (damage, fire
  rate, shield regen), all weighted alike. A bonus joins
  the run's mods for 20 s by default, adding to lull picks rather than
  multiplying them, and the HUD lists it with its seconds left. Each claim's
  bonus runs on its own clock, and sector teardown takes back any still
  running. The *hull patch* is a further roll that ships with a weight of 0, so
  no claim rolls it and repair stations are the sector's hull repair. Weighted
  in, it restores 30% of a full hull at once, touches no mods, and wastes what
  passes full. A low-hull lean, off by default, multiplies the patch's weight
  while the hero's hull is below a threshold. No beacon, no
  cache: headless and harness sectors never grant one. *(BeaconCache,
  BeaconCacheFeelProfile, IObjectiveService.ShowBoost)*
- **mod station** — the second *station* kind: a *find* each combat sector
  places, as many as its run row names, spaced from each other and from the
  repair stations.
  The first fly-in opens one *lull pick*, then the station goes dark; the
  hunt's release darkens an unclaimed one, and its pick is lost. Headless and harness
  runs claim it with no pick. *(ModStation, `SectorEntry.modStations`)*
- **armory** — the third *station* kind: a *find* whose count per sector its run
  row names, spaced from the repair and mod stations. Each fly-in opens
  the *weapon pick*. Taking a weapon spends it; a skip leaves it live to come
  back to. The hunt's release darkens it like any find, and its swap is lost.
  Headless and harness runs get no pick. *(Armory, `SectorEntry.armories`)*
- **find** — a sector object the player goes looking for and uses once:
  repair stations, mod stations and armories. A live find registers
  with the objective service. In range the minimap shows the find's own
  marker; out of range, an *edge pointer* (its icon pinned to the minimap rim,
  aimed at it). Live enemies, patrols included, get an edge pointer too: an
  arrowhead alone, in the minimap's enemy red; a concealed one gets none.
  Each kind has its own find colour, apart from ship markers; the icons are
  white and take it as a tint. Spent or shut by the hunt's alarm, it closes
  its registration and both go.
  *(IObjectiveService.MarkFind, FindMarker, FindPointers, FindColours)*
- **sector fixture** — a world object present at spawn, sector-owned, independent
  of encounter state.
- **signal / SignalPort** — the bus coupling seam. ⚠ **Designed, not built** —
  publisher-owned ports are the intended replacement for free-string tokens, so
  a doc describing them is describing the plan; the code is still
  `ActivateOnToken`.
- **adopt vs spawn** (sector) — the placed child IS the runtime object, versus
  spawner-produced. Variation lives in the object or in the spawner type.
- **locale** — the per-sector look, a prefab. Its root `LocaleSky` holds the
  lighting values (flat ambient, fog, custom reflection, sun), and its
  children are the locale's lights, ambience and sky layers. `LocaleService`
  instantiates it under the session's `Locale` root and writes those values to
  the *session scene*'s `RenderSettings`, so showing a locale never changes the
  active scene or the boot scene's lighting. The boot scene carries none: every
  authored sector names one or draws one, and the session shows its idle locale
  between sectors.
- **sector run** — the ordered sectors one run flies, one row each: the sector
  prefab (the layout), the locale it shows or Random, and the values the
  layout's parts read: faction, elite, sector phases with their minion counts,
  patrol count and field preset. The parts take these values from the row and keep no copy.
  Authored as one asset edited as a table, so a locale or pacing change never
  edits a prefab; tests and the headless lane build theirs with
  `SectorRun.Create`, from the shipped run's rows when they fly the trial
  layout. A preview fixture names one of these rows. See *fixture link*.
  Random rows draw from the run's locale pool at each run's start, never
  repeating within the run and leaving out the locales named rows use. The
  draw takes a `System.Random` because the sim reads `UnityEngine.Random`, and
  a headless run draws nothing.
  The host hands each drawn locale to the load and the jump backdrop that show
  it; the session keeps no draw. A session refuses, at construction, a row
  missing a value its layout reads, and a presentation session also refuses a
  named row with no locale and a pool that can't serve its Random rows.
  *(`SectorRun.DrawLocales`, `ISectorRowReader`, `SessionProfile.run`,
  `Settings/Game/SurvivalTrialRun.asset`, `Settings/Game/Preview/PreviewFixtures.asset`)*
- **Sectors window** — one page per game-run sector and per preview fixture.
  The window moves no data. "used by" is found from asset references.
  *(SectorsWindow, SectorPages)*
- **field preset** — a named asteroid field settings asset (sparse, medium,
  dense) a run row picks, as it picks a faction. A layout's field either names
  its own preset or takes the row's, so the session refuses a row naming one for
  a field with its own, or none for a field without. *(`SectorEntry.field`,
  `Settings/Asteroids/SparseField.asset`)*
- **idle locale** — the locale a presentation session shows while no sector is
  loaded (the hangar's backdrop). A sector naming the same locale keeps its
  instance across the hand-off. *(`SessionProfile.idleLocale`)*
- **cloud-bank sky layer** — a sky layer whose `Custom/Nebula` material has
  `_CloudOpacity` above 0. It draws from a seamless tile baked when it enables,
  not live noise, so it must stay static and zoom-free (`_NebulaSpeed` and
  `_NebulaZoomResponse` at 0; the locale wiring test pins both). `_Seed` picks
  the tile's cloud layout. *(`CloudBankTile`, #1048)*
- **palette role** — one of base, primary, secondary or accent: the colours
  Blender exports in a flat background's sidecar `.json`. Scene-linear.
  *(FlatBackgroundSidecar)*
- **palette parent** — a generated Material Variant of a shared sky-layer
  material whose only overrides are palette-mapped colours (`PaletteParents`).
  Never hand-edited: every sidecar reimport rewrites it. Tune a locale's own
  variant instead; its overrides survive the refresh.
- **GamePlane** — the frozen 2.5D convention. Production is `PlaneAxis.Z` (the XY
  plane); never reshape toward Y. *(GamePlane.cs)*
- **arena** — the harness isolation unit. Isolation is **by distance, not by physics scene**:
  arenas are offsets sharing one PhysicsScene, and **ghost rock** — cross-arena
  physical leakage — is a known, accepted consequence rather than a bug. The
  harness's per-arena composition spawns at its offset and hands its field to
  each ship; "arena" names the isolation unit, never a handle or a session.
- **firing envelope** — whether a shot is currently takeable (nose cone, range,
  LOS). ⚠ Read it with `InEnvelope()`, never `Gunsight.Evaluate()` — the latter
  mutates the firing path's LOS cache, so observing changes behaviour.
- **lock-on cone** — the cone around a ship's nose, measured from the ship's centre,
  within which its lock-on sensor acquires and holds a missile lock. Missiles
  launch from the mount, so the cone's tip is not where they leave.
  *(`LockOnSensor.lockOnConeAngle`)*
- **seeker cone** — the angle around a missile's nose within which it can see its
  target. A full angle, checked at half like `LockOnSensor.lockOnConeAngle`. *(`Missile.seekerConeAngle`)*
- **lose track** — a missile drops its target and flies straight when its target is
  outside the seeker cone, or an asteroid cuts the thin ray to `ITargetable.TargetPoint`.
  Checked from launch, no arming delay. It re-acquires the same target, and only that
  target, if the target comes back into the cone with a clear sightline before the
  missile expires. A target that conceals itself is dropped for good. *(`Missile.CanSee`)*
- **tracking missile** — a missile that has a target and hasn't lost track. The
  target's lock channel lists it from launch until it loses track or leaves play,
  and again when it re-acquires.
  *(`Missile.IsTracking`, `LockChannel.Trackers`, `IShipStatus.TrackingMissiles`)*
- **missile evasion** — a scripted pilot leaning away from a tracking missile and
  boosting to escape it. Only missiles tracking its own ship count: a lost track ends
  it, and a missile lock before launch draws none (dodging the lock-on cone would deny
  every missile lock). *(`ArchetypeBrain.evadeMissiles`)*
- **evasion urgency** — 0 to 1, how hard missile evasion leans, from the missile's
  time to impact. *(`ArchetypeLaws.EvasionUrgency`, `ArchetypeLaws.TimeToImpact`)*
- **swept hit test** — each physics step a projectile tests the whole path it is
  about to cover, so a fast shot can't pass through a thin target between steps
  (tunneling). Its trigger collider callback still catches anything already
  overlapping when a step starts. Not Unity's continuous collision detection,
  which skips trigger colliders, and every shot is one. Always qualified: the
  triage and rescue sweeps share the word. *(`ProjectileBase.SweepStep`)*
- **target point** — the world point in the game plane a shot flies to, fixed
  at fire time: the cursor for the player (the nose at max range with no cursor),
  `Gunner.AimPointFor` for the AI. It rides `WeaponCommand.targetPoint`; only
  self-steering projectiles (the concussion charge) read it, and the charge clamps
  it to its max range. Not `ITargetable.TargetPoint`, the lock anchor a missile
  homes on.
- **target marker** — the HUD's small ring at a concussion charge's target point,
  shown for the HUD ship's own charges only: it binds to that ship's launch readout
  (`ChargeTargetMarkers`), never to an "is player" fact. No blast-radius ring: the
  owner ruled it handholding.
- **spent charge** — the charge-up fraction the last shot consumed, announced by
  `ChargeTime` before its charge-up zeroes, so the sound's release holds the
  shot's charge. *(`ChargeTime.SpentCharge`, `OnChargeSpent`)*
- **sustained sound** — an FMOD event instance a component holds across state
  changes, feeds parameters and stops itself (the charge-up, the engine), as
  against a one-shot it starts and releases at once. One component per sustained
  sound; `WeaponFireAudio` is one-shots only by ruling. *(`ChargeWeaponAudio`, `EngineAudio`; #1012)*
- **engage** (fire lane) — the per-slot bool a brain emits: strategic weapons-free,
  never trigger timing. The Gunner owns the firing instant (envelope + lead) at
  physics rate — the brain decides *whether*, the gunner decides *when*. Replaces
  "the trigger is a decision, not a permission" (reversed by the fire-lane rework,
  2026-08-11: a ~0.2 s decision cadence cannot own a 50 Hz instant).
- **fire plan** — a weapon's own AI decision, each step, whether to hold its
  trigger. Any state it keeps, such as a charge in progress or a heat vent,
  lives in the weapon, so the Gunner never learns what a trigger means to it.
  *(`WeaponComponent.HoldTrigger`; #1133)*
- **hold range** (AI) — the distance from its target that a weapon asks an AI
  pilot to fly at, in place of the pilot's own range, as a mine carrier holds
  outside its own blast. Only the primary weapon's counts, and
  it reaches the brain on `AIContext`, never on `IShipStatus`, because it is
  how the AI uses a weapon, not a fact about the ship. Not a firing range.
  *(`WeaponComponent.HoldRange`; #1139)*
- **weapon slot** — a ship's first mount: one weapon (Lasers, TurboLasers, Rippers, Scattergun or
  Railgun) or nothing, never an ordnance. Left mouse fires it, and its weapon is the one
  whose lead, hold range and aim the AI reads. The code still calls it
  `WeaponSlot.Primary`; the rename waits for after M1 (#1418).
  *(`WeaponsController.RequireKinds`; #1410)*
- **ordnance slot** — a ship's second mount: one ordnance (Missiles or Concussion) or
  nothing. An item's kind comes from its class, so nothing is authored and no prefab can
  carry the wrong one. Regular enemies leave the slot empty. The code still calls it
  `WeaponSlot.Secondary`. *(`WeaponComponent.IsOrdnance`; #1410)*
- **single fire** — a weapon fires every shot from the hull's centre socket, on its
  centreline at the nose. Every weapon that is not dual fire fires this way.
  *(`WeaponComponent.FirePattern`; #1410)*
- **dual fire** — a weapon fires from the hull's left and right sockets instead of the
  centre, in one of two forms, alternating fire or volley fire. Left and right are the
  sides the player sees with the prow up under the game camera, so left is the hull's
  local -X. The weapon's class owns the form, so Missiles, Concussion and the Railgun
  have nothing to author. Rippers always alternate, and the TurboLasers variant
  turns on the Lasers switch for volley fire. *(`WeaponComponent.FirePattern`; #1410)*
- **alternating fire** — dual fire from one side socket per shot, switching sides each
  shot. Each shot is one projectile at the weapon's own fire rate, and its fire sound
  plays at the side that fired.
  *(`FirePattern.Alternating`; #1410)*
- **volley fire** — dual fire from both side sockets together on every shot. Each shot
  launches one projectile from each side, spends one shot's worth of ammo or heat, and
  plays one fire sound. The fire sound and the gunsight's line of sight read the centre
  socket, which sits on the centreline between the two muzzles.
  *(`FirePattern.Volley`; #1422)*
- **velocity reference / feasibility tracker** — the brain↔MPC boundary: the brain
  emits a planar velocity and MPC is demoted to a ~2s tracker (feasibility, aim,
  velocity-track).
- **aim reference** — the heading a ship's primary weapon needs this step and
  that heading's rate, or none, published by the Gunner. It parallels the
  velocity reference. The Gunner computes it from the target as it perceives it
  (see aim settings), with the same lead it fires with, and the AICommander
  hands it to the Navigator as a value on each `ComputeCommand` call, so no
  weapon fact enters the Navigator.
  *(AimReference, Gunner.PrimaryAimReference)*
- **aim servo** — the Navigator's yaw controller that turns the ship onto the
  aim reference within the engine's yaw torque. It replaces only the MPC's yaw
  torque, and the Navigator stays the single author of the pilot command. The
  AIM slot's weight switches it in and out with hysteresis rather than blending
  torques, because #871's research found continuous blending flip-flops. An
  obstacle threat hands yaw back to the MPC, which keeps it for a short hold
  after the threat clears. It exists because the MPC's facing cost cannot aim:
  its sampling noise held aim error near 20° whatever the facing cost's shape
  or weight (#409). *(AimServo, YawHandover)*
- **aim settings** — one armed AI pilot's aim knobs, a `ScriptableObject` beside
  `MpcSettings`: its perception (a reaction delay and a wandering aim error with
  its correlation time) and its earned accuracy, each off at 0. The AICommander
  hands it to the Gunner at `Initialize`, and the Gunner reads it every tick, so
  inspector edits apply live in Play. An armed AI pilot without one fails at
  spawn. The servo's turn acceleration stays on `MpcSettings`. These are raw
  knobs by ruling, not a 0 to 1 skill dial; how they map onto difficulty is
  open (#1057). *(AimSettings, AimPerception)*
- **earned accuracy** — a Gunner bearing error that starts loose when it picks
  up a target and tightens to 0 while the target stays in its nose cone, in
  range and in line of sight. A new target, a jink, a boost or a lost line of
  sight restarts it. #409's calibration found it is rarely earned against a
  moving target: a jink threshold low enough to see a strafe also fires on
  ordinary flight. *(EarnedAccuracy)*
- **controller** (probe) — the MPC-retune instrument separating target-motion
  yaw demand from self-generated churn; metrics live in the row schema.
  *(ControllerProbe, ControllerSampler)*
- **obstacle threat** — per-step classification: the MPC's obstacle handling
  fires — hull overlap (`Cost.Collides`) or collision-course turn-away
  (`Cost.TurnAwayCost > 0`). Always the qualified form in prose; the probe
  sidecar's `threat`/`clear` field names are context-bound and stay.
  *(Cost.ObstacleCosts, Cost.ObstacleThreat)*
- **intent sentence** — the decision-varying slice of the MPC cost as a fixed
  set of typed sentence slots (AIM/POS/VEL binding one referent each, LANE
  pinned to the enemy, FIELD a class slot binding none), each with a signed
  authority weight against its settings ceiling; the solver re-resolves every
  armed slot against live referent state each rollout step. An armed weight-0
  slot is a live command ("nothing matters"); an absent sentence is idleness —
  the distinction is structural (`IsIdle` = no armed slot). Design + staging:
  #485. *(IntentSentence, Cost.EvalContext)*
- **LANE slot** — the sentence term over the enemy's fire-lane geometry: a
  ray-segment down the enemy's facing, held (+) or dodged (−) by the weight's
  sign. Referent pinned to the enemy — rocks have no facing, so a referent
  branch would be noise (§Stage C fork 2). Always caps, always "LANE slot" or
  "LANE term" — bare "lane" stays collision-table qualified. *(LaneSlot,
  Cost.Lane)*
- **error-relative POS width** — the per-solve POS saturation law:
  width = max(posWidthSlope × err₀, posWidth), with err₀ the ring error at the
  solve's initial state, frozen for that solve's rollouts. Reach and settle
  stop sharing a width by construction; the asset `posWidth` is the near-field
  settle floor, and per-row width overrides are retired. Ruled §Stage C fork 3
  over authored-parameter scaling and a policy width channel.
  *(Cost.EffectivePosWidth, Mpc.ApplyErrorRelativePosWidth)*
- **intent grammar** — the closed design space intent sentences are written in:
  a term is a point in space × sign × geometry × frame/referent; slots sort
  instance-vs-class by the timescale rule (focused entities bind instance
  referents, ambient populations are class terms the solver resolves); six
  admission rules gate new vocabulary. "Closed" means syntax never grows, only
  vocabulary. Frozen at Stage B (2026-08-13) with the normalization contract.
  *(#485 §The grammar + §Stage B freeze)*
- **anchored intent** — the enemy-bound AIM+VEL degenerate intent sentence,
  today's production shape: a facing offset around the enemy intercept anchor,
  and a polar velocity in the enemy frame, each with a [0,1] weight. The MPC
  re-resolves both against the predicted enemy every rollout step, so the
  command never goes stale. **Sign pins:** radial > 0 closes along +losHat;
  tangential > 0 and positive facing offsets are CCW; the polar velocity is
  *relative to the enemy's motion*. Since Stage C3 the action-side VEL mapping
  is the θ-head — unit direction × the character-axis `speedRef`, vector
  magnitude = the slot weight. Authored through `NavObjective.Anchored(...)`;
  the `IntentSentence` struct is the solver-side carrier.
  *(AnchoredBuilder, IntentSentence, Cost.EvalContext)*
- **delegation prior** — the low-weight, config-gated fallback that steers a
  channel when its anchored authority is 0: facing eases to the velocity-aligned
  pose (`wFacingPrior`), velocity keeps course (`wMomentum`). Weight-0 reads as
  competent coasting, never drift. *(Cost.FacingPriorCost, Cost.MomentumCost)*
- **terminal ramp** — the MPC multiplier that scales *state* costs up toward the
  horizon end (reaching semantics); control terms and the velocity tracker
  deliberately sit outside it (regulation semantics). *(Cost.Evaluate)*
- **nav objective** — the decision-varying slice of the MPC cost function, and
  the only thing that crosses the seam's navigation lane: an intent sentence
  plus the legacy world-plane move channel, and (since Stage C2) the rock-seat
  identities behind the sentence's non-anchor referents. Two constraints are
  structural, not conventions: referent-frame slots are reachable only through
  `Anchored(...)`, so an anchorless one cannot be authored, and the move
  channels (world-plane vs VEL slot) overwrite each other, so only one
  survives. Idempotent per decision — re-applying one is safe, which is what
  lets a 5 Hz decision be re-pushed every tick. *(NavObjective, AnchoredBuilder)*
- **rock seat** — one of the three synthetic-referent identity carriers (sentence
  referent 1–3) riding a nav objective beside its sentence: the sentence's own
  max, since only AIM/POS/VEL bind one referent each. Asteroids bind by
  component-ref + spawn-epoch — an explicitly interim identity, replaced by an
  AsteroidId registry on its carded trigger (#423); the host re-resolves every
  seat into a fresh snapshot each tick, and a dead seat rides to the solver
  invalid (its slots drop to weight 0 until the next decision).
  *(AsteroidRef, NavObjective.RockSeat, CostInput.referent1–3)*
- **brain / decision lane** — the swappable-decision seam. A **brain** is the
  component that decides: one `Brain` subclass per policy, installed through
  `AICommander.InstallBrain` or authored on the pilot prefab. ("Chooser" was the
  interface-era name for the same thing — retired with `IIntentChooser`.) A
  `BrainDecision` is a transport, not a union: the commander opens it and routes
  each lane to a different consumer, keeping nothing. The nav lane is *solved*
  into a `PilotCommand` by the MPC; the fire lane is a per-slot **engage** bool
  the Gunner converts into trigger commands; the ability lane is a one-shot
  activation. On the nav lane, objective and command are different altitudes
  rather than synonyms.
  ⚠ Not to be confused with `PilotCommand`/`IPilot`, which are the actuator end.
  *(Brain, BrainDecision)*
- **tactic**. What a ship picks to fight with: attack run, flank, stand off,
  fall back, break off, charge, ambush, or hold the line. A ship picks for its own sake, the best shot for the least risk,
  and never knows it paces the player. "Beat" is the retired name. Owner rulings
  on #1349. Each tactic flies a law the archetype pilots already have:
  - *attack run*: hold at the gun hold range plus the temper's attack-run margin
    and fire (the Aggressor's law);
  - *flank*: circle toward the target's open side, firing once my firing lane
    clears;
  - *stand off*: hold just outside the target's real primary gun range, firing
    once it turns away and my firing lane is clear;
  - *fall back*: hold near the far edge of my own primary gun range, never
    closer than my attack run, firing while my firing lane is clear;
  - *break off*: flee with guns cold;
  - *charge*: hold at the temper's charge range, point-blank, and fire
    whatever the ship's shield. Only a last stand flies it.
  - *ambush*: fire the keystone when it is ready, fly hidden with guns cold to a
    point ahead of the target's nose, and fire once the ship shows. It comes
    first after the wounded rules.
  - *hold the line*: hold a place on the leader's line, across the line to the
    target, while the leader is there. It comes after ambush and before flank
    or stand off.

  A ship picks by *ordered rules*, the first rule that fires winning: the
  tactic its *wounded response* names while it is wounded (break off, fall
  back or charge), then ambush, then hold the line, then flank or stand off, then its *home tactic*, the tactic it flies when no
  rule overrides it. It keeps a tactic until the reason it was picked ends, and
  never for less than the *minimum hold*, break off included. A home tactic also
  holds while the rules still pick it.
  Only a pilot authored with tactics settings (the *tactics switch*) picks one.
  Every shipped pilot does, each on its own *temper*; the matrix lane's
  weapon-pair shooters never do. *(TacticRules.Next, TacticsSettings)*
- **temper**. One `TacticsSettings` asset per pilot. A faction's ranks fly
  the pilots its `Faction` asset names (`Faction.Rival`, `Faction.Minion`), so a
  temper belongs to a faction and a rank: Shoal's Valis and minion, Veil's
  Nightshade and minion, Bastion's Bulwark and minion. Hunt patrols fly the
  minion's pilot. Both of a faction's
  tempers carry the faction's wounded response, ruled on #1589, 2026-11-16:
  Veil never breaks, Shoal jumps out, and Bastion makes a last stand. On the
  same ruling the Veil rival ambushes and Bastion minions hold the line, and a
  minion whose leader is lost flies its wounded response. Veil minions *regroup*
  instead (arye, 2026-11-23). The
  elite and its escorts keep their own pilots. The owner ruled on #1349,
  2026-11-09, that the elite never breaks off and always presses (never
  wounded, nerve 0%) and escorts never fly into the target's guns (nerve
  100%). On #1556, 2026-11-13, the owner left the elite and escort numbers as
  they were. At 100% nerve a ship shies at a full shield too, so 0% and 100%
  hold at every shield level. The temper also records whether the ship circles
  the wreck before it wanders. A pilot with a temper never reads `archetype`.
  *(Faction.Rival, Faction.Minion; Settings/AI/Tactics/Factions; EliteTemper, EscortTemper)*
- **leader**. The ship a minion flies with: its wave's rival, or the rival or elite its reinforcement joined. The minion observes that
  ship's position and velocity and nothing of its brain, and the leader never commands it.
  When the leader is gone, the minion flies its wounded response, or regroups.
  *(AIContext.Leader, AICommander.FollowLeader)*
- **regroup**. What a minion whose temper says so does when its leader dies: it
  flees with guns cold and leaves past the temper's jump distance, and the wave
  director sends it again with the sector's next rival's wave (never with a
  reinforcement), on top of that wave's drawn minions and still under the minion
  cap. Unlike a jump out it comes back. Held
  minions are lost when the sector ends. Only Veil minions regroup: the Veil
  can't cloak a minion, so they withdraw in plain sight.
  *(TacticsSettings.regroupsWithoutLeader, WaveDirector.Rejoined)*
- **place**. A minion's signed position along its leader's line, counted in
  line spacings (..., -2, -1, +1, +2, ...). *(AIContext.Place, AICommander.FollowLeader)*
- **wounded response**. What a temper says its ships do when hurt: never
  retreat, break off, fall back, *turn cautious*, make a *last stand*, or *jump
  out*. Turn cautious adds no tactic and reads the ship's nerve as 100%, so the
  flank-or-stand-off rule keeps it out of the target's guns. A last stand
  charges until the ship dies. A jump out breaks off for good, and once the ship
  is farther from its target than the temper's jump distance it asks its ship
  to jump; the spawner that made the ship removes it, so no kill counts. A ship
  is *wounded* from the hit that finds its trigger gauge, shield or hull, under
  its temper's point, until its temper's end condition: the shield back to a
  level, or a number of seconds. A last stand and a jump out never end. A
  retreat is spent, and its seconds start, when the ship flies it: a held flank
  or stand off delays it, because a tactic holds until its reason ends (#1349).
  Every ship of one temper breaks at the same numbers. The owner ruled on
  #1589, 2026-11-16, that no per-ship draw decides them, which replaced #1579's
  draws. A temper that *commits* is never wounded again once a ship has spent
  its retreats. #1579's rulings, 2026-11-15, still set the role tempers: the
  Aggressor and the elite never retreat, the Orbiter turns cautious, the Kiter
  falls back, the Lurker breaks off once on hull, and the escort breaks off.
  Fall back leaves the 2026-11-09 hold-fire ruling on stand off as it was.
  *(WoundedRules, TacticsSettings)*
- **limp**. An enemy ship low on hull flies slower and never boosts. Its pilot
  plans and commands as if its ship were slower; nothing clamps the ship's
  velocity, and the player's ship never limps. The owner ruled on #1589,
  2026-11-16, that the limp comes from damage, not from the decision to run, and
  that it adds no smoke or sputter of its own. *(AICommander's limp settings,
  LimpingShip)*
- **situation**. What one ship reads about its fight before it picks a tactic,
  from its own sensors only. The owner's picks on #1349: allies count by where
  they are, never by asking another ship's brain, so the elite counts whatever it
  flies; *exposed* means inside the target's real primary gun range and its
  exposure cone, never a fixed distance; the hit clock on `AIContext` is the only
  state kept between reads. Not the retired *exposure arc*. *(Situation.Read)*
- **presentation** — the per-session axis deciding whether visuals and audio
  exist. Two things a reader needs: it is applied by the owning spawn seams,
  never by per-component globals; and it is **not** the same axis as the deleted
  `vfx` flag, which it has been confused with three separate times.
- **chassis / module / loadout / hangar / sidegrade** — the ship-customization
  naming key, in order of containment: the hull's own stats / the parts a ship
  carries (engine and shield come with the chassis; only weapons swap) / the
  equipped set / the between-run screen where you change it / an option that
  trades rather than upgrades.
- **weapon mod** — a run-scoped stat modifier on one field a carried weapon
  already has (fire rate, damage, heat capacity, cooling, magazine, reload
  speed, charge speed, blast radius), never a new behaviour. Mods ride the run,
  not the weapon: a picked mod lands on every carried weapon it fits, stacks
  with repeats, and one that fits nothing carried stays held and idle. The
  authored prefab numbers never change, so enemies and the hangar read the
  weapon unmodded. Not a ship module. *(WeaponMod, WeaponMods; held on the
  LullModStack)*
- **ship mod** — a run-scoped stat modifier on one field group of the player's
  own hull, engine or shield (max hull; max speed; thrust; handling; shield
  capacity, regen, recharge), grouped as the keystone scales group handling. It
  folds into the ship's own resolve and applies at once with no heal: a max
  rises and keeps its fraction. Only a bound stack mods a ship, so enemies and
  the hangar read authored. Sibling of a weapon mod; not a ship module.
  *(ShipMod, ShipMods, ResolvedShipStats.Resolve)*
- **lull mod** — a weapon mod or a ship mod: what the lull pool holds and a
  lull pick offers. A run's picks ride one stack the player rig binds to its
  ship once; the ship re-reads it on every resolve and on every change.
  *(LullMod, LullModPool, LullModStack, Ship.BindMods)*
- **lull pick** — the time-stopped choice of one lull mod from up to three,
  or a skip, when the player claims a *mod station*; the clock runs on after
  it. *(LullPickScreen, GameHost)*
- **lull beacon** — the zone the sector spine places on a clear point away from
  the hero shortly before a sector phase that a lull follows ends, so its
  marker shows during the fight; flying into it claims it. It
  stays through the lull and a grace window into the next sector phase, then
  closes unclaimed; a new beacon's opening closes the one
  before it, so beacons never stack. The lull before the elite gets one too,
  and that one stays its grace into the elite fight. The sector's opening lull
  gets none.
  *(PhaseSpineModule, ExtractionZone)*
- **weapon pick** — the *armory*'s choice of one weapon or ordnance the player
  does not carry, from up to three, or a skip; the pick replaces the mount in the
  slot its kind fills. The clock stops while it is up. The swap keeps hull,
  shield and the run clocks, and the hangar's kit stays the next run's.
  *(WeaponPickScreen, PlayerRig.SwapWeapon)*
- **keystone** — the locked ability in a ship's keystone slot, one kit per ship
  (`Keystone`; `Dash` today). Not a ship module: a kit prefab the ship
  instantiates, because a kit keeps per-ship runtime state (its cooldown clock,
  later its windows) that a shared module asset cannot. The boost names stay —
  `PilotCommand.boost`, `IShipStatus.BoostAvailable` — and mean "activate the
  keystone". Always "keystone slot", never bare "slot". *(Keystone, Ship)*
- **tell** — the player-visible cue for a keystone's ready, fired or active
  state. A prerequisite, not polish: any keystone state the AI may read needs a
  player-readable counterpart (#488 §A8). It rides the kit prefab as a
  presentation part, so presentation-off sessions drop it. *(KeystoneFlash,
  HullVisuals.HoldFlash)*
- **ready flash** — the HUD's brief brighten-and-fade over a weapon's arc when
  the weapon recovers: an overheat lockout ends, a reload lands, a round comes
  back, the railgun's cooldown ends. The HUD flashes on the domain's
  `IRecoveryReadout.Recovered` and never reads thresholds itself. Its sound is
  still to come (#1173). *(WeaponResourceHud, ReadyFlashProfile)*
- **trauma**. The flight camera's shake level, from 0 to 1. Hits the subject
  takes and its death add to it, and it drains at a steady rate. The view shakes
  by trauma raised to a power, so at the default power of 1.5, half the trauma
  gives about a third of the shake. The term
  comes from Squirrel Eiserloh's GDC 2016 camera talk (#1636). Only the world
  shake reads it; the canopy's damage shudder reads *hit size*.
  *(ObserverCam, FlightCameraProfile)*
- **hit size**. How hard one hit lands on its ship, from 0 to 1. The world
  shake and the canopy's damage shudder both scale by it, so a graze moves
  both less than a big hit. *(AppliedDamage.HitSize)*
- **shake profile**. One source's shake: an envelope, a frequency, how far it
  jitters, rolls and kicks, and how hard it glitches the HUD on raise and on
  release. The source's prefab references it, so adding a source changes no
  camera or HUD code. Only shakes on your ship's shake channel glitch the HUD;
  a nearby blast ignores its profile's glitch amounts. *(CameraShakeProfile)*
- **shake envelope**. A shake profile's strength over seconds, from 0 to 1. One
  shake envelope scales the jitter, the roll and the kick together. A *sustain
  point* on it holds the strength until the source releases the shake, then
  plays the rest. Overdrive's buzz holds this way while its part scales the
  strength by the ship's speed (`KeystoneSpeedShake`). Always "shake
  envelope": bare "envelope" is the firing envelope.
  *(CameraShakeProfile.envelope, sustainAt)*
- **shake layer**. One running shake on the flight camera, one per source; a
  new raise from the same source restarts it. Shake layers add to the trauma
  shake, and `shakeOffsetCap` and `shakeRollCap` clamp the total. While the
  game clock is stopped they leave the view alone. *(ShakeLayers)*
- **shake channel**. The events a ship raises its own shakes on. Its kit and
  weapon parts raise them. The flight camera subscribes only to its subject's
  and the canopy only to your ship's, so an enemy's keystone never shakes your
  view or glitches your HUD. Always "shake
  channel": the lock channel and the shake noise channels share the word.
  *(Ship.Shake, ShakeChannel)*
- **shake listener**. The one flight camera registered to hear nearby blasts.
  It measures each blast's distance from its subject and starts a shake layer
  scaled by that distance. A process-wide registry holds it. Wiring rule 3
  calls that suspect, and the owner accepted it for presentation only (#1643).
  Registering a second one throws. Always "shake listener": the audio side
  has `CameraListener`. *(ShakeListener, ObserverCam)*
- **shake emitter**. A blast prefab's part that passes each blast's position
  to the shake listener. It sits beside the prefab's audio part and subscribes
  to the same event. Always "shake emitter": particle systems have emitters.
  *(GrenadeShake, MissileShake,
  RailgunImpactShake, AsteroidShake)*
- **item** — anything that fills a loadout slot: a chassis (a prefab with a root
  `Ship`) or a weapon (a prefab with a root `WeaponComponent`). Engine and shield
  modules come with the chassis and fill no slot (#1073, #1254); projectiles,
  trial pacing and the kill refills are tuned but fill no slot either. None of
  those are items.
- **item catalog** — the one asset listing every item in the project, grouped
  by item type. An index only: each item's stats stay on its own asset. Listing
  is not what makes something an item — a test scans the project by type and
  fails when the list and the scan differ. *(ItemCatalog)*
- **item subset** — an authored list of items one consumer chooses from, per
  loadout slot. One exists: the hangar's offer (ships and weapons). Enemies draw
  nothing: each flies its faction kit. Never "the catalog": it doesn't list
  everything. *(ItemSubset)*
- **hangar's offer** — the item subset the hangar shows the player.
  *(GameHost.hangarOffer)*
- **lane clearing** — shooting asteroids to open a firing lane. Currently
  inexpressible: the firing-envelope check vetoes it, so
  asteroids act as walls.
- **broadphase scalar** — an asteroid's single-number size: the radius of a
  sphere with its baked volume. It answers a *bulk/extent* question only —
  cheap-collider sizing, field packing, the obstacle-scan reach cull — and is
  never the rock's shape. Shape is the baked **lobes** (`MeshInfo.cachedLobes`,
  1–3 covering spheres), which the MPC consumes directly. Deriving the scalar
  from volume rather than measuring the render mesh is what lets the runtime
  read no mesh vertices at all.
  *(AsteroidGeometry.RadiusFromVolume, AsteroidGeometryBuildGate)*
- **bleed-through** — letting a damage remainder cross a shield break into hull.
  The live rule since the §C3 overkill PR; the old discard rule was a hidden
  alpha-weapon tax.
- **overkill** — the damage a hit has left after killing its target.
  `TakeDamage` returns it so the railgun beam can carry it to the next target
  behind. It is not bleed-through. The "§C3 overkill PR" above used the word
  for the shield-to-hull remainder on a single target.
  *(`IDamageable.TakeDamage`)*
- **shield bypass**. A hit's damage skips the shield and goes straight to hull.
  Only a missile's direct hit carries it; its splash stays shield-first with
  bleed-through. The producer sets the flag, and `DamageController` routes on it,
  never on `DamageKind`. A bypass hit doesn't restart the shield's regen delay.
  *(`DamageInfo.BypassesShield`)*
- **DamageInfo**. The per-hit context struct every damage producer builds at
  its call site. Non-obvious: producer-side `Amount` is the *incoming* damage,
  `AppliedDamage.Info.Amount` is the *applied* damage (shield + hull, the locked bleed-through
  reading); `AttackerId` is `ShipId.Invalid` when no ship caused the hit;
  `OnDeath` is latched to fire once per life. *(DamageInfo, DamageController)*
- **damage ledger**. Records received shield and hull damage across the whole run,
  including repairs and sector changes. A consumer-side recorder owned by the
  player rig, never sim state. Source names
  are captured at event time because the attacker may despawn before the recap
  reads the row. *(DamageLedger)*
- **run tally** — kills and time survived for one run: a consumer-side recorder
  on the player rig beside the damage ledger, never sim state. A kill is a death
  whose killing blow came from the current player (id read at event time — the
  hangar can rebuild the player); only deaths between the game host's clock
  stamps count (begin after the run's first sector load, end at player death or
  the last extraction). Raises
  `Killed` with the victim on each counted kill. Reset at each run's loadout step. *(RunTally)*
- **kill refill** — the hull points the game host restores to the player each
  time the run tally counts a kill, set by the victim's rank: the rival refill for
  a wave's rival or the elite, the minion refill for every other kill, hunt
  patrols and elite escorts included. Hull only (ammo or heat on a kill would be
  a reset button, and the shield already regens). Ranked by the game lead on
  2026-11-23: minions come back on every beat, so a large minion refill would
  make farming them beat fighting the rival. Flat points, not a share of max
  hull (same day), so a kill pays the same on every chassis. Repair stations and
  beacon caches still restore a share. Interactive sessions only — the harness
  has no game host.
  *(GameHost.minionKillHeal, GameHost.rivalKillHeal, WaveDirector.IsRival, SpawnLog.IsElite, Resource.Restore)*
- **loadout stat hash** — a short hash of the balance numbers one ship flies with:
  its hull, its engine, its shield and the weapon on each mount. Only fields marked
  `[Stat]` count. The same parts with the same numbers give the same hash on any
  commit and any machine, and a spawned ship hashes like its prefab asset, so facts
  recorded about one loadout can be added up across runs. Two gotchas. Lines are
  keyed by asset name and field name, so renaming a marked field, or the asset it
  sits on, changes the hash with no number changed. A mount's numbers are read off
  the weapon prefab, so an inspector edit to the prefab or to a module asset shows
  up and an edit to a live weapon instance does not. *(StatHash.OfLoadout, StatAttribute)*
- **stat fingerprint** — a short hash of every balance number a run can draw from:
  what the hangar offers, the lull mod pool, each run sector's wave director in run order (its pacing and minion counts, and
  the parts its enemies draw from) and the kill refills. Runs played against the
  same numbers share a fingerprint, so results are grouped by it. A number no run
  can reach does not move it, and neither does list order or listing a part twice,
  except in a list marked `[Stat(Ordered = true)]` such as the sector phase table.
  Inside a list entry, order still does not move it, but a repeat does.
  Built from the `[Stat]` marks and keyed by name, so a rename moves it too.
  *(StatHash.OfSetting, StatAttribute)*
- **run record**. One JSON line appended when a run ends, extracted from its last
  sector, dead, or abandoned. Abandoned is the pause screen's quit run, or shutdown
  arriving while a run is open; it has no killing blow and no recap. What a line
  holds is `RunRecord`, and which schemas still read is `RunRecord.Counts`.
  Every record goes to one append-only file under the results root, which in the
  editor is the worktree's own `results/`, so an agent's editor runs never land in
  the user's file. A failed write, or an unreadable build identity, is logged and
  that run goes unrecorded. Rows name a ship by its position in the record's spawn
  list, never by instance id. *(RunRecord, RunRecordStore, GameHost.AppendRunRecord · #772)*
- **scoreboard**. Past-run rankings share one board across stat fingerprints.
  A sector counts only after extraction; dying in the first sector shows zero.
  Intermediate extraction has no scoreboard; victory and defeat do.
  An abandoned run stays off the board.
  *(Scoreboard, RunRecapScreen, GameHost)*
- **spawn log** — one entry per life of each ship spawned or sector-adopted in a
  run, other than the player (a respawn-policy revive opens a fresh entry): its
  parts by asset name, its loadout stat hash taken at spawn, when it spawned, how
  long it lived and whether the player's shot killed it (the run tally's rule). A
  consumer-side recorder on the player rig beside the damage ledger and the run
  tally, never sim state. It exists because damage kind cannot say which weapon
  fired: Lasers and Rippers both fire the `Laser` projectile. A ship
  placed during the sector load reads as spawned at second 0. *(SpawnLog)*
- **build identity** — what a run was played on: the commit, a dirty flag for any
  tracked change or untracked file under `src/Asteroids3D/`, and the build label.
  The commit and the dirty flag are the git side. The editor asks git at run end
  and leaves the label empty. A player build carries a file its pre-build hook
  stamped, and the game host throws at startup when that file is missing.
  Informational only: records are grouped by stat fingerprint and loadout stat
  hash. "Build" here is never a ship's parts, which are its loadout. *(BuildIdentity, BuildIdentityStamp)*
- **build label** — a short name the person starting a build gives it, such as
  `friends-1` or `itch-1.0`, so builds handed to testers can be told apart
  without reading commit hashes. Empty when the build was started without one.
  *(BuildIdentity.label, BuildIdentity.TryAcceptLabel)*
- **death recap** — the run outcome recap after a death: the HUD fades while the
  explosion finishes, then the banner and the breakdown card; presentation-gated, so a
  game host with presentation off goes straight to the unload.
  *(RunRecapScreen, GameHost.RunRecap)*
- **run moment stream** — one per run, created by the game host before the first
  sector loads and handed down through Initialize or a build context, never a
  static (owner ruling 2026-11-03 on #1321). Moments are data; the companion owns
  every line of text. *(RunMomentStream, IRunMoment)*
- **line sheet** — the companion's line slots, one per moment, and the *line file*
  its lines come from. A line slot with no matching line shows nothing in player
  builds; the editor and development builds show a bracketed debug label of its id
  and facts instead (owner ruling 2026-11-04).
  *(CompanionLineSheet, CompanionVoice.DebugLabel)*
- **line file** — the markdown file that holds every line: the Line sheet tab of
  the owner's lines doc, exported. The owner and an agent edit the doc together
  (owner ruling 2026-11-21, replacing the 2026-11-03 ruling that agents write no
  lines). Edits flow one way, from the doc to the file, so a hand edit to the
  file is lost at the next export. Only rows whose Status is "In game" load.
  The repo copy is named `.txt`, not `.md`: the merge gate treats every `.md`
  change as unable to change behavior and lands it with no test run, and a bad
  row here stops the game at boot (owner ruling 2026-11-21).
  *(LineTable, CompanionLineSheet.LoadLines)*
- **exchange** — what one moment speaks: an opening line and, some of the time,
  a reply from another speaker when the opener's hold ends. The two are one
  turn against the *nag budget* (owner ruling 2026-11-21): the reply skips the
  gap, and the gap then counts from the reply. *(LineTable.Pick, NagBudget)*
- **nag budget** — the companion speaks at most one *exchange* per interval;
  must-say line slots skip the wait, and a line that cannot show before it goes
  stale is dropped, never queued. *(NagBudget, CompanionFeelProfile)*
- **presentation module** — a presentation (the recap outcome sequence first)
  exposed as a pure function of a *feel profile*, an immutable *context* and a
  time, rendered by one renderer whichever host drives the time; a
  preview-only renderer is forbidden. Arc #1151. *(RecapOutcomeSequence,
  RecapOutcomeContext, RecapPresenter)*
- **feel profile** — the ScriptableObject of named knobs a presentation module
  reads live; the game serializes the asset on its host, and the preview's panel
  edits that same asset in place under a snapshot: Save writes it, Discard
  restores it. *(RecapFeelProfile, FeelSnapshot)*
- **death feel profile** — the feel profile every ship's death reads: its debris
  explosion and debris pace. The ship rig serializes it, not the game host.
  *(DeathFeelProfile, ShipDeathTarget)*
- **preview host** — the play-mode composition root beside the game host. It
  runs one entry at a time. A *preview Replay* entry composes a real session
  with presentation on around the real player rig, camera and HUD, and hosts a
  *preview subject* under a transport (play, pause, scrub, loop, speed). A
  *preview Playtest* entry runs the shipped game host prefab. The host knows
  entries and never *tuning targets*. The control panel is an editor window and
  nothing more. *(PreviewHost, PreviewEntry, PreviewTransport,
  PreviewControlPanel)*
- **tuning target** — one thing the owner tunes in the preview panel, such as
  the sector jump, the run recap or the HUD. It names its feel profiles and the
  preview entries that show them, and the panel draws only its knobs. Targets
  are editor code only: game code, the preview host included, never learns they
  exist (owner ruling 2026-10-30). Switching to a target that offers the running
  entry keeps the world; otherwise the preview rebuilds to the target's top
  entry. What makes something tunable, and how to add a target:
  `doc/agents/tuning.md`. *(TuningTarget, HudTarget)*
- **preview Replay** — the panel section and kind of preview entry that hosts
  a *preview subject* under the transport: a moment the owner judges. The recap
  and the jump scrub to any instant. The HUD replays and Enemy death play from
  their start, and Restart or Loop stages them again from scratch. Bare "Replay"
  is legal in preview context. (Retired form: the preview host's "Inspection"
  mode.) *(PreviewEntry.RunRecap, PreviewEntry.SectorJump, HudReplaySubject,
  EnemyDeathSubject)*
- **preview Playtest** — the panel section and kind of preview entry that runs
  the shipped game host prefab for the owner to fly, with preview run records
  and at most swapped rows. A picked sector flies its whole run row, and a
  fixture flies the row its fixture link resolves to. Bare "Playtest" is legal in preview context;
  elsewhere "playtest" is the owner playing the game.
  *(PreviewEntry.Combat, PreviewEntry.CrowdedJumpGate, PreviewEntry.Boss)*
- **fixture link** — a preview fixture's pick of one row of the game's run, by
  position. Reordering the run's rows moves every link with the positions, and
  an unticked field reads its linked row at each compose. *(`PreviewFixture`)*
- **preview subject** — a presentation module with a chosen context (a fixture
  built from constants, or any context a scenario builds), hosted by the preview
  host. *(IPreviewSubject, RecapPreviewSubject, RecapFixtures)*
- **staged moment** — a game state a preview subject reaches live through its
  real cause, never through a setter on a runtime component, so the host can
  show it and a capture scenario can film it. The recap stages the player's
  death at the context's death point. The HUD replays stage a hit and hull
  brought low through the damage path, a run's start in the game host's order,
  and extraction opened by a fixture sector's own spine. *(StagedMoments)*
- **gizmo capture profile** — the named set of Unity component types a capture
  selects for drawing, chosen by `RL_HARNESS_GIZMOS`. Code-defined only: there is
  no per-diagnostic selection grammar, because Unity's own per-component-type
  Gizmos controls are the authority. Selecting any profile films with
  presentation off — collider silhouettes and gizmo geometry are the footage;
  selecting none films plain gameplay with presentation on.
  *(GizmoCaptureProfile, GizmoCaptureProfiles)*
- **gizmo subview** — one registered gizmo toggle row in the Gizmo View window.
  Drawers gate each on the window's flag + scope predicate instead of
  `GizmoType.Selected`. Distinct from a *gizmo capture profile* (the headless
  harness-capture selection): subviews are interactive-editing state, in EditorPrefs,
  never committed. *(GizmoView, GizmoViewWindow)*

### Art pipeline

- **ship art stage** (1–8) — one of the eight owner-approved steps from concept to
  lineup review; each starts only after the owner approves the one before. Written
  "ship art stage 4" or by name ("the flight check"). *(`ship-art-pipeline` skill)*
- **anatomy** — the fixed set of named child slots (anatomy slots) every chassis prefab
  has; a hull fills them and never restructures them. *(ShipAnatomyEditModeTests)*
- **socket** — a named empty transform the hull places (engine emitters, hardpoints), so
  hull geometry, not the base, positions what mounts there. *(the anatomy's `Sockets`;
  `role.sockets` in art/tools/ship/README.md)*
- **role collection** — a Blender collection named for what its contents export as; a
  part in none is not exported. *(art/tools/ship/README.md)*
- **geometry lock** — the geometry fingerprint written at flight-check approval;
  `verify` fails on changed geometry, and only a reopen the owner approves moves it,
  reporting what it invalidates. *(art/tools/ship/ship_lock.py)*
- **legacy list** — the ship names not yet migrated; each array exempts only from its
  own check (`doc/agents/art-pipeline.md` → Legacy list). It only shrinks: each
  migration deletes one, and the arc closes when it is empty. *(ShipLegacyList)*
- **name mirror** — one ship name across the chassis prefab, the visuals folder and the
  art folder (lowercased). *(doc/agents/art-pipeline.md)*
- **palette region** — a material slot on a visual-role part whose colour every palette
  in `palettes.json` names; the unit of recolouring. *(art/tools/ship/README.md → Paint
  settings)*
- **paint mask** — Shadow, Light or Ink (Emission for painted indicators): a grayscale
  image on `PaintUV` the drawn-surface shader applies over the region colour. Never
  "paint layer": *layer* is a collision word; `layers/` is only the folder and
  `_PaintLayers` only the shader toggle. *(ship_paint.py; DrawnSurface.shader)*
- **plate** — one connected up-or-down-facing face group of a part (|n·z| ≥
  `plates.cap_facing`), with its own boundary; the unit the diagonal sweep and the inset
  ink line are drawn on. *(ship_paint.Part)*
- **mask baseline** — `layers/baseline/`, the paint tool's own last output, kept so a
  re-run can separate the owner's edits (mask minus baseline) and carry them onto the
  new generation. *(ship_paint.py)*

### Infra & tooling

- **coordinator** — `unity_access.ps1`, the machine-wide Unity access broker. A
  new caller goes through the coordinator; generalize the primitive, never bypass
  it. *(AGENTS.md wiring §6)*
- **editor profile / profile receipt** — a coordinator-selected, launch-scoped
  Unity quality tier (`LowMemory` → `Performant`, `HighFidelity` → `High
  Fidelity`) / the bootstrap's atomic record of requested-vs-observed values.
  The coordinator verifies the receipt before handing the editor off, so a
  mismatch fails the start rather than downgrading silently.
  *(unity_access.ps1, PR #457)*
- **producer-owns-outputs** — when one tool's output is another's input, the
  location and format are the producer's contract; consumers never re-derive
  paths. *(AGENTS.md §6 corollary)*
- **player-build tripwire** — merge-gate EditMode lints re-stating what the
  RLTraining player build needs (asmdef reference closure, scene-script
  survival, hydrated LFS meshes) without building one — the merge gate never
  builds a player, so player-only breaks are otherwise invisible (#185, #251).
  *(PlayerBuildTripwireEditModeTests)*
- **lane launcher** — the Python library that composes a lane's env, runs the
  batch child through the coordinator, and reads artifacts back from the dir it
  named. *(eval_lane.py)*
- **owner lease / boot lane / two-tier lock** — a per-project run claim / the
  machine-wide startup-only lock / their combination. Pid-backed leases survive
  TTL; pid-less ones expire and orphan live editors. A **wedged** boot lane — an
  unowned boot dir the cleanup cannot delete — reports `boot_lane_wedged`
  (exit 25), never free.
- **memory admission** — the coordinator's yes/no on whether a Unity boot may
  start, given memory. Asked read-only through `-Action BootAdmission`, enforced
  when the boot lane is granted; the refusal is `boot_refused_low_memory`
  (exit 28), and `-AllowLowMemory` overrides it for one invocation, only after
  the user approved that specific boot.
  *(Get-BootAdmission, scripts/unity_access.ps1)*
- **commit headroom** — commit limit minus commit charge: what a dying Unity
  boot runs out of, and the only reading memory admission blocks on. Available
  physical RAM rides along in the verdict and never refuses a boot.
- **coordinator Adopt** — seize an untracked live Unity process into fresh
  ownership. Refuses the user's hand-opened editor.
- **tracked vs user editor** — coordinator-owned versus user-owned. A user editor
  is never terminated, only asked about.
- **single-boot mode** — running EditMode and PlayMode in one editor boot
  (`-Mode Both`). It is a **mode, not a pass/fail check** — which is why the old
  name was wrong. (Retired: "single-boot gate".)
- **watchdog note field** — a non-empty `note` in the test summary means the
  editor hung; empty means healthy.
- **scratch → promote → park** — gitignored investigation code → committed →
  archived. Standing rule: **probes that live only as patches do not exist.**
- **staging** — *retired 2026-10-03*: copying checkpoints and exes into the
  primary tree so they survive slot recycling. The primary tree takes no agent
  writes now; the RL tools' staging comments predate that, and where durable RL
  artifacts live is open on #883 (an orphan branch is favoured).
- **define churn** — *retired 2026-10-28*: Sentis flipping its analytics define
  between batch and interactive editor loads. The committed define pair ended it
  (`doc/agents/environment.md` → The committed Sentis defines).
- **orphan discipline** — killed monitors leave `tail.exe`/`grep.exe` holding
  logs (WinError 32). Taskkill by PID, never by image name.
- **FMOD stub** — `src/fmod-stub/`: the package the hosted suite compiles
  against in place of FMOD for Unity, which only the local vendor tarball
  supplies. Only the FMOD types the game touches, every method a no-op; a
  prefab-visible component keeps FMOD's real script GUID. It grows one member
  at a time when the hosted suite fails to compile, never ahead of use.
  *(doc/agents/audio-pipeline.md; #987 ruling 2)*

---

## 3. Deprecated forms

| Retired | Use instead |
|---|---|
| council, agent council, agent counsel | **counsel** |
| yaw-thrash | **facing thrash** (symptom) or **command churn** (cause) |
| pursuit gap, pursuit ceiling, pursuit-gradient hole | **Evader hole** (deficit) or **pursuit gradient** (mechanism) |
| entry gate | **entry bar** |
| single-boot gate | **single-boot mode** |
| RAM floor | **memory admission** (the verdict decides; no hand-evaluated floor) |
| main tree | **primary tree** (or prim tree / primary) |
| program, package, series (as a work grouping) | **arc**, **slice**, or **pass** |
| ARC COMPLETE | **SHIPPED** or **CLOSED** |
| PR-N as an identifier | **branch-style arc names** (`vocab-docfix-2`) — for new arcs only |
| shelve, shelved, shelf, unshelf (work set aside) | **benched** (waits on a dependency or knowledge) or **held** (slot work waiting on the user) |
| parked, for slot work waiting on the user | **held** |
| parked arc | **waiting arc** |
| ledger row, work ledger | *(retired 2026-09-22)* — in-flight state is the pool (`status`, dashboard) plus open PRs |
| Phase 0–N as a chapter scheme | **stage** (campaign chapter) or an arc **slice** |
| "Driver:" as a doc header | *(drop it — say what it motivates)* |
| drain run, drain task | *(retired 2026-10-01)* — the ready queue is built by a **cloud batch**; one item's build is a **cloud build** |
| decision inbox | *(retired 2026-10-04)* — open questions and PR waits show in the **decisions view**; `ready-for-human` keeps its label meaning |
