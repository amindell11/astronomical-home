using System;
using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields;
using Cameras;
using Game.Sectors.Utils;
using Objectives;
using Objectives.States;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Combat sector: loads a scene, spawns player + enemies, sets extraction objective.
    /// Fully event-based — no Update loop. KeyPickup and ExtractionZone use trigger
    /// collisions, ObjectiveService self-ticks. Side effects (chaser spawn, restart)
    /// are bundled into state onEnter callbacks.
    /// </summary>
    public class CombatSectorManager : SectorManager
    {
        [Header("Combat Settings")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private ShipSettings shipSettings;
        [SerializeField] private Ships.Command.Commander playerCommander;
        [SerializeField] private Ships.Command.Commander enemyCommander;

        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;

        [Header("Objective Prefabs")]
        [SerializeField] private KeyPickup keyPickupPrefab;
        [SerializeField] private ExtractionZone extractionZonePrefab;

        [Header("Objective Positions (Plane Space)")]
        [SerializeField] private Vector2 keySpawnPosition = Vector2.zero;
        [SerializeField] private Vector2 extractionZonePosition = new Vector2(50f, 50f);

        [Header("Spawn Positions (Plane Space)")]
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;
        [SerializeField] private Vector2 enemySpawnPosition = new Vector2(0f, 50f);
        [SerializeField] private Vector2 chaserSpawnPosition = new Vector2(50f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship player, enemy, chaser;
        private UpdatingAsteroidField updatingAsteroidFieldInstance;
        private KeyPickup keyPickupInstance;
        private ExtractionZone extractionZoneInstance;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            player = SectorUtils.BuildAndWirePlayer(playerTemplate, playerCommander, shipSettings, 0, playerSpawnPosition, Services);
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

            yield return null;
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
            // Instantiate objective prefabs
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
        }

        private void RestartEncounter()
        {
            CompleteSector(SectorResult.Extracted());
        }

        protected override IEnumerator OnTeardown()
        {
            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();
            Services.ObjectiveService.Clear();

            if (keyPickupInstance) Destroy(keyPickupInstance.gameObject);
            if (extractionZoneInstance) Destroy(extractionZoneInstance.gameObject);
            if (updatingAsteroidFieldInstance) Destroy(updatingAsteroidFieldInstance.gameObject);

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            player = null;
            enemy = null;
            chaser = null;
            keyPickupInstance = null;
            extractionZoneInstance = null;
            updatingAsteroidFieldInstance = null;

            yield return null;
        }
    }
}
