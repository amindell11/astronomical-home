using System;
using Asteroids.Fields;
using Asteroids.Fields.Core;
using Game.Services;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The canonical harness asteroid-field composition: the production <see cref="UpdatingAsteroidField"/> with streaming neutralized (static anchor, load radius covering the whole arena-sized field) and a fresh deterministic layout per episode, wired into <see cref="ArenaContext.ObstacleField"/> exactly like the sector bridge (<see cref="Game.Sectors.AsteroidFieldSpawner"/>). Hosts (tests, training scene, traversal probe) share this so the field scenario cannot drift between them.</summary>
    public sealed class HarnessField : IDisposable
    {
        private const uint FieldSeedStream = 303;

        /// <summary>Generation-time clearing carved around each episode spawn pose so a ship never wakes inside a rock.</summary>
        public const float SpawnClearRadius = 10f;

        public UpdatingAsteroidField Field { get; }

        private readonly ArenaContext arena;
        private readonly GameObject root;

        private HarnessField(ArenaContext arena, GameObject root, UpdatingAsteroidField field)
        {
            this.arena = arena;
            this.root = root;
            Field = field;
        }

        /// <summary>Instantiates the field at the arena center and stages <paramref name="densityScale"/> before the field's own Start builds (the pool pre-size hint reads it). Episode 0 may see one extra build (Start auto-init + first reset rebuild) — accepted.</summary>
        public static HarnessField Spawn(ArenaContext arena, HarnessAssets assets, float densityScale = 1f, Transform parent = null,
            bool presentationEnabled = true)
        {
            var root = UnityEngine.Object.Instantiate(
                assets.FieldPrefab, GamePlane.PlanePointToWorld(arena.Offset), Quaternion.identity, parent);
            var field = root.GetComponent<UpdatingAsteroidField>();
            field.SetAnchor(null); // streams around its own transform: the whole field stays loaded
            field.SetDensityScale(densityScale);
            field.SetPresentation(presentationEnabled);
            arena.ObstacleField = field;
            return new HarnessField(arena, root, field);
        }

        /// <summary>Per-episode reset for combat episodes: the spec's density and lethality stage first (curriculum values move between episodes), poses derive, both spawn positions become generation-time clearings, and the layout seed re-derives from (runSeed, episodeIndex). The rebuild's overlay wipe IS the reset — destruction never leaks across episodes.</summary>
        public void Reset(in RewardSpec spec, int episodeIndex, in SpawnPoses poses)
        {
            Field.SetDensityScale(spec.fieldDensityScale);
            Field.SetLethalityScale(spec.collisionLethality);
            Rebuild(DeriveLayoutSeed(spec.runSeed, episodeIndex), poses.agentPos, poses.baselinePos);
        }

        /// <summary>Seed-explicit rebuild with clearings carved at the given absolute plane positions (the traversal probe sweeps layout seeds directly).</summary>
        public void Rebuild(int layoutSeed, params Vector2[] clearings)
        {
            var volumes = new ExclusionVolume[clearings.Length];
            for (var i = 0; i < clearings.Length; i++)
                volumes[i] = new ExclusionVolume { Center = clearings[i], Radius = SpawnClearRadius };
            Field.SetExclusionVolumes(volumes);
            Field.SetLayoutSeed(layoutSeed);
            // Pre-Start this is a no-op and Start builds once with the staged seed/clearings.
            Field.RebuildField();
        }

        public void SetDensityScale(float value) => Field.SetDensityScale(value);

        public static int DeriveLayoutSeed(int runSeed, int episodeIndex) =>
            new SeedScope(runSeed).Derive(FieldSeedStream).Derive((uint)episodeIndex).ToSeed();

        public void Dispose()
        {
            if (arena != null && ReferenceEquals(arena.ObstacleField, Field))
                arena.ObstacleField = null;
            if (Field) Field.DespawnAll();
            if (root) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
