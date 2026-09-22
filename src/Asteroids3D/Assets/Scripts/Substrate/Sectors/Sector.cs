using System;
using System.Collections;
using System.Collections.Generic;
using Substrate.Services.Units;
using Substrate.Services.Objectives;
using Substrate.Sessions;
using Ships;
using UnityEngine;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Activation;

namespace Substrate.Sectors
{
    /// <summary>The single concrete play-sector: owns manifest content + modules; the hero is session-lifetime and injected by the host through <see cref="Initialize"/>. Combat/Arena/Testbench are prefabs differing only in manifest.</summary>
    public class Sector : MonoBehaviour, ISector
    {
        public event Action<SectorResult> OnSectorComplete;

        [Header("Manifest — press Sync in the inspector to reconcile with placed children")]
        [Tooltip("Hand-placed ship children wired into the unit service in place at load.")]
        [SerializeField] private AdoptedShip[] adopted = Array.Empty<AdoptedShip>();

        [Tooltip("Procedural spawner children (e.g. RingSpawner) built in list order at load.")]
        [SerializeField] private SectorSpawner[] spawners = Array.Empty<SectorSpawner>();

        [Tooltip("Behavior modules (root components) set up after content in list order; teardown reverse.")]
        [SerializeField] private SectorModule[] modules = Array.Empty<SectorModule>();

        [Tooltip("The authored asteroid field AI ships sense in this sector (none = a world with no rocks).")]
        [SerializeField] private Asteroids.Fields.UpdatingAsteroidField obstacleField;

        [Tooltip("The authored start point marker (none = the sector begins at its root).")]
        [SerializeField] private StartPointMarker startPointMarker;

        protected IUnitService Units { get; private set; }
        protected IObjectiveService Objectives { get; private set; }
        protected bool PresentationEnabled { get; private set; }
        protected SectorSettings Config { get; private set; }
        protected bool IsSetUp { get; private set; }
        protected SectorBuildContext Context { get; private set; }

        /// <summary>Baked adopt manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<AdoptedShip> Adopted => adopted;

        /// <summary>Baked spawner manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<SectorSpawner> Spawners => spawners;

        /// <summary>Baked module manifest (read-only view for editor/tests).</summary>
        public IReadOnlyList<SectorModule> Modules => modules;

        /// <summary>The baked obstacle field AI ships spawned into this sector sense; null for a sector without rocks.</summary>
        public AI.Scanning.IObstacleField ObstacleField => obstacleField ? obstacleField : null;

        /// <summary>Plane-space point the sector begins at: the baked marker, or the sector root. The sector only declares it; the session tier resets the hero there.</summary>
        public Vector2 StartPoint =>
            GamePlane.WorldPointToPlane((startPointMarker ? startPointMarker.transform : transform).position);

        public void Initialize(IUnitService units, IObjectiveService objectives, bool presentationEnabled,
            SectorSettings config, SessionFrame frame, Ship hero)
        {
            Units = units ?? throw new ArgumentNullException(nameof(units));
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            PresentationEnabled = presentationEnabled;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Context = new SectorBuildContext(Units, Objectives, PresentationEnabled, this, frame, ObstacleField, hero);
        }

        public IEnumerator Setup()
        {
            // Fresh bus each cycle so a restart never sees stale latched tokens (episode-reset requirement).
            Context = new SectorBuildContext(Units, Objectives, PresentationEnabled, this, Context.Frame,
                Context.Field, Context.Hero, new SectorEventBus());

            foreach (var t in adopted)
                AdoptShip(t);

            foreach (var t in spawners)
                if (t) yield return t.Build(Context);

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

            for (var i = spawners.Length - 1; i >= 0; i--)
                if (spawners[i]) yield return spawners[i].Teardown(Context);

            // Despawn adopted ships so NPCs don't accumulate across restarts.
            foreach (var entry in adopted)
                if (entry.target) Units.DespawnShip(entry.target);
        }

        protected void CompleteSector(SectorResult result)
        {
            OnSectorComplete?.Invoke(result);
        }

        private void AdoptShip(AdoptedShip entry)
        {
            var ship = entry.target;
            if (!ship) return;

            ship.teamNumber = entry.team;
            var adoptedShip = Units.AdoptShip(ship, Context.Field);
            if (!adoptedShip) return;
            Respawn.Wire(adoptedShip, entry.respawn, Units);
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
            startPointMarker = result.StartPointMarker;
            return result;
        }

        /// <summary>Editor-only read-only drift check against the live hierarchy.</summary>
        public SectorManifestSync.DriftReport ComputeDrift() =>
            SectorManifestSync.ComputeDrift(transform, adopted, spawners, modules, obstacleField, startPointMarker);

        /// <summary>Test/editor seam mirroring what the inspector Sync writes; null arguments leave that slice untouched.</summary>
        internal void SetManifest(AdoptedShip[] adopted, SectorSpawner[] spawners, SectorModule[] modules,
            Asteroids.Fields.UpdatingAsteroidField obstacleField = null)
        {
            if (adopted != null) this.adopted = adopted;
            if (spawners != null) this.spawners = spawners;
            if (modules != null) this.modules = modules;
            if (obstacleField) this.obstacleField = obstacleField;
        }
#endif
    }
}
