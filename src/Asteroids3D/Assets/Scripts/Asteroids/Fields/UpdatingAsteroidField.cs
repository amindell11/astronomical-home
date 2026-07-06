using System;
using System.Collections.Generic;
using Asteroids.Fields.Core;
using Asteroids.Spawning;
using Game;
using UnityEngine;

namespace Asteroids.Fields
{
    /// <summary>
    /// Deterministic, persistent asteroid field: a chunk-streaming manager
    /// over the procedural baseline LUT (<see cref="AsteroidFieldLayout"/>)
    /// plus the sparse override overlay (<see cref="AsteroidFieldOverlay"/>).
    /// A given spot always holds the same asteroids; destruction and damage
    /// persist for the session, everything else regenerates from the seed.
    /// (Class name kept from the old annulus spawner to avoid churn across
    /// GameConfig / SectorManifestSync / AdoptEntry / AsteroidFieldSpawner.)
    /// </summary>
    public partial class UpdatingAsteroidField : AsteroidField, AI.Scanning.IObstacleField
    {
        [SerializeField] private World.WorldFollow follower;

        [Tooltip("Authored layout seed for this placed field. Two sectors sharing the same settings asset still get distinct layouts by varying this.")]
        [SerializeField] private int seed = 12345;

        [Header("Debug Visualization (editor gizmos)")]
        [SerializeField] private bool drawChunkGizmos = true;
        [SerializeField] private bool drawNoiseHeatmap = true;

        public Func<Vector3> CurrentAnchorPos { private get; set; }

        /// <summary>Headless core, exposed for tests and tooling.</summary>
        public AsteroidFieldModel Model { get; private set; }

        private Transform playerAnchor;
        private Vector2? playerStartPlane;
        private ChunkStreamer streamer;
        private Vector2 fieldOriginPlane;
        private bool initialized;
        private int appliedSettingsVersion;
        private bool rebuildRequested;

        // Cached settings
        protected float loadRadius;
        protected float unloadRadius;
        private int maxSpawnsPerFrame;

        // Streaming state
        private readonly Queue<Vector2Int> loadQueue = new();
        private readonly HashSet<Vector2Int> queuedChunks = new();
        private readonly List<FieldAsteroidSpec> chunkSpecBuffer = new();
        private int chunkSpecCursor;
        private Vector2Int inProgressChunk;
        private bool hasInProgressChunk;
        private readonly List<Vector2Int> toLoadBuffer = new();
        private readonly List<Vector2Int> toUnloadBuffer = new();

        // Live-asteroid bookkeeping (all pooled lookups; no per-frame GetComponent)
        private readonly Dictionary<AsteroidController, AsteroidId> idOf = new();
        private readonly Dictionary<AsteroidController, Vector2Int> homeChunkOf = new();
        private readonly Dictionary<Vector2Int, List<AsteroidController>> spawnedByChunk = new();
        // Pooled controllers are hooked once for their lifetime; handlers no-op when untracked.
        private readonly HashSet<AsteroidController> destructionHooked = new();

        /// <summary>
        /// Stash the injected player anchor (may be null for spectator/headless runs) and wire the
        /// follower to it. Called from <see cref="Game.Sectors.AsteroidFieldSpawner"/> during
        /// <c>Build</c>, before this component's own <c>Awake</c>/<c>Start</c> have run.
        /// </summary>
        public void SetPlayer(Transform player)
        {
            playerAnchor = player;
            if (follower) follower.SetTarget(player);
            CurrentAnchorPos = () => playerAnchor ? GamePlane.ProjectOntoPlane(playerAnchor.position) : transform.position;
        }

        /// <summary>
        /// Stash the sector's static authored player start (absolute plane
        /// space) so generation carves a permanent clearing around it. Called
        /// from <see cref="Game.Sectors.AsteroidFieldSpawner"/> during
        /// <c>Build</c>, like <see cref="SetPlayer"/>. Only static authored
        /// positions may feed the layout — dynamic occupancy must never bake
        /// into the deterministic baseline.
        /// </summary>
        public void SetPlayerStart(Vector2 absolutePlanePosition) => playerStartPlane = absolutePlanePosition;

        /// <summary>
        /// Benchmark/tooling hook: override the authored layout seed. Takes effect on the next
        /// <c>InitializeField</c> — call it before the field's <c>Start</c> runs (e.g. from a
        /// sector module's Setup, while the sector is still under its inactive load holder) or
        /// follow it with <see cref="RebuildField"/> on a live field.
        /// </summary>
        public void SetLayoutSeed(int value) => seed = value;

        protected override void CacheSettings()
        {
            base.CacheSettings();

            if (!settings) return;

            loadRadius = settings.loadRadius;
            unloadRadius = settings.UnloadRadius;
            maxSpawnsPerFrame = settings.maxSpawnsPerFrame;
        }

        protected virtual void Start()
        {
            // Anchor the spawner BEFORE the initial fill: each asteroid captures the spawner's
            // world-anchor at spawn time (used for mesh-collider LOD).
            SetWorldAnchor(follower ? follower.transform : null);
            CurrentAnchorPos ??= () => transform.position;

            InitializeField();
        }

        private void InitializeField()
        {
            CacheSettings();

            var spawnSettings = AsteroidSpawner ? AsteroidSpawner.Settings : null;
            if (!settings || !spawnSettings || spawnSettings.meshInfos is not { Length: > 0 }) return;

            var meshVolumes = new float[spawnSettings.meshInfos.Length];
            for (var i = 0; i < meshVolumes.Length; i++)
                meshVolumes[i] = spawnSettings.meshInfos[i].cachedVolume;

            fieldOriginPlane = GamePlane.WorldPointToPlane(transform.position);

            var genParams = settings.BuildGenerationParams();
            genParams.MeshVolumes = meshVolumes;
            genParams.MeshDensity = spawnSettings.density;
            genParams.MassScaleRange = spawnSettings.massScaleRange;
            genParams.VelocityRange = spawnSettings.velocityRange;
            genParams.SpinRange = spawnSettings.spinRange;

            // The player start is static and authored, so it is safe to bake as
            // a generation-time cull (a persistent clearing). Spec positions are
            // field-relative; the start arrives in absolute plane space.
            if (playerStartPlane.HasValue && settings.startClearRadius > 0f)
                genParams.ExclusionVolumes = new[]
                {
                    new ExclusionVolume { Center = playerStartPlane.Value - fieldOriginPlane, Radius = settings.startClearRadius }
                };

            var layout = new AsteroidFieldLayout(seed, genParams);
            Model = new AsteroidFieldModel(layout);
            streamer = new ChunkStreamer(settings.chunkSize, loadRadius, unloadRadius);

            AsteroidSpawner.PreSizePool(settings.WorstCaseLoadedCount());
            AsteroidSpawner.OnFragmentSpawned -= HandleFragmentSpawned;
            AsteroidSpawner.OnFragmentSpawned += HandleFragmentSpawned;
            appliedSettingsVersion = settings.Version;
            rebuildRequested = false;
            initialized = true;

            // Initial fill is synchronous (unbudgeted), like the old field's Start-time spawn burst.
            StepStreaming(int.MaxValue);
        }

        /// <summary>
        /// Live-tuning support: tears everything down (including the overlay —
        /// stable IDs are derived from the layout, so edits invalidate it) and
        /// regenerates from the current settings/seed. Triggered automatically
        /// when the settings asset or seed is edited in the inspector.
        /// </summary>
        [ContextMenu("Rebuild Field")]
        public void RebuildField()
        {
            if (!initialized) return;
            DespawnAll();
            initialized = false;
            InitializeField();
        }

        private void OnValidate()
        {
            // Editor-only Unity message; covers inspector edits to component
            // fields (seed, toggles). Cleared by InitializeField so the
            // load-time OnValidate never triggers a spurious rebuild.
            rebuildRequested = true;
        }

        private void OnDestroy()
        {
            if (AsteroidSpawner) AsteroidSpawner.OnFragmentSpawned -= HandleFragmentSpawned;
        }

        private void Update()
        {
            if (!initialized) return;
            if (rebuildRequested || (settings && settings.Version != appliedSettingsVersion))
            {
                RebuildField();
                return;
            }
            StepStreaming(maxSpawnsPerFrame);
        }

        public override void DespawnAll()
        {
            base.DespawnAll();
            idOf.Clear();
            homeChunkOf.Clear();
            spawnedByChunk.Clear();
            loadQueue.Clear();
            queuedChunks.Clear();
            chunkSpecBuffer.Clear();
            hasInProgressChunk = false;
            streamer?.Reset();
            Model?.Overlay.Clear();
        }

        // ── Streaming ────────────────────────────────────────────────────────────

        private void StepStreaming(int spawnBudget)
        {
            var anchor = RelativePlanePos(CurrentAnchorPos());

            toLoadBuffer.Clear();
            toUnloadBuffer.Clear();
            streamer.Update(anchor, toLoadBuffer, toUnloadBuffer);

            foreach (var chunk in toLoadBuffer)
            {
                loadQueue.Enqueue(chunk);
                queuedChunks.Add(chunk);
            }
            foreach (var chunk in toUnloadBuffer) UnloadChunk(chunk);

            ProcessLoadQueue(spawnBudget);
        }

        private void ProcessLoadQueue(int spawnBudget)
        {
            while (spawnBudget > 0 && (hasInProgressChunk || loadQueue.Count > 0))
            {
                if (!hasInProgressChunk)
                {
                    var chunk = loadQueue.Dequeue();
                    if (!queuedChunks.Remove(chunk)) continue; // cancelled by unload while queued
                    chunkSpecBuffer.Clear();
                    Model.GetChunkContents(chunk, chunkSpecBuffer);
                    chunkSpecCursor = 0;
                    inProgressChunk = chunk;
                    hasInProgressChunk = true;
                }

                while (spawnBudget > 0 && chunkSpecCursor < chunkSpecBuffer.Count)
                {
                    SpawnFromSpec(inProgressChunk, chunkSpecBuffer[chunkSpecCursor]);
                    chunkSpecCursor++;
                    spawnBudget--;
                }
                if (chunkSpecCursor >= chunkSpecBuffer.Count) hasInProgressChunk = false;
            }
        }

        private void UnloadChunk(Vector2Int chunk)
        {
            // Not yet spawned: cancel the queued load.
            if (queuedChunks.Remove(chunk)) return;

            // Mid-spawn chunk: drop the remaining specs; spawned ones unload below.
            if (hasInProgressChunk && inProgressChunk == chunk) hasInProgressChunk = false;

            if (!spawnedByChunk.TryGetValue(chunk, out var asteroids)) return;
            foreach (var ast in asteroids)
            {
                if (!ast || !idOf.TryGetValue(ast, out var id)) continue;
                idOf.Remove(ast);
                homeChunkOf.Remove(ast);

                if (id.Authored && TrySnapshotOrRehomeFragment(ast, id, chunk)) continue;
                if (!id.Authored) SnapshotBaselineDamage(ast, id);
                AsteroidSpawner.Despawn(ast);
            }
            asteroids.Clear();
        }

        /// <summary>
        /// A fragment that drifted into a still-loaded chunk stays live (re-homed);
        /// otherwise it is snapshotted at rest into the overlay and despawned.
        /// The wide hysteresis band means this freeze happens out of view.
        /// </summary>
        private bool TrySnapshotOrRehomeFragment(AsteroidController ast, AsteroidId id, Vector2Int unloadingChunk)
        {
            var planePos = RelativePlanePos(ast.transform.position);
            var currentChunk = Model.Layout.CellOf(planePos);
            if (currentChunk != unloadingChunk && streamer.IsLoaded(currentChunk))
            {
                Track(ast, id, currentChunk);
                return true;
            }

            Model.Overlay.WriteAuthored(new AuthoredAsteroid
            {
                Id = id,
                PlanePosition = planePos,
                Rotation = ast.transform.rotation,
                MeshIndex = MeshIndexOf(ast),
                Mass = ast.Mass,
                Scale = ast.transform.localScale.x,
                HealthFraction = HealthFractionOf(ast)
            });
            return false;
        }

        private void SnapshotBaselineDamage(AsteroidController ast, AsteroidId id)
        {
            Model.Overlay.RecordDamage(id, HealthFractionOf(ast));
        }

        // ── Spawning & bookkeeping ───────────────────────────────────────────────

        private void SpawnFromSpec(Vector2Int chunk, in FieldAsteroidSpec spec)
        {
            var spawnSettings = AsteroidSpawner.Settings;
            var meshInfo = spawnSettings.meshInfos[Mathf.Clamp(spec.MeshIndex, 0, spawnSettings.meshInfos.Length - 1)];
            var attrs = new AsteroidAttributes(
                meshInfo,
                spec.Mass,
                spec.Scale,
                GamePlane.PlaneDirToWorld(spec.PlaneVelocity),
                spec.AngularVelocity);

            var pose = new Pose(ToWorld(spec.PlanePosition), spec.Rotation);
            var ast = AsteroidSpawner.Spawn(pose, attrs);
            if (spec.HealthFraction < 1f) ast.Damage?.ApplyHealthFraction(spec.HealthFraction);

            Track(ast, spec.Id, chunk);
            HookDestruction(ast);
        }

        private void HandleFragmentSpawned(AsteroidController ast)
        {
            if (!initialized) return;
            var id = Model.NextAuthoredId();
            var chunk = Model.Layout.CellOf(RelativePlanePos(ast.transform.position));
            if (!streamer.IsLoaded(chunk))
            {
                // Born outside the loaded set (shouldn't happen in practice):
                // persist immediately rather than leaking an untracked live object.
                idOf[ast] = id;
                homeChunkOf[ast] = chunk;
                TrySnapshotOrRehomeFragment(ast, id, chunk);
                idOf.Remove(ast);
                homeChunkOf.Remove(ast);
                AsteroidSpawner.Despawn(ast);
                return;
            }
            Track(ast, id, chunk);
            HookDestruction(ast);
        }

        private void Track(AsteroidController ast, AsteroidId id, Vector2Int chunk)
        {
            idOf[ast] = id;
            homeChunkOf[ast] = chunk;
            if (!spawnedByChunk.TryGetValue(chunk, out var list))
                spawnedByChunk[chunk] = list = new List<AsteroidController>();
            list.Add(ast);
        }

        private void Untrack(AsteroidController ast)
        {
            if (!homeChunkOf.TryGetValue(ast, out var chunk)) return;
            idOf.Remove(ast);
            homeChunkOf.Remove(ast);
            if (spawnedByChunk.TryGetValue(chunk, out var list)) list.Remove(ast);
        }

        private void HookDestruction(AsteroidController ast)
        {
            if (!destructionHooked.Add(ast)) return;
            ast.OnDestroyed += _ => HandleAsteroidDestroyed(ast);
        }

        /// <summary>
        /// Destruction persists its outcome: baseline asteroids tombstone their
        /// ID (fragments arrive separately via <see cref="HandleFragmentSpawned"/>);
        /// destroyed fragments simply leave the overlay.
        /// </summary>
        private void HandleAsteroidDestroyed(AsteroidController ast)
        {
            if (!idOf.TryGetValue(ast, out var id)) return;
            if (id.Authored) Model.Overlay.RemoveAuthored(id);
            else Model.Overlay.Tombstone(id);
            Untrack(ast);
        }

        // ── Obstacle field query (B2) ────────────────────────────────────────────

        /// <summary>
        /// Fills <paramref name="buffer"/> with live asteroids inside a fixed-size AABB
        /// (half-extent per axis) around <paramref name="centerPlane"/>. Only spawned,
        /// non-destroyed asteroids in the loaded chunks are reported; destroyed ones have
        /// already been untracked. The chunk sweep uses the layout cell grid, then a precise
        /// per-asteroid box test rejects the corner slop.
        /// </summary>
        public int QueryObstacles(Vector2 centerPlane, float halfExtent, AI.Scanning.DetectedObstacle[] buffer)
        {
            if (!initialized || buffer == null || buffer.Length == 0) return 0;
            var relCenter = centerPlane - fieldOriginPlane;
            var cellMin = Model.Layout.CellOf(relCenter - new Vector2(halfExtent, halfExtent));
            var cellMax = Model.Layout.CellOf(relCenter + new Vector2(halfExtent, halfExtent));
            var count = 0;
            for (var cx = cellMin.x; cx <= cellMax.x && count < buffer.Length; cx++)
            for (var cy = cellMin.y; cy <= cellMax.y && count < buffer.Length; cy++)
            {
                if (!spawnedByChunk.TryGetValue(new Vector2Int(cx, cy), out var list)) continue;
                for (var i = 0; i < list.Count && count < buffer.Length; i++)
                {
                    var ast = list[i];
                    if (!ast) continue;
                    var p = GamePlane.WorldPointToPlane(ast.transform.position);
                    if (Mathf.Abs(p.x - centerPlane.x) > halfExtent || Mathf.Abs(p.y - centerPlane.y) > halfExtent) continue;
                    buffer[count++] = new AI.Scanning.DetectedObstacle(ast.transform.position, ast.Radius, ast.SimpleCollider);
                }
            }
            return count;
        }

        // ── Coordinate mapping ───────────────────────────────────────────────────

        private Vector2 RelativePlanePos(Vector3 world) =>
            GamePlane.WorldPointToPlane(world) - fieldOriginPlane;

        private Vector3 ToWorld(Vector2 relativePlanePos) =>
            GamePlane.PlanePointToWorld(relativePlanePos + fieldOriginPlane);

        private static float HealthFractionOf(AsteroidController ast)
        {
            var damage = ast.Damage;
            if (!damage || damage.MaxHealth <= 0f) return 1f;
            return Mathf.Clamp01(damage.Health / damage.MaxHealth);
        }

        private int MeshIndexOf(AsteroidController ast)
        {
            var meshInfos = AsteroidSpawner.Settings.meshInfos;
            for (var i = 0; i < meshInfos.Length; i++)
                if (meshInfos[i].mesh == ast.CurrentMesh) return i;
            return 0;
        }
    }
}
