# Per-Sector Environment Scenes (locale scene = authored lighting backdrop)

*Draft • 2026-07-11 • status: design agreed (grill session), implementation soft-gated on driver-seam PR1*

> Supersedes the SO-based sketch in memory `project_sector_skybox_lighting`.
> That sketch carried skybox + lighting as fields on sector data and code-applied
> `RenderSettings.*` at load. The grill reframed it: the thing scenes uniquely
> provide — **baked ambient/reflection + native lighting authoring + free
> audio/backdrop** — is exactly what we want per sector, and code-applying
> `RenderSettings` reinvents a slice of Unity's lighting system while forfeiting
> baked GI. So: **environment is a scene, not an SO.**

## Motivation

Many sectors want distinct skyboxes and moods. Today the skybox + environment
lighting is authored per-scene, and only one world scene (`BasicWorld`) is ever
loaded — so there is no per-sector variety. Worse, the current wiring is a latent
no-op: sectors load `BasicWorld` **additively** via
`EnvironmentService.LoadSceneAsync`, but **nothing calls `SetActiveScene`**, so
`RenderSettings` (skybox, ambient, reflection, fog) resolves to the *boot* scene
(EditScene, which #113 gave the nebula sky). The world scene's authored lighting
is **inert**. That is why the visible change in #113 was "switch EditScene's
skybox," not BasicWorld's.

## The cleave: environment = scene, gameplay = prefab

The sector refactor (`project_sector_datadriven_refactor`) deliberately fled
scenes for **gameplay content** because scenes force cross-scene wiring (Unity
forbids direct `[SerializeField]` refs across scene boundaries → back to the
"role string" registry anti-pattern the refactor killed), Awake-on-load breaks
the "instantiate inert → wire → activate" determinism trick, and RL wants N
arenas per process which prefabs instance cleanly but scenes do not.

**Those objections apply to gameplay, not environment.** The environment layer
has *no gameplay references to inject* (nothing wires into a skybox), and RL runs
**headless** — an RL arena never loads an environment scene at all. So the
multi-arena/instancing objection to scenes **evaporates for environment.** The
axis that decides scene-vs-prefab is therefore **"does it need cross-object
wiring / N-instancing,"** not "is it authored":

- **Environment = scene.** Author N *locale* scenes (Deep Nebula, Red Giant, …),
  each with native baked lighting + skybox + reflection + ambient audio +
  backdrop. Not reinvented. Cheap. Per-sector variety = which locale the sector
  references.
- **Gameplay (encounters, ships, spawners) = prefab + manifest.** Unchanged —
  wired, instanceable, deterministic, RL-ready. Encounters are the *worst*
  candidate for scenes despite being authored, because they carry gameplay wiring
  (`EncounterSequenceModule` injects the `chaser`) and need N-instancing.

So: **Sector = an authored locale scene (environment) + a procedural manifest of
gameplay prefabs composed on top.**

## What a locale scene owns

Recipe per locale (space-appropriate; author in Unity, "Generate Lighting" once):

- **Skybox material** (nebula HDR, `Skybox/Panoramic`) — per-locale variety.
- **Ambient = `AmbientMode.Skybox`** (IBL from the HDR) — colored ambient matching
  the nebula.
- **One+ directional light** — the key star; per-locale direction/color/intensity.
- **A reflection probe** capturing the skybox (or default skybox reflection) so
  shiny hulls reflect the right sky.
- **Fog** optional, per-locale mood.
- **Ambience + Music AudioSources** — per-locale audio *content* rides in the
  scene. (The persistent **Audio Reverb Zone** stays on `WorldRoot` — it's
  world-tier infra, not per-locale content.)
- **No lightmaps** — the scene has no meaningful static geometry (asteroids/ships
  dynamic; the backdrop *is* the skybox). "Generate Lighting" is here to
  precompute the **ambient probe + reflection cubemap**, not lightmaps.

**The bake is the payoff over the SO approach:** precomputed ambient SH +
reflection travel *with the scene*, so loading a locale brings coherent lighting
with **zero runtime `DynamicGI.UpdateEnvironment` cost and no re-bake pop.** The
SO+code path could never do that; it'd pay an environment update every load.

## The two star layers (do not conflate)

There are two kinds of stars, split by whether they move with the viewer:

| Layer | Nature | Home | Per-sector? |
|---|---|---|---|
| **Far stars + nebula** | infinite, baked into the HDR skybox | per-locale scene | Yes — the variety |
| **Near parallax quad** (`StarField.shader`) | camera-following, motion parallax | persistent `WorldRoot` (already a child of `World.prefab`) | No — universal |

The near parallax quad **stays on `WorldRoot`** and is out of scope for this
feature. It must follow the camera to parallax; putting it in the locale scene
would force a cross-scene ref to the persistent camera — the exact problem we
avoid. A *bonus* property falls out: during a locale swap the **near layer stays
put** (visual continuity, no pop) while only the **far backdrop** changes behind
it. Per-locale variation of the near layer (density/tint) is a possible future
add (locale carries a near-star material the loader assigns onto the persistent
quad) — **explicitly deferred.**

## Lifetime: locale-lifetime, not sector-lifetime

The environment scene is **not** sector-lifetime — it is **locale-lifetime**,
swapped only when the locale actually changes, persisting across same-locale
sector loads (like the rig persists while content cycles). Two reasons force this:

1. **Coroutine-vs-unload ordering.** `Sector.Teardown` unloads the world scene
   *last*, and `LoadSector` does `sector.transform.SetParent(null)` (dropping the
   sector into the active scene). If we `SetActiveScene(locale)`, the sector
   controller — the MonoBehaviour running the teardown coroutine — could land *in*
   the locale scene and be destroyed mid-teardown when that scene unloads, killing
   its own coroutine. **Our controller objects must never live in the locale
   scene** (they stay in the boot/DDOL scene); only *lighting* keys off
   active-scene.
2. **Needless churn.** Two consecutive sectors sharing a locale should not
   unload+reload the scene (wasteful; forces reflection/GI re-pop). `EnvironmentService`
   already no-ops a redundant load (`loadedSceneName`) — diffing is half-built.

So environment load/`SetActiveScene`/unload moves **out of `Sector.Setup/Teardown`**
and **up to the loader** (`MainGameManager.LoadSector`, via `EnvironmentService`):
on load, if the sector's locale differs from the currently-loaded one, unload the
old + load + `SetActiveScene` the new; if same, no-op. On session teardown,
unload + restore the boot scene as active.

**De-risked:** all persistent infra is `DontDestroyOnLoad` — session root
(`MainGameManager`), `SimplePool`'s pool parent, pooled audio — so the SetActive
side-effect cannot orphan pools or the session on a locale unload.

## The reference

The locale-scene reference moves **off** `Sector.sceneName` (prefab template,
justified only when the scene was inert infra) **onto `SectorSettings`** (the
per-entry SO) — because one `PlaySector` prefab backs multiple `SectorEntry`
configs, and variety must be per-entry. Type: a small serialized
**`SceneReference`** wrapper (editor `SceneAsset` → validated path/GUID) rather
than a raw string, so a missing/misnamed locale is caught at author time.
**Nullable** → null means "no locale scene, inherit boot-scene lighting," which
is also the headless path.

## Design invariants (boundaries)

- **RL / headless skips environment entirely.** The loader skips the whole
  environment step when the locale ref is null **or** presentation is off. RL runs
  headless, never `SetActiveScene`s, never loads a locale. Ties to the driver
  seam's `profile.presentation`.
- **No multi-arena tension — by construction.** N-arena is **always headless**
  (never rendered). So the process-global `RenderSettings`/active-scene is never
  contended across arenas. Environment is a cleanly *presentation-only* concern;
  there is no interim seam to mark here (unlike `GamePlane`/`ObstacleFields`).

## Sequencing — soft-gated, 2 PRs

**Soft-gate:** design now; hold the PR until agent-2's driver-seam PR1
(`task/sessionrig-deaccretion`) merges, to avoid bootstrap churn (PR2 of that seam
moves `GamePlane.Configure` down into `ComposeSession` and reshuffles world/env
ownership). The core here is rig-independent — it lives in `EnvironmentService` +
the loader + sector data — but it shares files with the in-flight refactor.

**The sequencing landmine:** turning on `SetActiveScene(locale)` is itself a
**visible flip** — today EditScene's nebula renders and BasicWorld's lighting is
inert; the instant the loader SetActives BasicWorld, its *old sample skybox*
becomes what you see, a regression — **unless BasicWorld already has the good
baked lighting.** The flip must land with correct content.

- **PR-A — mechanism + neutral flip (rig-independent core).**
  `EnvironmentService` gains: locale load → `SetActiveScene` → restore-boot-active
  on teardown → same-locale no-op (diff). Loader drives it on locale change. Move
  the scene reference `Sector.sceneName` → `SectorSettings` (`SceneReference`).
  Move Ambience/Music into the locale scene; leave the Reverb Zone on `WorldRoot`.
  **Same PR: promote BasicWorld to the good baked nebula lighting** so the
  SetActive flip is neutral-or-better (also the honest fix for "world-scene
  lighting was silently inert"). PlayMode tests: locale swap, boot-restore on
  teardown, same-locale no-op.
- **PR-B — variety proof (mostly Unity authoring, user's hands).** Author a 2nd
  locale scene (distinct sky / sun / mood), wire two `SectorEntry` configs to
  different locales, verify per-sector variety in-Editor.

**Deferred:** audio crossfade on locale swap (audio now lives in-scene; a hard cut
on SetActive may pop); per-locale near-star (parallax quad) variation.

## Touch points

- `Game/Services/Environment/EnvironmentService.cs` (+ `IEnvironmentService`) —
  locale load/`SetActiveScene`/restore-boot/diff; owns the active-scene seam.
- `Game/Bootstrap/MainGameManager.cs` — `LoadSector` drives the locale swap on
  change (currently the sector self-loads its scene in `Setup`).
- `Game/Sectors/Sector.cs` — drop `sceneName`/`loadScene`; sector no longer
  loads/unloads a scene.
- `Game/Sectors/SectorSettings.cs` — add the `SceneReference` locale field.
- New: `SceneReference` wrapper (+ its property drawer).
- `Assets/Scenes/BasicWorld.unity` — promote to a real baked nebula locale;
  Ambience/Music stay, migrate the #113 skybox in.
- New locale scene(s) for variety (PR-B).
- Tests: `SectorLifecyclePlayModeTests` retarget (scene load moves off the
  sector); new env-swap/boot-restore/no-op coverage.

## Non-obvious facts to re-verify before coding

- Nothing calls `SetActiveScene` today → world-scene lighting is inert (grep
  confirmed 2026-07-11). Fixing this is a **visible** change, not a refactor.
- Persistent infra is DDOL (`MainGameManager`, `SimplePool` parent,
  `PooledAudioSource`) → SetActive instantiation side-effect is safe for them.
- The parallax `StarField` quad is already a child of `World.prefab`
  (`WorldRoot`) alongside the Audio Reverb Zone + Boundary.
