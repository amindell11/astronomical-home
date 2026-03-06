using System;
using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields;
using Objectives;
using Objectives.States;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Combat sector: spawns enemies, sets extraction objective.
    /// Player, camera, and UI overlay are handled by PlaySector.
    /// </summary>
    public class CombatSectorManager : PlaySector
    {
        [Header("Combat Settings")]
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private Ships.Command.Commander enemyCommander;

        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        [Header("Objective Prefabs")]
        [SerializeField] private KeyPickup keyPickupPrefab;
        [SerializeField] private ExtractionZone extractionZonePrefab;

        [Header("Objective Positions (Plane Space)")]
        [SerializeField] private Vector2 keySpawnPosition = Vector2.zero;
        [SerializeField] private Vector2 extractionZonePosition = new Vector2(50f, 50f);

        [Header("Enemy Spawn (Plane Space)")]
        [SerializeField] private Vector2 enemySpawnPosition = new Vector2(0f, 50f);
        [SerializeField] private Vector2 chaserSpawnPosition = new Vector2(50f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship enemy, chaser;
        private UpdatingAsteroidField updatingAsteroidFieldInstance;
        private KeyPickup keyPickupInstance;
        private ExtractionZone extractionZoneInstance;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            yield return base.OnSetup();

            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(enemySpawnPosition), Quaternion.identity);
            chaser = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(chaserSpawnPosition), Quaternion.identity);
            chaser.gameObject.SetActive(false);

            InitializeAsteroidField();

            // Player death → fail the objective immediately
            player.Damage.OnDeath += (_, __) => Services.ObjectiveService.Fail();

            if (spawnerSettings)
            {
                enemy.Damage.OnDeath += (s1, _) =>
                    Services.UnitService.WaitAndRespawnShip(s1,
                        Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                        0, spawnerSettings.enemyRespawnDelay);
            }

            // Chaser always respawns near the world center so it can re-engage the player.
            // This is wired unconditionally: without it, killing the chaser would permanently
            // deactivate it (HandleShipDeath calls SetActive(false) with no reactivation path).
            var respawnDelay = spawnerSettings ? spawnerSettings.enemyRespawnDelay : 3f;
            chaser.Damage.OnDeath += (s1, _) =>
                Services.UnitService.WaitAndRespawnShip(s1,
                    Random.insideUnitCircle * (spawnerSettings ? spawnerSettings.offscreenDistance : 30f)
                        + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                    0, respawnDelay);

            ObjectivePhase();
        }

        private void InitializeAsteroidField()
        {
            if (!updatingAsteroidFieldPrefab)
                return;

            var cullingBoundary = Services.EnvironmentService.World
                ? Services.EnvironmentService.World.AsteroidCullingBoundary
                : null;

            if (!cullingBoundary)
            {
                Debug.LogWarning("Updating asteroid field prefab is assigned, but no AsteroidCullingBoundary exists in the spawned world.");
                return;
            }

            updatingAsteroidFieldInstance = Instantiate(updatingAsteroidFieldPrefab);
            updatingAsteroidFieldInstance.Initialize(cullingBoundary);
            updatingAsteroidFieldInstance.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            updatingAsteroidFieldInstance.CurrentAnchorPos = () =>
                player ? GamePlane.ProjectOntoPlane(player.transform.position) : updatingAsteroidFieldInstance.transform.position;
        }

        private void ObjectivePhase()
        {
            if (keyPickupPrefab)
            {
                var keyWorld = GamePlane.PlanePointToWorld(keySpawnPosition);
                keyPickupInstance = Instantiate(keyPickupPrefab, keyWorld, keyPickupPrefab.transform.rotation);
                keyPickupInstance.SpawnKey(keyWorld);
            }

            if (extractionZonePrefab)
            {
                extractionZoneInstance = Instantiate(extractionZonePrefab, GamePlane.PlanePointToWorld(extractionZonePosition), extractionZonePrefab.transform.rotation);
                extractionZoneInstance.Initialize(chaser ? chaser.transform : null);
            }

            if (chaser)
                chaser.gameObject.SetActive(false);

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(keyPickupInstance),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(
                    extractionZoneInstance,
                    onEnter: () => { if (chaser) chaser.gameObject.SetActive(true); }),
                ["extracted"] = () => new ExtractedState(onEnter: ()=>CompleteSector(SectorResult.Extracted())),
                ["failed"] = () => new FailedState(onEnter: ()=>CompleteSector(SectorResult.Failed("failed")))
            };

            Services.ObjectiveService.SetObjective(MissionDefinition.CreateDefault(), builders);

            WireObjectiveMarker();
        }

        private void WireObjectiveMarker()
        {
            var overlay = Services.UIService.ActiveOverlay;
            if (!overlay || !overlay.ObjectiveMarker) return;

            var marker = overlay.ObjectiveMarker;

            SetMarkerTarget(marker, Services.ObjectiveService.CurrentState ?? ObjectiveType.Explore);
            Services.ObjectiveService.OnStateChanged += (_, to) => SetMarkerTarget(marker, to);
        }

        private void SetMarkerTarget(UI.MinimapObjectiveMarker marker, ObjectiveType state)
        {
            switch (state)
            {
                case ObjectiveType.Explore:
                    marker.SetTarget(keyPickupInstance ? keyPickupInstance.transform : null);
                    break;
                case ObjectiveType.KeyAcquired:
                case ObjectiveType.ExtractionChallenge:
                    marker.SetTarget(extractionZoneInstance ? extractionZoneInstance.transform : null);
                    break;
                default:
                    marker.SetTarget(null);
                    break;
            }
        }

        private void RestartEncounter()
        {
            CompleteSector(SectorResult.Extracted());
        }

        protected override IEnumerator OnTeardown()
        {
            Services.ObjectiveService.Clear();

            if (keyPickupInstance) Destroy(keyPickupInstance.gameObject);
            if (extractionZoneInstance) Destroy(extractionZoneInstance.gameObject);
            if (updatingAsteroidFieldInstance) Destroy(updatingAsteroidFieldInstance.gameObject);

            yield return base.OnTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            enemy = null;
            chaser = null;
            keyPickupInstance = null;
            extractionZoneInstance = null;
            updatingAsteroidFieldInstance = null;
        }
    }
}
