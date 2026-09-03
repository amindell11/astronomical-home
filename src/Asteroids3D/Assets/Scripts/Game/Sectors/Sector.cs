using System;
using System.Collections;
using System.Collections.Generic;
using Game.Services;
using Ships;
using UnityEngine;
using World;

namespace Game.Sectors
{
    /// <summary>The single concrete play-sector: owns manifest content + modules; player/camera/world are session-tier, injected via Initialize. Combat/Arena/Testbench are prefabs differing only in manifest.</summary>
    public class Sector : MonoBehaviour, ISector
    {
        public event Action<SectorResult> OnSectorComplete;

        [Header("Manifest — press Sync in the inspector to reconcile with placed children")]
        [Tooltip("Hand-placed content children wired into services in place at load.")]
        [SerializeField] private AdoptEntry[] adopted = Array.Empty<AdoptEntry>();

        [Tooltip("Procedural spawner children (e.g. RingSpawner) built in list order at load.")]
        [SerializeField] private SectorSpawner[] spawners = Array.Empty<SectorSpawner>();

        [Tooltip("Behavior modules (root components) set up after content in list order; teardown reverse.")]
        [SerializeField] private SectorModule[] modules = Array.Empty<SectorModule>();

        [Tooltip("The authored asteroid field AI ships sense in this sector (none = a world with no rocks).")]
        [SerializeField] private Asteroids.Fields.UpdatingAsteroidField obstacleField;

        protected IGameServices Services { get; private set; }
        protected SectorSettings Config { get; private set; }
        protected bool IsSetUp { get; private set; }
        protected SectorBuildContext Context { get; private set; }

        /// <summary>Baked adopt manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<AdoptEntry> Adopted => adopted;

        /// <summary>Baked spawner manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<SectorSpawner> Spawners => spawners;

        /// <summary>Baked module manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<SectorModule> Modules => modules;

        /// <summary>The baked obstacle field the session host builds this sector's <see cref="WorldHandle"/> from; null for a sector without rocks.</summary>
        public AI.Scanning.IObstacleField ObstacleField => obstacleField ? obstacleField : null;

        /// <summary>Plane-space player start from an optional PlayerStartMarker child (sector root otherwise), recomputed each entry — the sector only declares it, the session tier does the reset.</summary>
        public Vector2 PlayerStart
        {
            get
            {
                var marker = GetComponentInChildren<PlayerStartMarker>(true);
                return GamePlane.WorldPointToPlane((marker ? marker.transform : transform).position);
            }
        }

        public void Initialize(IGameServices services, SectorSettings config, WorldHandle world, Ship player)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (world == null) throw new ArgumentNullException(nameof(world));
            Context = new SectorBuildContext(Services, this, world, player);
        }

        public IEnumerator Setup()
        {
            // Fresh bus each cycle so a restart never sees stale latched tokens (episode-reset requirement).
            Context = new SectorBuildContext(Services, this, Context.World, Context.Player, new SectorEventBus());

            yield return OnBeforeContent();

            foreach (var t in adopted)
                Adopt(t);

            foreach (var t in spawners)
                if (t) yield return t.Build(Context);

            yield return OnAfterContent();

            foreach (var m in modules)
            {
                if (!m) continue;
                yield return m.Setup(Context);
                m.SectorEndRequested += CompleteSector;
            }

            IsSetUp = true;
        }

        public IEnumerator Teardown()
        {
            IsSetUp = false;
            // Freeze before module teardown: modules dismantle sequentially and no rule may fire mid-teardown.
            Context.Bus?.Freeze();

            for (var i = modules.Length - 1; i >= 0; i--)
            {
                var m = modules[i];
                if (!m) continue;
                m.SectorEndRequested -= CompleteSector;
                yield return m.Teardown(Context);
            }

            yield return OnBeforeTeardown();

            for (var i = spawners.Length - 1; i >= 0; i--)
                if (spawners[i]) yield return spawners[i].Teardown(Context);

            // Despawn adopted ships so NPCs don't accumulate across restarts; non-ship adopts (WorldRoot) are session infra, deliberately left alone.
            foreach (var entry in adopted)
                if (entry.target is Ship ship) Services.UnitService.DespawnShip(ship);

            yield return OnAfterTeardown();
        }

        protected void CompleteSector(SectorResult result)
        {
            OnSectorComplete?.Invoke(result);
        }

        private void Adopt(AdoptEntry entry)
        {
            var target = entry.target;
            if (!target) return;

            switch (target)
            {
                case Ship ship: AdoptShip(ship, entry); break;
                case WorldRoot world: Services.EnvironmentService.AdoptWorld(world); break;
                default:
                    break;
            }
        }

        private void AdoptShip(Ship ship, AdoptEntry entry)
        {
            ship.teamNumber = entry.team;
            var adoptedShip = Services.UnitService.AdoptShip(ship, Context.World);
            if (!adoptedShip) return;
            Respawn.Wire(adoptedShip, entry.respawn, Services, Context.World.Offset);
            if (!entry.startActive) adoptedShip.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: reconcile the serialized manifest with the live child hierarchy; caller marks dirty / records undo.</summary>
        public SectorManifestSync.ReconcileResult SyncManifest()
        {
            var result = SectorManifestSync.Reconcile(transform, adopted, spawners, modules);
            adopted = result.Adopted;
            spawners = result.Spawners;
            modules = result.Modules;
            obstacleField = result.ObstacleField;
            return result;
        }

        /// <summary>Editor-only read-only drift check against the live hierarchy.</summary>
        public SectorManifestSync.DriftReport ComputeDrift() =>
            SectorManifestSync.ComputeDrift(transform, adopted, spawners, modules);

        /// <summary>Test/editor seam mirroring what the inspector Sync writes; null arguments leave that slice untouched.</summary>
        internal void SetManifest(AdoptEntry[] adopted, SectorSpawner[] spawners, SectorModule[] modules,
            Asteroids.Fields.UpdatingAsteroidField obstacleField = null)
        {
            if (adopted != null) this.adopted = adopted;
            if (spawners != null) this.spawners = spawners;
            if (modules != null) this.modules = modules;
            if (obstacleField) this.obstacleField = obstacleField;
        }
#endif

        protected virtual IEnumerator OnBeforeContent() { yield break; }
        protected virtual IEnumerator OnAfterContent() { yield break; }
        protected virtual IEnumerator OnBeforeTeardown() { yield break; }
        protected virtual IEnumerator OnAfterTeardown() { yield break; }
    }
}
