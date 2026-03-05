using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Objective Prefabs")]
        [SerializeField] private KeyPickup keyPickupPrefab;
        [SerializeField] private ExtractionZone extractionZonePrefab;

        [Header("Objective Positions (Plane Space)")]
        [SerializeField] private Vector2 keySpawnPosition = Vector2.zero;
        [SerializeField] private Vector2 extractionZonePosition = new Vector2(50f, 50f);

        [Header("Enemy Spawn")]
        [SerializeField] private Vector2 enemySpawnOffset = new Vector2(0f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship enemy, chaser;
        private KeyPickup keyPickupInstance;
        private ExtractionZone extractionZoneInstance;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            yield return base.OnSetup();

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
        }

        private void ObjectivePhase()
        {
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
            Services.ObjectiveService.Clear();

            if (keyPickupInstance) Destroy(keyPickupInstance.gameObject);
            if (extractionZoneInstance) Destroy(extractionZoneInstance.gameObject);

            yield return base.OnTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            enemy = null;
            keyPickupInstance = null;
            extractionZoneInstance = null;
        }
    }
}
