# SimplePool Registry + Live-Instance Flush

**Date:** 2026-07-13 (deferred from PR #138 review — Codex P1 on `EpisodeSetup.ProjectileFlush`)
**Status:** Scoped, not started. Small standalone PR.
**Origin:** `doc/Feature_Plans/RL_Episode_Reward_Layer.md` (episode reset needs "return every
in-flight projectile to its pool"); Codex P1 thread on #138 (`EpisodeSetup.cs:41`).

> **One-line intent.** Kill the scene-wide `FindObjectsByType<ProjectileBase>` in the RL
> episode reset by teaching the pool to answer "what's checked out" — via a self-registering
> non-generic pool registry that also removes the rotted reflection helper and its dead code.

---

## Problem

`ProjectileFlush.ReturnAllToPool()` (RL episode boundary, PR #138) scans the whole scene with
`FindObjectsByType<ProjectileBase>`. That violates the repo rule that FindObject-style scans are
Awake-only (`src/Asteroids3D/Assets/AGENTS.md`), and it sits inside the RL loop: once PR-3
training runs episodes back-to-back across N arenas, an O(scene) scan per reset scales with
asteroid count, not projectile count.

The scan exists because the pool cannot be asked. `SimplePool<T>` (`Utils/SimplePool.cs`) is a
**static generic class**: every closed type (`SimplePool<Laser>`, `SimplePool<Missile>`, …) has
its own independent statics, there is no shared base, and the CLR cannot enumerate the
instantiations of a generic. So no "for all pools" operation is expressible without extra
machinery — and the machinery that exists demonstrates the failure mode:

- `SimplePoolManager.ClearAllPools()` reflects over a **hand-maintained list of closed pool
  types** that names only `PooledAudioSource` (plus an "add other types here as needed" comment
  nobody obeyed). It never knew about projectile or VFX pools.
- It has **zero callers** — a rotted registry that is also dead code.

Meanwhile the pool already half-tracks what we need: `InstanceToKey` records every instance it
ever created, and all instances stay parented under the per-type `Pool_<T>` root. The episode
flush was asking the scene for information whose rightful owner is the pool.

## Design

1. **Non-generic `SimplePools` registry** (same file). Each `SimplePool<T>` self-registers a
   small ops handle on first use (static-init path via `GetOrCreateStack`/`EnsurePoolParent`).
   Self-registration is the structural fix for the enumeration gap: a pool that exists has
   registered, *because registering is part of coming into existence* — the list cannot rot,
   and no reflection is needed.
2. **Explicit `Live` set per pool**: add in `Get`, remove in `Release`, corpse-tolerant
   (destroyed-instance skip, mirroring the existing stack-pop guard). O(live) enumeration and
   the honest data structure for "checked out" (today it is only implied by
   `InstanceToKey ∖ stacks`).
3. **Registry surface:** `SimplePools.ForEachLive(Action<MonoBehaviour>)` and
   `SimplePools.ClearAll()`.
4. **`ProjectileFlush` consumes it**: filter `is ProjectileBase p → p.ReturnToPoolImmediate()`.
   The flush MUST keep going through the projectile's own return path (it resets projectile
   state) — the pool enumerates, the caller owns the domain action. Do not add a raw
   "release everything" that bypasses `ReturnToPoolImmediate`.
5. **Delete `SimplePoolManager`** — superseded by `SimplePools.ClearAll()` (which, unlike its
   predecessor, actually covers every pool).

## Non-goals / notes

- **Not** arena-scoped pooling. `SimplePool` statics remain the documented interim
  process-wide seam; the `Live` set rides inside it and moves with it when the multi-arena
  rethink makes pooling arena-scoped (`doc/Feature_Plans/Multi_Arena_Substrate.md` deferrals).
- Episode-purity side-thought recorded, not scoped: `ForEachLive` would also let episode reset
  flush live pooled VFX/audio, not just projectiles, if training-observation purity ever wants it.

## Tests

- EditMode: registry self-registration (touch two pool types → both enumerable); Live-set
  add/remove/corpse-tolerance; `ClearAll` covers a type the old hand-list missed.
- Existing PlayMode episode smoke already asserts zero active projectiles at episode start —
  it becomes the integration proof for the swapped flush implementation.

## Files

- `Utils/SimplePool.cs` — `SimplePools` registry, `Live` set, delete `SimplePoolManager`.
- `RLHarness/EpisodeSetup.cs` — `ProjectileFlush` swaps scan → registry.
- `Editor/Tests/EditMode/` — registry/live-set units.
