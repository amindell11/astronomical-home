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
    /// Owns chaser spawning and encounter restart as reactions to state changes.
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
        [SerializeField] private Transform chaser;

        [Header("Spawn Positions")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 enemySpawnOffset = new Vector3(0f, 0f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        private Ship enemy;

        /// <summary>The player ship spawned by this sector.</summary>
        public Ship Player { get; private set; }

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);
            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            Player = SectorUtils.BuildAndWirePlayer(playerTemplate, playerCommander, shipSettings, 0, playerSpawnPosition, Services);
            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, playerSpawnPosition + enemySpawnOffset, Quaternion.identity);

            // Enemy respawns on death; player death is handled by the objective tracker (→ Failed → restart)
            if (spawnerSettings)
            {
                enemy.Damage.OnDeath += (s1, _) =>
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
                keyPickup.Initialize(Player.transform, objectiveParams.KeyPickupDistance);
                keyPickup.SpawnKey(playerSpawnPosition, objectiveParams.KeySpawnRadius);
            }

            if (chaser)
                chaser.gameObject.SetActive(false);

            var extractionPos = extractionZonePrefab
                ? extractionZonePrefab.position
                : Vector3.zero;

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(keyPickup),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(
                    () => Player ? Player.transform.position : Vector3.zero,
                    () => extractionPos,
                    () => IsExtractionBlocked(),
                    objectiveParams.ExtractionRadius),
                ["extracted"] = () => new ExtractedState(),
                ["failed"] = () => new FailedState()
            };

            Services.ObjectiveService.SetObjective(
                MissionDefinition.CreateDefault(),
                builders,
                () => Player && Player.gameObject.activeSelf);

            Services.ObjectiveService.OnStateChanged += OnObjectiveStateChanged;
        }

        private bool IsExtractionBlocked()
        {
            if (!chaser || !Player || !objectiveParams)
                return false;

            return Vector3.Distance(chaser.position, Player.transform.position) <= objectiveParams.ExtractionBlockDistance;
        }

        private void OnObjectiveStateChanged(ObjectiveType from, ObjectiveType to)
        {
            switch (to)
            {
                case ObjectiveType.ExtractionChallenge:
                    if (chaser)
                        chaser.gameObject.SetActive(true);
                    break;
                case ObjectiveType.Extracted:
                case ObjectiveType.Failed:
                    RestartEncounter();
                    break;
            }
        }

        private void RestartEncounter()
        {
            if (keyPickup)
                keyPickup.ResetKey(playerSpawnPosition, objectiveParams.KeySpawnRadius);

            if (chaser)
                chaser.gameObject.SetActive(false);

            if (Player && !Player.gameObject.activeSelf)
                Player.ResetShip();

            Services.ObjectiveService.Restart();
        }

        protected override IEnumerator OnTeardown()
        {
            if (Services?.ObjectiveService != null)
                Services.ObjectiveService.OnStateChanged -= OnObjectiveStateChanged;

            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();
            Services.ObjectiveService.Clear();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            Player = null;
            enemy = null;

            yield return null;
        }
    }
}
