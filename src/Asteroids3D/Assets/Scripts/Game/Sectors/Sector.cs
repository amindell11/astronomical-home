using System;
using System.Collections;
using System.Collections.Generic;
using Game.Services;
using Ships;
using UnityEngine;
using World;

namespace Game.Sectors
{
    /// <summary>
    /// The single concrete play-sector. Owns manifest content (adopted ships, procedural spawners —
    /// including any asteroid field, which is just another spawner) and behavior modules. The player,
    /// observer camera, UI overlay and world are built once by the session-tier
    /// <see cref="Game.Bootstrap.PlayerRig"/> and injected
    /// via <see cref="Initialize"/> — the sector references the player, it does not own it.
    /// Combat / Arena / Testbench are prefabs of this class, differing only in their manifest, modules
    /// and producer-owned RespawnPolicies.
    /// </summary>
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

        /// <summary>
        /// Plane-space player start declared by an optional <see cref="PlayerStartMarker"/> child, or
        /// null if none is placed. The sector only DECLARES this; the session tier resets the injected
        /// player on entry. The sector never repositions the player itself.
        /// </summary>
        public Vector2? PlayerStart
        {
            get
            {
                var marker = GetComponentInChildren<PlayerStartMarker>(true);
                return marker ? GamePlane.WorldPointToPlane(marker.transform.position) : (Vector2?)null;
            }
        }

        public void Initialize(IGameServices services, SectorSettings config, Ship player)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            // Player is injected from the session-tier rig (may be null for headless/RL runs).
            Context = new SectorBuildContext(Services, this, player);
        }

        public IEnumerator Setup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);

            // The session rig (world, player, camera, UI) is already built and the player is injected
            // via Initialize. Manifest content adopts here in list order. OnBeforeContent is a
            // vestigial extension hook (used by test subclasses); the base body is empty.
            yield return OnBeforeContent();

            foreach (var t in adopted)
                Adopt(t);

            // Build procedural spawners in manifest order.
            foreach (var t in spawners)
                if (t) yield return t.Build(Context);

            yield return OnAfterContent();

            // Behavior modules run after all content. The context already carries the injected player
            // (set in Initialize) so modules can read ctx.Player; everything else is a dragged
            // serialized reference. After building each module, auto-subscribe its end signal to the
            // sector's completion sink (a module that never raises never ends the sector).
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

            // Tear down modules in reverse before the subclass teardown hook; unsubscribe the
            // auto-wired completion signal so a torn-down module cannot end the sector.
            for (var i = modules.Length - 1; i >= 0; i--)
            {
                var m = modules[i];
                if (!m) continue;
                m.SectorEndRequested -= CompleteSector;
                yield return m.Teardown(Context);
            }

            yield return OnBeforeTeardown();

            // Tear down loose/spawned instances in reverse. The persistent rig (player/camera/UI/world)
            // is NOT touched here — it survives sector restarts and is torn down only on session exit.
            for (var i = spawners.Length - 1; i >= 0; i--)
                if (spawners[i]) yield return spawners[i].Teardown(Context);

            yield return OnAfterTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);
        }

        protected void CompleteSector(SectorResult result)
        {
            OnSectorComplete?.Invoke(result);
        }

        // ── Adopt type-dispatch ────────────────────────────────────────────────
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
            var adoptedShip = Services.UnitService.AdoptShip(ship);
            if (!adoptedShip) return;
            // Producer-owned respawn: additive — default policy (origin None) wires nothing, so
            // adopted ships in today's prefabs behave identically.
            Respawn.Wire(adoptedShip, entry.respawn, Services);
            if (!entry.startActive) adoptedShip.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: reconcile the serialized manifest with the live child hierarchy.
        /// Returns the reconcile result (counts of appended/orphaned). Caller is responsible for
        /// marking the object dirty / recording undo.
        /// </summary>
        public SectorManifestSync.ReconcileResult SyncManifest()
        {
            var result = SectorManifestSync.Reconcile(transform, adopted, spawners, modules);
            adopted = result.Adopted;
            spawners = result.Spawners;
            modules = result.Modules;
            return result;
        }

        /// <summary>Editor-only read-only drift check against the live hierarchy.</summary>
        public SectorManifestSync.DriftReport ComputeDrift() =>
            SectorManifestSync.ComputeDrift(transform, adopted, spawners, modules);
#endif

        protected virtual IEnumerator OnBeforeContent() { yield break; }
        protected virtual IEnumerator OnAfterContent() { yield break; }
        protected virtual IEnumerator OnBeforeTeardown() { yield break; }
        protected virtual IEnumerator OnAfterTeardown() { yield break; }
    }
}
