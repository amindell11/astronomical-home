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
    /// Builds the objective step dictionary inline with closures capturing runtime refs.
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

        [Header("Spawn Positions")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 enemySpawnOffset = new Vector3(0f, 0f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        [Header("Objective Visuals")]
        [SerializeField] private GameObject keyVisual;
        [SerializeField] private Transform chaser;

        private Ship enemy;
        private KeyPickup keyPickup;

        /// <summary>The player ship spawned by this sector.</summary>
        public Ship Player { get; private set; }

        private void Update()
        {
            if (keyPickup != null && Player && objectiveParams
                && keyPickup.CheckPickup(Player.transform.position, objectiveParams.KeyPickupDistance)
                && keyVisual)
                keyVisual.SetActive(false);

            Services.ObjectiveService.Tick(Time.deltaTime);
        }

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);
            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            Player = SectorUtils.BuildAndWirePlayer(playerTemplate, playerCommander, shipSettings, 0, playerSpawnPosition, Services);
            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, playerSpawnPosition + enemySpawnOffset, Quaternion.identity);
            Player.Damage.OnDeath += (s1, _) =>
                Services.UnitService.WaitAndRespawnShip(s1,
                    Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                    0, spawnerSettings.enemyRespawnDelay);
            enemy.Damage.OnDeath += (s1, _) =>
                Services.UnitService.WaitAndRespawnShip(s1,
                    Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                    0, spawnerSettings.enemyRespawnDelay);

            ObjectivePhase();
            yield return null;
        }

        private void ObjectivePhase()
        {
            if (objectiveParams == null)
                return;

            // Create key pickup
            keyPickup = new KeyPickup();
            var spawnCenter = playerSpawnPosition;
            keyPickup.SpawnKey(spawnCenter, objectiveParams.KeySpawnRadius);

            // Extraction zone position (use prefab position if available)
            var extractionPos = extractionZonePrefab
                ? extractionZonePrefab.position
                : Vector3.zero;

            // Build string-keyed step builders with closures
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

            var mission = MissionDefinition.CreateDefault();

            Services.ObjectiveService.SetObjective(
                mission,
                builders,
                () => Player != null && Player.gameObject.activeSelf);

            // Subscribe to state changes for reactions
            Services.ObjectiveService.OnStateChanged += OnObjectiveStateChanged;
        }

        private bool IsExtractionBlocked()
        {
            if (chaser == null || Player == null || objectiveParams == null)
                return false;

            return Vector3.Distance(chaser.position, Player.transform.position) <= objectiveParams.ExtractionBlockDistance;
        }

        private void OnObjectiveStateChanged(ObjectiveType from, ObjectiveType to)
        {
            switch (to)
            {
                case ObjectiveType.ExtractionChallenge:
                    SpawnChaser();
                    break;
                case ObjectiveType.Extracted:
                case ObjectiveType.Failed:
                    RestartEncounter();
                    break;
            }
        }

        private void SpawnChaser()
        {
            if (chaser != null)
                chaser.gameObject.SetActive(true);
        }

        private void RestartEncounter()
        {
            // Reset key
            keyPickup?.SpawnKey(playerSpawnPosition, objectiveParams.KeySpawnRadius);
            if (keyVisual != null)
            {
                keyVisual.SetActive(true);
                keyVisual.transform.position = keyPickup.KeyPosition;
            }

            // Reset chaser
            if (chaser != null)
                chaser.gameObject.SetActive(false);

            // Respawn player if needed
            if (Player != null && !Player.gameObject.activeSelf)
                Player.ResetShip();

            Services.ObjectiveService.Restart();
        }

        protected override IEnumerator OnTeardown()
        {
            // Unsubscribe from objective events
            if (Services?.ObjectiveService != null)
                Services.ObjectiveService.OnStateChanged -= OnObjectiveStateChanged;

            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();
            Services.ObjectiveService.Clear();

            if (Config.LoadScene)
            {
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);
            }

            Player = null;
            enemy = null;
            keyPickup = null;

            yield return null;
        }
    }
}
