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
- **Structure ratchet:** apply to files and folders you touch; whole-package
  re-taxonomies are dedicated hygiene PRs, never folded into feature PRs.
