# RL Harness Lane Unification (the "PR-3 arc")

> STATUS: design FROZEN 2026-07-29 (pr-prep session, all forks user-resolved).
> Build NOT started. Slices A–F below; each slice gets a short pr-prep of its
> own before building, but re-deciding anything in this doc is out of bounds.

Spun out of `RL_Infra_Paydown_Pass.md` §PR-3 when scoping showed it is an arc,
not a PR. Context: `handoff_2026-07-27_stage3_results.md` (why eval
trustworthiness matters), `handoff_2026-07-28_rules_change_design.md` (the
telemetry surface this substrate must eventually serve).

## Concept model

Every harness session is a point in this grid. The host serves the grid; lanes
are presets, not classes.

| Axis | Values (today → designed-for) |
|---|---|
| A. Team-0 pilot | checkpoint (InferenceOnly) · heuristic · trainer (training hosts only) · human (playtest — future) |
| B. Team-1 opponent | scripted archetype (pinned; stratification is client sequencing) · mirror (same ckpt) · second checkpoint (slot 2) |
| C. Pacing | locked fast contract (measurement) · real-time (watch / playtest) |
| D. Presentation/graphics | off (`-nographics`) · on (capture / watch / playtest) |
| E. Recording | off · clips (episode selection, resolution) |
| F. Probes | any set of registered samplers (+ paired painters) |
| G. Episode plan | client-sequenced blocks — a *block* = N consecutive episodes vs one opponent config, on one composition, one hook set (the `RunBlock` unit; today's eval = 5 blocks per seed) |
| H. Summary client | eval W/L/D+Wilson · probe aggregate · bench delta · none |

**Boundary.** In: offline lanes (eval, capture, probe, bench) on one substrate;
second ONNX slot; painter/canvas contract; `record.flag` retirement; thin
Python surface + eval_gate watch extraction. Expressible but NOT built:
playtest (axis A=human — needs input wiring, own future PR), profiler lane
(counter probe + raw capture — leaf, post-arc), throughput preset (bench
hardening HELD for separate discussion). Out: TrainingHost absorption (trainer
lifecycle is genuinely different machinery; sharing stays at the composition
layer), PR-4's statistics (verdicts, replicates, intervals, ELO math),
threshold recalibration (locked: waits for the rules change).

## Locked decisions (fork → resolution → why)

1. **Topology: one host + typed `SessionSpec` + summary-client seam.** Env
   protocol parses ONCE at the boundary into a typed spec; illegal combos
   (recording without graphics, …) throw at boot. Axis H is code, not config —
   a small client object. Rejected sibling hosts (EvalHost/CaptureHost/…):
   grid axes are orthogonal, and host-per-lane freezes grid points into
   classes — the `RecordCheckpointEpisodes` "keep in lockstep" duplication
   pattern at host scale.
2. **Machinery split: two host primitives, clients are protocol coroutines.**
   `NewComposition(seed, opponentKind)` creates the pair/composition;
   `RunBlock(composition, opponent, episodes, extraHooks)` owns the mechanical
   layer — per-episode opponent install, wiring the spec's probes AND recorder
   into `RunEpisode(onBegin,onFixedStep)`, JSONL append, disposal ordering.
   Clients sequence: eval = per seed {NewComposition; per archetype
   {RunBlock}}; capture = once; bench = per condition {NewComposition; physics
   toggle; RunBlock}. Why: protocol knowledge (fresh-pair-per-SEED RNG replay,
   stratification, bench conditions) is client knowledge; a plan-DSL would
   swallow it and still not fit the bench. Composition creation is a sibling
   primitive (not inside RunBlock) specifically to preserve today's
   fresh-pair-per-seed eval protocol byte-for-byte.
3. **Opponent axis + slot 2.** Single env var `RL_EVAL_OPPONENT` = `roster`
   (default) | archetype name | `mirror` | path ending `.onnx` (imported into
   a second gitignored fixture slot `EvalOpponent.onnx`;
   `ImportEvalCandidate` parameterized by slot). `ComposeSelfPlayPair` gains
   per-side models. Summary: field renamed `archetypes` → `opponents`
   (labels: archetype name, `Mirror`, opponent-ckpt stem), top-level summary
   gains a schema id (`rl-eval-summary-v2`) and records SOURCE paths for both
   slots (provenance — today only the gate's dir naming knows). `eval_gate.py`
   reader updated in the same slice; its resume/replay path checks the schema
   id and fails loud on mismatch. Why rename now: two readers exist, both in
   this arc's blast radius; the rename's cost is at its historic minimum and
   rises with every PR-4/telemetry consumer.
4. **Probe contract: interface + name registry + per-probe summary block.**
   Lifecycle Begin(context)/Sample()/End(result)→row + Summarize()→JSON block
   written into the caller-named out dir; selection by name via
   `SessionSpec.probes`; per-probe params as a flat key→float map (all the
   facing probe needs: the wFacing override). `ArchetypeGateProbe` adapts
   (it already has exactly this lifecycle) and becomes the eval preset's
   default probe rather than hardwired. A probe MAY expose a painter (same
   identity) so its markup appears in filmed runs. Registry roster: gate,
   contact, facing (slice D), then designed-for: profiler-counter, heat read,
   rock-shooting. Abstraction budget: 3 concrete + 2 parked implementations
   justify the interface as consolidation, not speculation.
5. **`record.flag` dies (rung 1).** It existed only to smuggle config into a
   PlayMode test; the host takes env vars. `RecordConfig`, the whitelist
   parser, and `RecordCheckpointEpisodes` are deleted (~250 lines). The
   sticky-flag footgun becomes unrepresentable. `watch.flag` (human-present
   real-time) and the `RL_EPISODES` characterization lane stay test-side.
6. **Ram bench: split, then absorb.** Contact metrics (`ContactSampler`) →
   registry probe (early; also serves rules-change telemetry). The
   layer-split physics regression → a host client: condition loop around the
   primitives, committed pass margins, delta JSON + verdict field, nonzero
   exit on regression; NUnit form retires. Deterministic committed margins ≠
   PR-4's statistical verdicts — no boundary collision. Late slice: needs
   slot 2 to become candidate-vs-frozen-rammer (`ShipCombat-999950`).
7. **Python surface: launcher + watch extraction (arc-final slice).** One
   lane launcher on PR-2's `driver_common` (compose env → `run_batch` →
   read back from caller-named out dir). eval_gate's checkpoint-watch loop
   (discover → per-step dir → replay-or-run) extracts to a library; eval_gate
   becomes watch + launcher + verdict rules, behavior byte-identical,
   `test_eval_gate.py` green, verdicts untouched (PR-4's). `rl_eval.ps1`
   (PR-1 rescue-promotes it) retires here — rescue-then-absorb, per the
   ram-bench pattern. No cross-language env pinning: the C# SessionSpec
   parser is the single authority and throws at boot, so drift fails loud on
   first run (unlike the silent-drift JSONL suffix contract PR-2 pins).
8. **Painter/canvas contract (in-arc); migration deferred.** A small canvas
   interface — `CaptureDraw`'s existing vocabulary (Line/Vector/Ring/Trail/
   Label, plane space) — with two backends: CaptureDraw (clips) and a Gizmo
   canvas (live editor). Painters live runtime-side with name-based identity
   and draw only the active set; editor Gizmos/Handles can never render into
   offscreen captures (documented in CaptureDraw), so one-source-two-renderers
   is the only possible unification. `ShipDiagnosticsOverlay` (leaves the
   test assembly) and #222's `PolicyGizmos` are rewritten as painters. The
   legacy `AIDebugChannel` flags enum + ~15 scattered gizmo files stay
   untouched until the deferred migration arc (carded) — two identity
   systems, deliberately temporary.

## Blindsider resolutions (code-grounded, folded in above where structural)

- **Batch hang → fast fail:** the host wraps the client coroutine; an
  exception logs and `EditorApplication.Exit(1)`s instead of today's silent
  coroutine death + play-mode hang until the caller's lease-wait expires.
- **Graphics validation mechanism:** `SystemInfo.graphicsDeviceType == Null`
  detects `-nographics` at parse time for the record⇒graphics combo check.
- **Capture artifacts join the out-dir contract:** clips/frames/manifest land
  under the caller-named session dir, not the hardcoded `results/rl-episodes`
  root (producer-owns-outputs corollary applied to film).
- **Env naming:** existing `RL_EVAL_*` names keep working through A–E
  (eval_gate compat); new axes join the family; slice F may rationalize the
  prefix atomically with the child script + entry rename.
- **No episode watchdog added:** truncation (`timeoutDecisions`) bounds
  episodes, as today; a genuine hang is a programmer error surfaced by the
  fast-fail wrapper above, not absorbed by a timer.

## Approved assumptions

1. `TrainingBootstrap.RunEval` keeps name + env contract through A–E; any
   rename happens in F atomically with `eval_child.ps1`.
2. Artifact contract unchanged: caller-owned `RL_EVAL_OUT_DIR`,
   `EpisodeJsonl.NewRunPath` dirOverride, existing `results/` layout.
3. `EvalProtocol` constants untouched (held-out seeds, InferenceSeed,
   canonical density 2.0, Wilson) — PR-4's territory.
4. Slice A is behavior-identical: same episode sequence + JSONL rows as
   today's evaluator on same inputs; summary differs only by schema change.
   Verified against a golden pre-A run. **AMENDED 2026-07-29: identity is
   gated at the seeded-protocol layer — see §Slice A brief → Verification.
   The pre-A eval itself is not run-to-run bit-reproducible (sim-level,
   threading-independent; carded: board BUGS + memory
   project_eval_sim_nondeterminism), so full-row byte-identity is
   unattainable by any refactor and was over-specified.**
5. Pacing/presentation combos validated at parse; locked pacing +
   presentation-off for measurement lanes; real-time reserved for watch.
6. Placement: host machinery in `RLHarness/Agent/`; canvas + painters
   runtime-side (single asmdef → folder taxonomy); Gizmo backend in Editor asm.
7. Tests: EditMode for spec parsing (incl. illegal-combo throws) and
   summarizers; one PlayMode lane smoke on `ShipCombat-smoke.onnx` in the
   merge gate; capture graphics-gated; bench by explicit invocation only.
8. Mirror rows self-fingerprint (`opponent = "Mirror"`) — closes the
   no-JSONL-trace gap.
9. Bookkeeping: paydown §PR-3 is a pointer here; facing-probe ledger row
   resolves at slice D; deferred arcs carded on the board.

## Slices

| # | Slice | Contents | Depends on |
|---|---|---|---|
| A | Substrate + eval migration | SessionSpec + host + primitives; probe interface/registry, gate probe adapted; CheckpointEvaluator → eval client; opponent grammar (roster/archetype/mirror); schema v2; eval_gate reader line. Behavior-identical eval. | PR-1 |
| B | Capture + painters | Canvas interface + both backends; painter identity; overlay + PolicyGizmos as painters; recording axis + recorder in RunBlock; child-script graphics conditional; record.flag deleted. Mirror capture = config. | A, #222 |
| C | Second ONNX slot | Slot-parameterized import; per-side models; path grammar value; opponent-stem labels. Small. | A |
| D | Probe clients | Facing probe rebuilt (summary schema, wFacing param) — resolves ledger BLOCKED row, confirms #219 behaviorally; contact probe extracted. | A |
| E | Ram-bench regression client | Condition loop, committed margins, verdict + nonzero exit; candidate-vs-rammer via slot 2. | C, D |
| F | Python surface | Launcher on driver_common; watch extraction; eval_gate re-plumb; retire rl_eval.ps1; optional auto-assemble for capture runs. | PR-2, A (B for capture ergonomics) |

B, C, D parallel after A (mostly file-disjoint).

## Coordination constraints

- **PR-1** (agent-2): lands VfxEnabled deletion (host code must not reference
  it), parks ram bench at `training/archive/ram-bench-harness/`, promotes
  `rl_eval.ps1` (retired again in F — intended lifecycle, not churn).
- **PR-2** (agent-3): owns `training/rl` drivers, `unity_access.*`, README.
  This arc adds files and touches `eval_gate.py` only post-PR-2; never
  touches PR-2's files.
- **#222** (agent-1): must merge before B rewrites `PolicyGizmos` as a
  painter. `IPolicyReadout`/`PolicyAction` are the pattern B generalizes.
- **PR-4**: designs against this doc once frozen; owns verdicts, replicates,
  intervals, ELO math, threshold work. The bench's deterministic margins and
  the schema v2 rename are this arc's; everything statistical is PR-4's.

## Deferred (carded on the board)

- Gizmo migration + "observation environments": migrate the ~15 legacy gizmo
  files onto painters; named preset sets, nothing-on-by-default. Parallel arc
  any time after B.
- Playtest lane: axis A=human input wiring in the harness arena; inherits
  probes/telemetry/recording for measured playtests (rules-change fork 6's
  instrument).
- Profiler lane: ProfilerRecorder counter probe + `Profiler.logFile` raw
  capture flag; first customer = shelved frame-drops investigation.
- Heat-read + rock-shooting probes: ~50-line registry additions when their
  investigations pull (parking lot).

## Code anchors

`CheckpointEvaluator.cs` (the split subject) · `EvalHost.cs` +
`TrainingBootstrap.RunEval` (entry precedent) · `EpisodeLoopDriver.RunEpisode`
(hook seam) · `IEpisodeComposition` + `ScriptedRosterComposition` /
`SelfPlayComposition` (composition family) · `ShipAgentFactory` (compose
recipes; per-side models land here) · `OpponentRoster` (pinned install) ·
`ArchetypeGate.cs` (probe precedent) · `RLEpisodePlayModeTests.cs` ~380–650
(capture lane to delete) · `CaptureDraw`/`CaptureRecorder` (canvas backend) ·
`training/archive/ram-bench-harness/` (bench to rebuild; API-rotted) ·
`eval_gate.py` + `eval_child.ps1` (Python seams).

## Slice A brief

> FROZEN 2026-07-29 (slice-local pr-prep; forks, assumptions and blindsiders all
> user-resolved). The implementing agent builds from this plus the doc above and
> re-decides neither. Verified against main @ `dd2b7a89` (post-PR-1 #223).

**Scope.** `SessionSpec` + `HarnessSessionHost` + the `NewComposition`/`RunBlock`
primitives; probe interface + name registry with `ArchetypeGateProbe` adapted;
`CheckpointEvaluator` rebuilt as the eval client; opponent grammar (roster /
pinned archetype / mirror); summary schema v2; the `eval_gate.py` reader update.
Eval output stays behavior-identical.

**Non-goals.** No recording / painters / canvas, no second ONNX slot, no
`record.flag` or `RecordCheckpointEpisodes` deletion (all B/C). The
`RLEpisodePlayModeTests` record lane is untouched — its "keep in lockstep"
comment goes stale until B deletes the lane. `EvalProtocol`, `TrainingHost`, and
the two training compositions are untouched. `training/rl/README.md` gets only
the two new env lines (below); PR-2 owns the rest and F rewrites it.

### Tier map (vocabulary for slices B–F)

The harness host/client split is the same seam the game tier already has, and the
names should stay legible against it:

| Tier | Game | Harness (this arc) | Training |
|---|---|---|---|
| Primitives (no clock, no policy) | `SessionHost : ISessionPrimitives` | `HarnessSessionHost` (`NewComposition`/`RunBlock`) | — |
| Driver (clock + protocol/reset policy) | `GameDriver` | the lane client (`CheckpointEvaluator`, …) | — |
| Fused both halves | — | — | `TrainingHost` |

Do NOT force the harness through `ISessionPrimitives`: same shape of seam,
different nouns (the game's lifecycle unit is a sector, the harness's is an
episode block). The broader "what should these three actually share" question is
carded — [[project-session-tier-convergence]] — and is explicitly out of scope
here.

### Forks (resolved, with why)

1. **Host identity → rename now; `EvalHost` is rebuilt as `HarnessSessionHost`.**
   A is the substrate slice; a host still named `EvalHost` invites B to bolt
   capture on as a sibling — the host-per-lane shape decision 1 rejected. Plain
   `HarnessHost` loses to `TrainingHost` ambiguity; `SessionHost` collides with
   `Game.Bootstrap.SessionHost`.
2. **Probe artifacts → per-probe sidecars.** Each probe writes
   `<base>-<name>.jsonl` + `<base>-<name>-summary.json`; the eval summary carries
   a `probes[]` pointer list instead of a typed `behavior[]`. Embedding would
   force the client to know the gate probe concretely (JsonUtility can't
   serialize heterogeneous blocks), leaving `Summarize()` unexercised until D and
   making a schema v3 likely — defeating the point of paying the rename cost now.
3. **Client seam → enum + switch in A; `ISessionClient` extracted at B's second
   client.** Scope conservation wins when the interface would have exactly one
   implementer and the extraction point is already scheduled (wiring philosophy
   #6, consolidation at the second caller). The serialized lane enum also
   survives the domain reload, which a live client object would not.

### Assumptions (locked; code-grounded)

1. `SessionSpec` is a `[Serializable]` class parsed in `RunEval` **before**
   `EnterPlaymode`, carried on the host as a `[SerializeField]` — today's proven
   mechanism (`TrainingBootstrap.cs:41-59`). Seed resolution and the artifact tag
   move from `Start` into parse, so a malformed `RL_EVAL_SEEDS` fails at the
   boundary instead of inside play mode.
2. Fields for A's axes only: onnx source + imported path, resolved seeds + tag,
   episodesPerSeed, density, opponent (kind + label), probe names, outDir, lane.
   No pacing / presentation / recording fields — unrepresentable until B.
3. Present-but-invalid throws. `RL_EVAL_EPISODES_PER_SEED=abc` is silently
   ignored today (`if (int.TryParse(...))`, `TrainingBootstrap.cs:50`); it joins
   the `ResolveNumArenas`/`ResolveHybridScriptedWorkers` throw precedent.
4. Opponent grammar: `roster` (default) | archetype name | `mirror` | `*.onnx` →
   throws "second checkpoint slot lands in slice C"; unknown token → throw naming
   the legal set. `RunEval` keeps its name, signature and env contract.
5. `EvalHost.cs` is deleted, replaced by `HarnessSessionHost.cs` in
   `RLHarness/Agent/`. No scene references it (`RunEval` builds it into an empty
   scene), so this is pure code — no asset/GUID churn.
6. `RunBlock` reproduces today's per-episode ordering exactly: pinned install
   BEFORE `RunEpisode`, probe `onBegin`/`onFixedStep` hooks, then
   `RecordOpponent(draw)` AFTER the episode — that trailing call is what puts the
   draw in the JSONL row (`CheckpointEvaluator.cs:90-103`).
7. Two new compositions, because the existing ones don't fit: `ScriptedRoster`/
   `SelfPlayComposition` each spawn their own arena AND field, but eval composes
   arena+field once and only the pair per seed. `InferenceRosterComposition` and
   `MirrorComposition` take the host-owned `(units, arena, projectiles, field)`.
   `ISessionComposition` exposes `Driver`, `Pair`, and
   `InstallOpponent(...) → OpponentDraw`; the mirror returns a `Mirror`-labelled
   draw without installing, which is how approved assumption 8's self-fingerprint
   falls out for free.
8. Mirror needs no factory change: `ComposeSelfPlayPair(..., behaviorType,
   parent, onnxAssetPath)` already drives both sides from one frozen checkpoint
   (`ShipAgentFactory.cs:39-56`).
9. `CheckpointEvaluator` keeps its name and file, reshaped into the eval client
   (referenced by README, plan docs, and `RLEvalProtocolEditModeTests`).
10. The probe splits along a seam the code already has: today's class is
    per-episode by construction (baselines the spawn pose in its ctor, unhooks in
    `Dispose`) while `ArchetypeGateSummary.Summarize` is already a static over a
    row list. So today's class becomes `ArchetypeGateSampler` (per-episode, math
    unchanged) and a new session-scoped `ArchetypeGateProbe : ISessionProbe` owns
    one per episode, groups rows by opponent label, and writes both sidecars.
    `OpponentArchetypePlayModeTests` — the probe's second consumer
    (`:100`, `:169`) — keeps using the sampler directly: a 4-line rename.
11. Probe interface: `Name` · `Begin(in ProbeContext)` · `Sample()` ·
    `End(in EpisodeResult) → string` (the JSONL line) · `Summarize(outDir)` ·
    `IDisposable`. `ProbeContext` carries pair, arena center, spec, episode index,
    opponent draw + label. Registry maps `name → factory`; unknown name throws
    listing the registered set. Roster in A is `gate` only.
12. Per-archetype grouping and the teacher-scorecard `Debug.Log` move into the
    probe — probe-domain concerns the evaluator currently holds
    (`CheckpointEvaluator.cs:70, 127-131`).
13. Summary v2: `schema: "rl-eval-summary-v2"`; `archetypes[]` → `opponents[]`
    AND the inner `archetype` field → `opponent` (a Mirror block reading
    `archetype: "Mirror"` would be a lie); `checkpointSource` for provenance
    (slot-2 source is C's); `behavior[]` + `behaviorJsonl` → `probes[]`.
    `Summarize` keeps its name; `ArchetypeSummary` → `OpponentSummary`.
14. Golden run (verification, below).
15. Test list (below).
16. This PR is also the commit vehicle for `RL_Harness_Lane_Unification.md` (new)
    and the §PR-3/STATUS edits to `RL_Infra_Paydown_Pass.md` — copy both from the
    primary tree verbatim; both apply cleanly on `dd2b7a89`.

### Blindsider resolutions

1. **The fast-fail wrapper can't catch what it's for → use the log hook.** In
   Unity, `yield return someEnumerator` hands the inner enumerator to Unity's
   scheduler, so a `try/catch` around the outer's `MoveNext` never sees an
   exception thrown inside `RunEpisode`, and never sees one from a ship's
   `Awake`/`FixedUpdate` at all. Resolution:
   `Application.logMessageReceived += ...; if (type == LogType.Exception) → log,
   flush, EditorApplication.Exit(1)`. Simpler AND strictly more complete than a
   manual enumerator pump. Exit(0) on success stays behind the existing
   `Application.isBatchMode` guard.
2. **Probe params: contract locked, grammar deferred.** The registry factory
   signature takes the `key→float` map as decision 4 specifies, but `SessionSpec`
   carries only `string[] probes` in A — `Dictionary` is not Unity-serializable,
   and A's gate probe takes zero params (`wFacing` is D's). The env grammar for
   params is invented in D alongside its first real user.
3. **`RunBlock` omits `extraHooks` in A.** No caller until B's recorder. Adding a
   parameter later is not re-deciding topology; the A signature deliberately
   won't match the frozen text literally.
4. **The `eval_gate.py` change is reader line + schema guard + test.** Decision 3
   requires failing loud on a schema mismatch, which is more than one line; all of
   it lives inside `read_score`'s blast radius and `test_eval_gate.py` is not in
   PR-2's diff, so there is no collision. **Operational consequence for the PR
   body:** after A, a restarted gate replaying pre-A `step-*/` dirs dies on the
   guard — an in-flight gate must finish or restart into a fresh dir. Nothing is
   running as of 2026-07-29.
5. **Document the two new env vars** (`RL_EVAL_OPPONENT`, `RL_EVAL_PROBES`) at
   `training/rl/README.md:143-147`. The producer owns its contract's
   documentation; four slices of undocumented user-facing env is worse than a
   two-line touch of a file whose owner (PR-2) has landed.

### Verification — golden pre-A run (AMENDED 2026-07-29)

The original criterion — full-row byte-identity — is unattainable: the pre-A
eval is not run-to-run bit-reproducible (rollout drift at identical seeds;
threading ruled out by a `-job-worker-count 0` A/B; evidence in memory
`project_eval_sim_nondeterminism` + board BUGS card). Discovered by this
slice's own baseline runs; predates slice A. The criterion below gates
everything the refactor can actually break — the seeded protocol layer, which
is 75/75 reproducible across all four baselines.

Four baseline runs exist on main @ `d61b31cc` (2× default, 2×
`-job-worker-count 0`), 75 episodes each, archived with the comparator at
`results/rl-eval/golden-main-d61b31cc/`.

1. **Deterministic-mask identity (gated).** `golden_compare.py` computes the
   mask = fields identical across ALL FOUR baselines at every episode (18
   episode + 8 probe fields; must contain the protocol fields — spec, opponent
   draw, episodeIndex, startRange, schema/geometry constants — or the
   comparator refuses). The post-A run must match baseline run 1 on every mask
   field, 75/75, both streams.
2. **Structure (gated).** 75 + 75 rows; seed-major, `EvalArchetypes`-order
   blocks; per-block episode indices 0..N−1; tag `custom`; artifact set per
   fork 2 (probe sidecars).
3. **Stochastic fields (reported, not gated).** decisions, simSeconds, ranges,
   rewards, outcome — paired per episode in the PR body; their drift is the
   carded sim bug, not this refactor.

### Tests

- **New `RLSessionSpecEditModeTests`** — parse defaults; opponent grammar
  including both throw paths (unknown token, `.onnx` → slice C); garbage
  episodes / density / seeds throw; unknown probe name throws.
- **`RLEvalProtocolEditModeTests`** — `EvaluatorSummarize` updated for the
  rename, plus a v2 schema-id assertion.
- **PlayMode lane smoke** — the existing
  `CheckpointEvaluator_SmallRun_AggregatesOutcomesAndWritesArtifacts` becomes it,
  asserting `opponents[]` + probe sidecar artifacts, with a short **mirror block
  folded into the same test** (covers `MirrorComposition` + the `Mirror`
  self-fingerprint) rather than a new fixture. Stays in the merge gate.
- **Python** — `test_eval_gate.py` gains a `read_score` test (valid v2 summary,
  and schema mismatch → `SystemExit`). `read_score` is untested today.
