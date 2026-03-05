using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private Vector2 enemySpawnOffset = new Vector2(0f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship player, enemy, chaser;
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
            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(playerSpawnPosition + enemySpawnOffset), Quaternion.identity);
            chaser = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(playerSpawnPosition + enemySpawnOffset), Quaternion.identity);
            chaser.gameObject.SetActive(false);

            // Player death → fail the objective immediately
            player.Damage.OnDeath += (_, __) => Services.ObjectiveService.Fail();

            if (spawnerSettings)
            {
                enemy.Damage.OnDeath += (s1, _) =>
                    Services.UnitService.WaitAndRespawnShip(s1,
                        Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                        0, spawnerSettings.enemyRespawnDelay);

                chaser.Damage.OnDeath += (s1, _) =>
                    Services.UnitService.WaitAndRespawnShip(s1,
                        Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                        0, spawnerSettings.enemyRespawnDelay);
            }

            ObjectivePhase();

            yield return null;
        }

        private void ObjectivePhase()
        {
            // Instantiate objective prefabs
            if (keyPickupPrefab)
            {
                var keyWorld = GamePlane.PlanePointToWorld(keySpawnPosition);
                keyPickupInstance = Instantiate(keyPickupPrefab, keyWorld, Quaternion.identity);
                keyPickupInstance.SpawnKey(keyWorld);
            }

            if (extractionZonePrefab)
            {
                extractionZoneInstance = Instantiate(extractionZonePrefab, GamePlane.PlanePointToWorld(extractionZonePosition), Quaternion.identity);
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
                ["extracted"] = () => new ExtractedState(onEnter: RestartEncounter),
                ["failed"] = () => new FailedState(onEnter: RestartEncounter)
            };

            Services.ObjectiveService.SetObjective(MissionDefinition.CreateDefault(), builders);
        }

        private void RestartEncounter()
        {
            if (keyPickupInstance) keyPickupInstance.ResetKey(GamePlane.PlanePointToWorld(keySpawnPosition));
            if (chaser) chaser.gameObject.SetActive(false);
            if (player && !player.gameObject.activeSelf)
            {
                player.transform.position = GamePlane.PlanePointToWorld(playerSpawnPosition);
                player.ResetShip();
            }
            Services.ObjectiveService.Restart();
        }

        protected override IEnumerator OnTeardown()
        {
            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();
            Services.ObjectiveService.Clear();

            if (keyPickupInstance) Destroy(keyPickupInstance.gameObject);
            if (extractionZoneInstance) Destroy(extractionZoneInstance.gameObject);

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            player = null;
            enemy = null;
            keyPickupInstance = null;
            extractionZoneInstance = null;

            yield return null;
        }
    }
}
