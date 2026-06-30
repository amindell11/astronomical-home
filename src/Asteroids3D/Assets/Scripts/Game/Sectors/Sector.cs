using System;
using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields;
using Game.Services;
using Ships;
using UnityEngine;
using World;

namespace Game.Sectors
{
    public abstract class Sector : MonoBehaviour, ISector
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

        public void Initialize(IGameServices services, SectorSettings config)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Context = new SectorBuildContext(Services, this);
        }

        public IEnumerator Setup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);

            // Infrastructure (world, player, camera, UI) builds first in OnBeforeContent; manifest
            // content adopts afterwards in list order, so ships register after the observer cam has
            // subscribed and the asteroid field can read the now-built world + anchor to the player.
            yield return OnBeforeContent();

            foreach (var t in adopted)
                Adopt(t);

            // Build procedural spawners in manifest order.
            foreach (var t in spawners)
                if (t) yield return t.Build(Context);

            yield return OnAfterContent();

            // Behavior modules run after all content. Rebuild the context with the now-built player
            // (PlaySector supplies it) so modules can read ctx.Player; everything else is a dragged
            // serialized reference. After building each module, auto-subscribe its end signal to the
            // sector's completion sink (a module that never raises never ends the sector).
            Context = new SectorBuildContext(Services, this, GetSectorPlayer());
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

            // Tear down loose/spawned instances in reverse. Service-owned ships/world/UI are
            // destroyed by services.Clear(); keep the two disjoint (no double-destroy).
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
                case UpdatingAsteroidField field: AdoptField(field); break;
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

        private void AdoptField(UpdatingAsteroidField field)
        {
            var world = Services.EnvironmentService.World;
            var cullingBoundary = world ? world.AsteroidCullingBoundary : null;
            if (!cullingBoundary)
                return;

            field.Initialize(cullingBoundary);
            field.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            field.CurrentAnchorPos = () => GetContentAnchorWorldPos(field.transform.position);
        }

        /// <summary>
        /// World-space anchor used by adopted asteroid fields. Base returns the field's own
        /// position; <c>PlaySector</c> overrides to track the player.
        /// </summary>
        protected virtual Vector3 GetContentAnchorWorldPos(Vector3 fallback) => fallback;

        /// <summary>
        /// Runtime player ship exposed to modules via <see cref="SectorBuildContext.Player"/>. Base
        /// has no player; <c>PlaySector</c> overrides to return the ship it built.
        /// </summary>
        protected virtual Ship GetSectorPlayer() => null;

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
