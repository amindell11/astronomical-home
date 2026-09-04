# Unity code conventions

> STATUS: living — branch-triggered reference, read before writing Unity code; pointed at from `AGENTS.md`.

- **Expensive lookups in `Awake()` only** — `GetComponent*`, `GameObject.Find*`,
  `Camera.main` never run in `Start`/`OnEnable`/`Initialize`/update loops or any
  runtime path. Cache in `Awake`; `Initialize(...)` only assigns injected
  references (see `AGENTS.md` → dependency wiring).
- **Unity null checks use the engine's lifetime-aware operators** — use
  `if (obj)` or `obj != null` for `UnityEngine.Object` types. `is null`,
  `is not null`, `?.`, and `??` bypass the destroyed-object check; use
  `object.ReferenceEquals` only when CLR-reference identity is intentional.
  Plain C# types may use normal null syntax freely.
- **Prefab-ASSET reads take a serialized reference, never a lookup in a property
  getter** — no `Awake` runs on an asset, and getter-side `GetComponent` was
  rejected in `Railguns.HangarStats` (#99). Serialize the ref and wire it in the
  prefab; `Awake` is backfill-if-unwired only.
- **Settings-driven behavior reads the `.asset`, not the C# default** — tuned
  values live in the ScriptableObject (e.g. `MpcSettings.asset`); code defaults
  are stale fallbacks and have produced wrong analyses.
- **The game plane is frozen to `PlaneAxis.Z` at origin zero**, pinned by
  `GamePlanePlayModeTests.Canonical_IsFrozenZFrameAtOrigin`. Any change baking a
  plane convention into a constant must use Z — a Y freeze rotates every entity
  onto the wrong plane while leaving Y-configuring tests green.
- **Editor-hook types must subclass what Unity discovers** — Unity finds
  `OnPostprocessAllAssets` only on `AssetPostprocessor` subclasses; a bare
  `static class` fails completely silently, with every test still green. Verify
  a "structurally impossible" claim by causing the failure, not by reading the
  code. Also: never name an editor namespace `Editor` (CS0118 against
  `UnityEditor.Editor` breaks the build).
- **Never hand-author prefab YAML with nested instances** — it degenerates on
  import (the main asset becomes a child rig). Regenerate prefabs through editor
  scripts, and load them with `LoadMainAssetAtPath`.
- **Prefer early returns** (inverted ifs) over nested blocks.
- `[SerializeField]` tooltips are documentation for the inspector, not code
  comments — the comments policy in `AGENTS.md` does not apply to them.
- **Folder taxonomy carries the object relationships** — leaf folders of
  roughly 2–8 files grouped by domain (the `Combat/Projectiles/Audio` grain),
  never by tier or catch-all ("Agent", "Core", "Misc"). A PR adding a file to
  a package root or a 10+-file folder either names the domain subfolder or
  creates it.
- **One primary type per file, file named for that type.** Small satellite
  types (a row struct, an enum, a summary) may ride with their owner; a file
  whose name matches none of its types is the smell.
- **Names mirror location.** A type's namespace is its folder path under
  `Assets/Scripts/` (`Combat/Projectiles/Audio/` → `Combat.Projectiles.Audio`);
  editor code under `Editor/<X>/` takes the namespace of the runtime tree it
  edits. Every assembly is `Game.<Area>[.Runtime|.Editor]`, its `.asmdef` file
  named exactly for it, and editor-only assemblies live under an `Editor/`
  folder. Moving a file moves its namespace in the same diff; a namespace that
  names a folder that doesn't exist is the smell.
- **Structure ratchet:** apply the two rules above to files and folders you
  touch — a file you edit gets its correct home and namespace in the same PR,
  and a renamed or moved folder drags its namespaces with it. Whole-package
  re-taxonomies and rename sweeps are dedicated hygiene PRs, never folded
  into feature PRs. Known drift is inventoried on the open taxonomy issue.
