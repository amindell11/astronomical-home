# Glossary

> STATUS: living — shared vocabulary; updated in the same PR that coins or shifts a term.

Coined terms accumulate faster than shared definitions do. This file is the
authority for what this project's words mean, and the place to look when a term
reads like it might mean two things.

## How to use this

**Two standing rules.**

1. **Never bare — unless the row grants it.** Every word in the collision table
   carries a qualifier by default: "eval gate", "worktree slot", "command
   churn". Some rows name exactly one context where the bare word is legal
   (bare "envelope" = firing envelope, "harness" = RL harness, "seed" = RNG
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
  feels certain — "(taking 'gate' as the gate score here)".
- Genuine ambiguity, or a divergence that would change what happens next:
  **ask** rather than assume — "which roster, the training mixture or the eval
  instrument?"
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
| **gate** | merge gate · eval gate (`eval_gate.py`) · gate score · cost gate (fix-ladder rung 3) · go/no-go gate · curriculum lesson gate · anti-churn gate · scoping gate · "gated off" code conditionals | Always qualified. Bare "the gate" is legal only in pool-merge context (= merge gate) and RL-run context (= eval gate), and never in a title. |
| **lane** | boot lane · harness lane · curriculum lane · watch/capture lane · audit lane · teacher-tuning lane · access-queue lane · firing lane (lane clearing) | Always qualified. |
| **pool** | worktree pool · ship resource pool (`PoolDifferential`) · self-play snapshot pool · object pool (`SimplePool`) · Dev Pool board columns | Always qualified. |
| **token** | bus/signal token · obs obstacle token (`ObstacleTokenCap`) · threat token · LLM context token | Always qualified. |
| **slot** | worktree slot (`agent-N`) · weapon/mount slot · ONNX import slot · obs slot-block grammar · MPC terminal-cost slot | Qualify outside pool-loop context; bare "slot" = worktree slot in workflow text only. |
| **pin** | pin test (freeze a value) · pinned seeds/hypers · instance pinning (MCP) · ram-pin exploit | Qualify. "ram-pin" always hyphenated for the physics exploit. |
| **fixture** | NUnit test fixture · sector fixture · ONNX smoke/eval fixture | Always qualified — all three appear within a page of each other in the RL docs. |
| **seed** | RNG seed · `SeedScope` stream · eval seed set (2001+) · sealed held-out seeds (1001–1020) · seed checkpoint · `SeedMode.BorderEscape` | Bare "seed" = RNG seed. Checkpoints are "seed checkpoints". |
| **rung / tier** | fix-ladder rung · curriculum ladder · screening ladder (Tier 0–3) | "Rung" is fix-ladder-only. The curriculum has *lessons*; screening has *tiers*. |
| **archetype** | opponent archetype (Aggressor/Evader/Orbiter/Kiter/Dummy) · chassis archetype · ship-prefab archetype | Qualify — the first two both feed the matchup matrix. |
| **spine** | sector spine (`SectorSpineModule`) · reward spine (the sparse ±1 outcome term) | Qualify. |
| **adopt** | sector adoption (the placed object IS the runtime object) · coordinator Adopt (seize an orphan process) | Qualify: "sector-adopt" / "coordinator Adopt". |
| **authority** | facing authority (RL action semantics) · strafe/lateral authority (physics) · design authority (which doc rules) · merge authority (who may merge) | Always qualified. |
| **churn** | command churn (facing) · anti-churn gate (diff size) · define churn (Sentis) · churn-discard (bloat) · sort-churn | Always qualified. |
| **smoke** | `Smoke` NUnit category · `-ScopeType Smoke` · `run_smoke.py` / trainer smoke · smoke ONNX fixture · "50k smoke" run | Qualify. Smoke is a **ScopeType, never a Mode**. |
| **floor** | noise floor · characterization floor · curriculum floor (Dummy) · entropy floor · radius floor | Always qualified. |
| **mirror** | mirror match/league · mirrored second `EpisodeRunner` · eval-env mirror · yaml branch-tip mirror | Always qualified. |
| **driver** | Python drivers (`training/rl/`) · `GameDriver` · `RLDriver` · `EpisodeLoopDriver` | Qualify. "Driver:" is retired as a doc-header word. |
| **harness** | RL harness (`Game.RLHarness`) · determinism/sweep/ram-bench harness · test harness | Bare "harness" = RL harness; qualify the others. |
| **arc** | multi-PR work arc · enemy arc exposure (`ExposureCost`) | The work sense dominates; combat docs say "exposure arc". |
| **stage / phase** | see §2 → *stage*, *phase*, *tier*, *batch* — four schemes, each naming a different **kind** of sequence | Never a bare number: "stage (iii)", not "stage 3" or "phase 3". |
| **composition** | `IEpisodeComposition` · composition root (DI) · prefab-vs-runtime composition · capture-scene composition | Always qualified. |
| **envelope** | firing envelope · kinematic envelope · scan envelope · MPC travel envelope | Bare "envelope" = firing envelope; qualify the others. |
| **guard** | the prohibited runtime check (fix-ladder rung 5, pejorative) · a benign regression/test guard · infra guard | The pejorative sense wins in fix-ladder context. Tests say "regression test", not "guard". |
| **anchor** | `--initialize-from` checkpoint · field world anchor / null anchor · archive anchors (file locations) · arena root | Always qualified. |

---

## 2. Canonical terms

Format: **term** — definition. *(authority)*

### Workflow & process

- **fix ladder** — the five-rung classification for programmer-error fixes, one
  rung per fix: unrepresentable → earliest deterministic failure → cost gate →
  loud failure → guards (prohibited). *(CLAUDE.md § Fix ladder)*
- **operating vs programmer error** — the triage above the ladder: untrusted
  boundary input is parsed once at the boundary; our own invariant violations
  climb the ladder. *(CLAUDE.md)*
- **cost gate** — fix-ladder rung 3: the structural fix exceeds the current
  scope, so stop and present narrow-vs-structural to the user. Never downgrade
  silently.
- **guard** (pejorative) — a check that absorbs a bad state and keeps running;
  prohibited for programmer errors. Log-and-continue is a guard wearing a costume.
- **scope conservation** — the confirmed scope bounds the *diff*, not just the
  intent; re-read the diff against it before submit. It never licenses violating
  a design value to touch fewer files. *(CLAUDE.md)*
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
  assumptions + blindsider resolutions. *(doc/Feature_Plans/)*
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
- **arc** — a multi-PR narrative with a declared end.
- **slice** — a sub-unit of an arc, each getting its own short pr-prep.
- **pass** — a bounded one-shot sweep with no successor (hygiene pass, texture
  pass). Retired for this sense: "program", "package", "series".
- **arc & PR naming** — arcs and slices carry descriptive, branch-style names
  (`vocab`, `vocab-docfix`); the PR branch is that path (`task/vocab-seed`). A
  **positional number appears only at the leaf**, only when one named unit spans
  several PRs (`vocab-docfix-1..3`), and only once you are building it — plans
  hold names, never numbers. Once a PR merges, its number is a historical fact
  and never renumbers. Max three levels; a fourth means you have two arcs.
  (Retired: "PR-N" as an identifier. Arcs numbered before 2026-07-29 keep their
  numbers — nothing is retrofitted.)
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
  vocab ratchet.
- **rescue sweep** — salvaging valuable strays (scratch probes, orphaned docs)
  into an infra-hygiene PR rather than losing them to a slot reset.
- **three tracking surfaces** — board = what / for-when (title-only cards);
  memory = why / how; ledger = right-now claims. Never conflate. *(AGENTS.md)*
- **parking lot** — deferred *discussion* items, not work items; add on park,
  delete on resolution. *(memory)*
- **handoff** — a memory brief a fresh session reads cold to take over.
  Explicitly not `/compact`; the consuming session deletes it.
- **ledger row** — a live claim on in-flight work. A row is deleted when its
  claim no longer holds — merged, abandoned, or superseded. The ledger is not a
  history.
- **slot / pool / lease** (workflow senses) — a pooled `agent-N` worktree / the
  pool machinery / the durable claim on a slot. *(agent_worktree_pool.sh)*
- **warm** (slot) — its Unity Library is already built; a reason to name a slot
  on acquire instead of auto-picking.
- **primary tree** — `D:/amind/git/astronomical-home`, as against the `agent-N`
  slots; canonical home for pool state, built exes, and staged checkpoints.
  Short forms: **prim tree**, **primary**. "Main" is exclusively the git branch.
- **merge gate** — the full-suite test gate inside `merge <slot>`; the only
  sanctioned merge path.
- **merge-grade proof / tested-tree proof** — a recorded tree hash from a green
  full run. Scoped runs never produce one.
- **inert diff** — a behaviour-neutral delta (docs-only, comment-only) that
  extends existing proof without a fresh run.
- **consent / merge instruction** — an explicit "merge it". Praise is not
  consent, and approval binds the tree at consent-time HEAD.
- **spend** — compute expenditure needing its own explicit approval; a run is a
  run, not a PR.
- **disposition table** — the per-review-round table, one row per comment:
  Fixed (rung N) / Rebutted / Deferred.
- **chunk-down** — replacing a class of remembered failures with a deterministic
  tool ("preflight, don't remember"). *(postmortem)*

### RL & training

- **league** — the self-play opponent population. Mirror league = own snapshots
  only; hybrid league = a per-worker split of scripted roster and snapshots.
- **roster** — the weighted scripted-opponent mixture. It is simultaneously a
  training mixture, an eval instrument, and a tuning lever — always say which.
- **opponent archetype** — a scripted opponent held to exactly one job, so a
  per-archetype result isolates one capability. The constraint is the point; the
  cast list is in the code. *(OpponentRoster.cs)*
- **teacher** — an archetype in its pedagogical role; the scripted roster as
  curriculum bootstrap.
- **hole** — a per-archetype capability deficit. **Evader hole** is the specific,
  long-running instance: the policy's inability to beat the Evader. (Retired:
  "pursuit gap", "pursuit ceiling", "pursuit-gradient hole".)
- **pursuit gradient** — the learning signal that closes a pursuit hole; the
  mechanism, as against the deficit it fixes.
- **mirror-brawlers** — the stage-(iii) diagnosis in one phrase: a league of
  mirror-brawlers has no pursuit gradient, because every opponent already wants
  the same close-range fight the policy does.
- **eval gate** — the deterministic scripted eval run per checkpoint, as a
  **sidecar**: it reports and does not kill the trainer unless explicitly armed
  to. Treating it as an automatic stop is the recurring misread. *(eval_gate.py)*
- **gate score** — the eval total (5 archetypes × 15 = X/75; older runs X/60).
  Not comparable across a rules change.
- **noise floor** — eval re-run variance, and ⚠ **currently unknown**. The
  quoted ±4/75 is an n=2 estimate. The 16-point span (58–74) across Phase B's 14
  checkpoints is *not* a substitute for it: no checkpoint has been evaluated more
  than twice, so policy variance and eval variance are confounded in that range.
  Treat both figures as provisional until a checkpoint is replicated, and do not
  size an effect against either. *(RL_Infra_Paydown_Pass.md, finding 1)*
- **ELO treadmill** — snapshots inherit `current_elo`, so ELO measures
  improvement against recent selves and cannot see absolute capability loss. The
  gate score is the absolute yardstick.
- **erosion** — self-play destroying a previously-earned capability while ELO rises.
- **reality check** — the scheduled per-archetype scripted eval during self-play,
  which exists because ELO structurally cannot see erosion.
- **blended-metrics ban** — no aggregate win rate in any summary; per-archetype
  only. All three stage-(ii) defects were invisible in the mean.
- **pause-eval** — stop the trainer at a checkpoint export, run the deterministic
  eval, `--resume` losslessly. *(runbook)*
- **scorecard / tripwire** — per-archetype W/L/D plus behavior metrics / the
  subset watched purely as a collapse detector.
- **command churn** — commanded facing movement per decision (measured 48°)
  exceeding the **slew budget** (yaw rate × decision period = 36°/decision). The
  cause.
- **facing thrash** — the visible symptom command churn produces. (Retired:
  "yaw-thrash". Not to be confused with **twitch**, reserved for the MPC
  obstacle×tactical defect, or **chatter**, a metric that provably does not
  capture thrash.)
- **facing authority** — the policy's way of saying "facing doesn't matter right
  now", by scaling down the MPC facing cost. It changed the **action semantics**,
  so checkpoints from before it cannot warm-start across the boundary.
- **shaping / Φ / undiscounted telescoping** — potential shaping deliberately
  telescoped *undiscounted* (Φ′ − Φ), trading Ng policy-invariance for a pursuit
  gradient. The discounted form leaks a per-decision drain that pays the agent to
  disengage. *(PotentialShaping.cs)*
- **dummy ignition** — weighting the passive Dummy high early so sparse reward
  ever fires; proven necessary.
- **curriculum lane / lesson** — one env-param's schedule track / one step on it.
  Env lanes gate on `measure: progress`, a fraction of `max_steps` — rescale when
  bumping `max_steps`.
- **boot-frozen composition** — a worker's episode composition (scripted vs
  mirror) is decided at boot and never changes mid-run; mid-run flips wedge all
  workers (observed twice).
- **worker / fleet** — one player process under `--num-envs` / the set of live
  training processes. Workers are addressed by index everywhere (ports, JSONL
  names), so "worker 3" is the same 3 across every surface.
- **screening ladder** — cheapest-falsifier-first evaluation of a rules change:
  Tier 0 arithmetic/scripted → Tier 1 frozen-policy A/B (screens, never
  certifies) → Tier 2 ~500k best-response probe → Tier 3 full retrain for
  finalists. Stop at the first tier that falsifies.
- **obs/policy surface** — the observation+action contract. Any change forces a
  full retrain; production and training share one spec.
- **seed checkpoint / warm start** — the `--initialize-from` origin. Distinct
  from an RNG seed.
- **seed sets** — three roles, do not mix them: training runs at **runSeed 1**
  (`EvalProtocol.TrainingRunSeed`); routine checkpoint eval defaults to
  **2001–2005** (`eval_gate.py`); **1001–1020 is the sealed held-out set** —
  never spent, reserved for one defensible final claim, and a knob change resets
  the protocol rather than re-opening it.
- **brawl** — the degenerate equilibrium where optimal range is provably ~0, so
  the game prices no decision. The motivation for the rules change.
- **passivity rot** — the policy ceasing to engage; visible only per-archetype.

### Game & sim

- **sector** — a bounded open-space field of POIs. The load-bearing decisions:
  it builds **deterministically** from a serialized manifest, and there is **one
  concrete class**, configured by prefab — variation never arrives as a subclass.
  *(Sector.cs)*
- **sector spine** — the sector's main objective chain; step names ARE the bus
  tokens (`spine:ready-to-extract`). Distinct from the **reward spine**, the
  sparse ±1 outcome term. *(SectorSpineModule.cs)*
- **activation rule / term / latch** — `predicate(terms) → effect`; terms are
  AND-ed, evaluated as a standing *level* rather than an enter-edge, and latched
  Active on first satisfaction. *(ActivationRule.cs)*
- **presence ≠ arming ≠ firing** — exists in world / trigger listening /
  predicate fired. "Present-but-inert" is the named middle state.
- **encounter** — the *fat* activation rule: rule + lazily-spawned content +
  local objective + on-complete events. A thin rule on a fixture is not an
  encounter.
- **sector fixture** — a world object present at spawn, sector-owned, independent
  of encounter state.
- **signal / SignalPort** — the bus coupling seam. ⚠ **Designed, not built** —
  publisher-owned ports are the intended replacement for free-string tokens, so
  a doc describing them is describing the plan; the code is still
  `ActivateOnToken`.
- **adopt vs spawn** (sector) — the placed child IS the runtime object, versus
  spawner-produced. Variation lives in the object or in the spawner type.
- **locale** — the per-sector environment *scene* (skybox, light, ambience).
  Environment is a scene; gameplay is a prefab.
- **GamePlane** — the frozen 2.5D convention. Production is `PlaneAxis.Z` (the XY
  plane); never reshape toward Y. *(GamePlane.cs)*
- **arena** — the RL isolation unit. Isolation is **by distance, not by scene**:
  arenas are offsets sharing one PhysicsScene, and **ghost rock** — cross-arena
  physical leakage — is a known, accepted consequence rather than a bug.
  *(ArenaContext is the per-arena handle)*
- **firing envelope** — whether a shot is currently takeable (nose cone, range,
  LOS). ⚠ Read it with `InEnvelope()`, never `Gunsight.Evaluate()` — the latter
  mutates the firing path's LOS cache, so observing changes behaviour.
- **the trigger is a decision, not a permission** — the acting policy owns the
  firing instant; no subsystem vetoes it. Sibling: **aim is a service, not a veto**.
- **velocity reference / feasibility tracker** — the RL↔MPC boundary: the policy
  emits a planar velocity and MPC is demoted to a ~2s tracker (feasibility, aim,
  velocity-track).
- **brain / chooser / intent** — the swappable-decision seam. The contract worth
  knowing: an intent is **idempotent per decision**, so re-applying one is safe.
  ⚠ "Intent" is acknowledged stale — it also carries fire and boost, which the
  name denies; rename carded. *(Brain, IIntentChooser)*
- **presentation** — the per-session axis deciding whether visuals and audio
  exist. Two things a reader needs: it is applied by the owning spawn seams,
  never by per-component globals; and it is **not** the same axis as the deleted
  `vfx` flag, which it has been confused with three separate times.
- **chassis / module / loadout / hangar / sidegrade** — the ship-customization
  naming key, in order of containment: the hull's own stats / the swappable parts
  / the equipped set / the between-run screen where you change it / an option
  that trades rather than upgrades.
- **lane clearing** — shooting asteroids to open a firing lane. Currently
  inexpressible: the firing-envelope check vetoes it, so the policy learned that
  asteroids are walls.
- **bleed-through** — letting a damage remainder cross a shield break; currently
  discarded, which is a hidden alpha-weapon tax.

### Infra & tooling

- **coordinator** — `unity_access.ps1`, the machine-wide Unity access broker. A
  new caller goes through the coordinator; generalize the primitive, never bypass
  it. *(CLAUDE.md wiring §6)*
- **producer-owns-outputs** — when one tool's output is another's input, the
  location and format are the producer's contract; consumers never re-derive
  paths. *(CLAUDE.md §6 corollary)*
- **owner lease / boot lane / two-tier lock** — a per-project run claim / the
  machine-wide startup-only lock / their combination. Pid-backed leases survive
  TTL; pid-less ones expire and orphan live editors. A **wedged** boot lane — an
  unowned boot dir the cleanup cannot delete — reports `boot_lane_wedged`
  (exit 25), never free.
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
- **staging** — copying checkpoints and exes into the primary tree so they
  survive slot recycling. Never leave eval artifacts only in a slot.
- **stdio-vs-durable-server trap** — a session's Unity MCP tools may be a private
  stdio instance blind to the durable 8081 server; diagnose with
  `debug_request_context`.
- **define churn** — Sentis re-adding its analytics define on interactive loads.
  Fixed at two registry values; never fix it by committing the define.
- **orphan discipline** — killed monitors leave `tail.exe`/`grep.exe` holding
  logs (WinError 32). Taskkill by PID, never by image name.

---

## 3. Deprecated forms

| Retired | Use instead |
|---|---|
| council, agent council, agent counsel | **counsel** |
| yaw-thrash | **facing thrash** (symptom) or **command churn** (cause) |
| pursuit gap, pursuit ceiling, pursuit-gradient hole | **Evader hole** (deficit) or **pursuit gradient** (mechanism) |
| entry gate | **entry bar** |
| single-boot gate | **single-boot mode** |
| main tree | **primary tree** (or prim tree / primary) |
| program, package, series (as a work grouping) | **arc**, **slice**, or **pass** |
| ARC COMPLETE | **SHIPPED** or **CLOSED** |
| PR-N as an identifier | **branch-style arc names** (`vocab-docfix-2`) — for new arcs only |
| Phase 0–N as a chapter scheme | **stage** (campaign chapter) or an arc **slice** |
| "Driver:" as a doc header | *(drop it — say what it motivates)* |
