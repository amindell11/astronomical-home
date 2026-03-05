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
    /// Fully event-based — no Update loop. KeyPickup self-ticks, ObjectiveService self-ticks.
    /// Side effects (chaser spawn, restart) are bundled into state onEnter callbacks.
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

        [Header("Objective")]
        [SerializeField] private ObjectiveParams objectiveParams;
        [SerializeField] private Transform extractionZonePrefab;
        [SerializeField] private KeyPickup keyPickup;

        [Header("Spawn Positions")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 enemySpawnOffset = new Vector3(0f, 0f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship player, enemy, chaser;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);
            
            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            player = SectorUtils.BuildAndWirePlayer(playerTemplate, playerCommander, shipSettings, 0, playerSpawnPosition, Services);
            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, playerSpawnPosition + enemySpawnOffset, Quaternion.identity);
            chaser = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, new Vector2(50,50), Quaternion.identity);
            
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
            if (!objectiveParams)
                return;

            if (keyPickup)
            {
                keyPickup.Initialize(player.transform, objectiveParams.KeyPickupDistance);
                keyPickup.SpawnKey(playerSpawnPosition, objectiveParams.KeySpawnRadius);
            }

            if (chaser)
                chaser.gameObject.SetActive(false);

            var extractionPos = new Vector2(50, 50);

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(keyPickup),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(
                    () => player ? player.transform.position : Vector3.zero,
                    () => extractionPos,
                    IsExtractionBlocked,
                    objectiveParams.ExtractionRadius,
                    onEnter: () => { if (chaser) chaser.gameObject.SetActive(true); }),
                ["extracted"] = () => new ExtractedState(onEnter: RestartEncounter),
                ["failed"] = () => new FailedState(onEnter: RestartEncounter)
            };

            var mission = MissionDefinition.CreateDefault(
                failCriteria: () => !player || !player.gameObject.activeSelf);

            Services.ObjectiveService.SetObjective(mission, builders);
        }

        private bool IsExtractionBlocked()
        {
            if (!chaser || !player || !objectiveParams)
                return false;

            return Vector3.Distance(chaser.transform.position, player.transform.position) <= objectiveParams.ExtractionBlockDistance;
        }

        private void RestartEncounter()
        {
            if (keyPickup) keyPickup.ResetKey(playerSpawnPosition, objectiveParams.KeySpawnRadius);
            if (chaser) chaser.gameObject.SetActive(false);
            if (player && !player.gameObject.activeSelf)
            {
                player.transform.position = playerSpawnPosition;
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

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            player = null;
            enemy = null;

            yield return null;
        }
    }
}
