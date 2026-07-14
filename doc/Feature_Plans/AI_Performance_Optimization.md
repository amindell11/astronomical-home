# AI Performance Optimization Plan

> **Profiling update (July 14, 2026):** The physics-first ordering below was a hypothesis, not a measured priority. Native Unity Profiler captures of the current eight-ship workload instead identify synchronous MPC candidate-job scheduling and main-thread completion fences as the active frame-drop source; GPU and physics were not the limiting stacks. See [Unity Profiler Frame-Drop Investigation](Unity_Profiler_Frame_Drops.md). Keep the remaining ideas as scale-out options and re-profile before implementing them.

Below is a high-impact, staged roadmap for making the AI codebase scale to hundreds of simultaneous ships.

---

## 1. Physics & Visibility Queries  (critical hot-spot)

**A. Batch all physics tests**  
• Replace every `Physics.Raycast` / `OverlapSphere` call with `RaycastCommand` / `SpherecastCommand` inside an `IJobParallelFor`.  
• The batched API removes per-call overhead and lets Burst vectorise the math.

**B. Spatial partition for enemies & asteroids**  
• Maintain a simple grid (`NativeMultiHashMap<cell, entity>`) updated once per tick.  
• Each AI only queries its 3 × 3 neighbouring cells instead of an N² scan.

**C. Off-screen culling**  
• Cache the camera frustum once per frame.  
• Ships outside are put into an "Asleep" state that skips obstacle scans & aiming until they re-enter.

---

## 2. Jobify & Burst-compile the math pipeline

**A. Convert `PathPlanner.Compute` & `VelocityPilot.Compute` to Burst-compiled jobs**  
Use `Unity.Mathematics float2/float3` for data.

**B. Drive them from an `IJobParallelFor`**  
Collect `Kinematics` into a `NativeArray` once, write results to another array.

**C. Apply outputs through a `TransformAccessArray` job**  
Only the final write touches main-thread `ShipController`s.

**D. Eliminate managed allocations inside jobs**  
Use `NativeList` / pre-sized `NativeArray`s pooled centrally.

---

## 3. Remove per-frame managed allocations ✅ **COMPLETE (Jun 22)**

✅ Swap to `Physics.OverlapSphereNonAlloc` with pooled collider arrays.  
✅ Replace string tag compares with layer masks or small enums.  
✅ Turn the `navPoint` GameObject into a pure struct waypoint.  
✅ Wrap `collidingFutures` (debug list) with `#if UNITY_EDITOR`.

---

## 4. Lower tick-rate of expensive logic

• Cache LOS for 10+ physics frames instead of 2–5.  
• Evaluate Behaviour Trees at 5–10 Hz via a timer gate.  
• Perform obstacle scans every N physics ticks, staggered across instances:  
  `if ((Time.frameCount + shipID) % 4 != 0) return;`

---

## 5. Tighten MonoBehaviour touch-points

• Cache `Camera.main` at startup.  
• Make layer masks `readonly static`.  
• Remove all `Debug.Log` calls from runtime paths.

---

## 6. Scale-out architecture (optional, huge on > 200 AI)

• Port the AI stack to DOTS Entities:  
  – `ShipKinematics`, `PilotInput`, `WeaponRequest` as ECS components.  
  – One Burst system updates all pilots in parallel.  
  – Render with Hybrid Renderer or a simple Transform sync.

• Consider GPU-based avoidance if thousands of projectiles/asteroids are required.

---

### Quick-start Checklist

☑ Profile: If `Physics.RaycastNonAlloc` > 50 % frame time → optimise first.  
☑ Jobify `PathPlanner` & `VelocityPilot` (1–2 days).  
☑ Introduce `RaycastCommand` batches (½ day).  
☑ Drop BehaviourTree tick-rate to 5 Hz (minutes).  
☑ Remove `Debug.Log` / string tag compares (minutes).  
☑ Object-pool remaining lists/arrays (≈1 hour).

Implementing the shaded items above typically raises the cap from ~50 AI to 300+ at 60 fps on mid-range hardware, leaving headroom for visuals.
