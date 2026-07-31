# RLHarness Folder Taxonomy

> STATUS: live arc — cut frozen 2026-07-31 (user-approved); move PR
> (`harness-taxonomy-move`) fires after harness-lane slice A (#231) merges and
> before slices B/C/D acquire slots. The move PR deletes this brief.

The RLHarness package accreted lane-by-lane into two catch-all folders (a
13-file root, a 15-file `Agent/`) whose names describe tier, not domain —
unlike the rest of `Scripts/`, where small domain-named leaf folders carry the
object relationships (`Combat/Projectiles/Audio`, `Ships/Command`,
`Game/Sectors/Activation`). Several files are multi-type bundles whose name
matches none of their types (`EpisodeSetup.cs`, `TraversalDrivers.cs`,
`OpponentArchetypes.cs`), so a PR diff's file paths carry near-zero
orientation. This brief freezes the reshuffle; the companion AGENTS.md
guideline (landed with this brief) keeps the grain from regressing.

## Frozen cut

| Folder | Contents |
|---|---|
| `Episodes/` | `EpisodePair`, `EpisodeRunner`, `EpisodeLoopDriver`, `EpisodeTypes` (outcome/end enums), `EpisodePoses`, `EpisodeJsonl` |
| `Episodes/Compositions/` | `IEpisodeComposition`, `ScriptedRosterComposition`, `SelfPlayComposition`, `ShipAgentFactory`, `ShipAgent` |
| `Arena/` | `HarnessArena`, `HarnessField`, `HarnessAssets` |
| `Opponents/` | `OpponentRoster` (+ archetype enum, `OpponentDraw`), `ArchetypeSteering`, `EvaderChooser`, `OrbiterChooser`, `HoldRangeFireChooser`, `DummyChooser`, `RangerChooser` |
| `Probes/` | `ArchetypeGate*`; slice A's probe interface + registry land here |
| `Reward/` | unchanged |
| `Hosts/` | `TrainingHost`, `TrainingBootstrap`, `HarnessSessionHost`, `CheckpointEvaluator`, `EvalProtocol`, `PacingContract`, `EnvParamOverlay`, `RLTrainingPlayerBuild` |
| `Runtime/` | unchanged — asmdef boundary for the in-game inference surface; the one place tier-naming earns its keep |

File splits (the only non-move edits): `OpponentArchetypes.cs` → one file per
chooser + `ArchetypeSteering.cs`; `EpisodeSetup.cs` → `EpisodePoses.cs`
(carrying `SpawnPoses`) + `EpisodeJsonl.cs`. Satellite types stay with their
owners (`OpponentDraw` with the roster, `ArchetypeGateRow`/`Summary` with the
gate) — the `Game/Sectors/Elements` grain.

## Locked decisions

1. **Namespaces stay flat `Game.RLHarness`.** Both precedents exist in the
   repo (`AI.Context` follows folders; `Game/Sectors/Elements` doesn't);
   staying flat makes the move PR a pure `git mv` with zero using-churn.
   Sub-namespacing may follow per-folder later when a real naming win exists.
2. **No type renames.** Long names (`ScriptedRosterComposition`, …) are the
   flat structure compensating; with one-type-per-file in domain folders the
   diff path orients instead. Renames would churn tests/docs/glossary for
   cosmetic gain.
3. **`ShipAgent` + `ShipAgentFactory` ride `Episodes/Compositions/`** — they
   are the agent side of the compose recipes, not host machinery.
4. **Both asmdefs stay put** (`Game.RLHarness.Editor` at package root,
   `Game.RLHarness.Runtime` on the `Runtime/` subtree). New folders all sit
   under the root asmdef's subtree; no assembly change.

## Move-PR obligations

- `git mv` each `.cs` with its `.meta` — GUIDs stable, so the `HarnessAssets`
  SO and scene-attached MonoBehaviours keep their references.
- Update `RL_Harness_Lane_Unification.md` approved-assumption 6: host
  machinery placement `RLHarness/Agent/` → `Hosts/` (user authorized this
  re-decision 2026-07-31; slice A's in-branch doc amendment must be merged
  first — another reason the move PR waits for #231).
- Optional rider commit: traversal-probe deletion (board card
  `project_traversal_probe_retirement` — RULED, gated on slice A's probe
  registry, which #231 carries).
- Delete this brief.
- Full submit gate; no behavior change expected.
