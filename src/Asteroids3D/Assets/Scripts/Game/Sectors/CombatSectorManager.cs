using System;
using System.Collections;
using System.Collections.Generic;
using Cameras;
using Game.Bootstrap;
using Objectives;
using Objectives.States;
using Player;
using Ships;
using UnityEngine;

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
        [SerializeField] private ShipSpawnerSettings respawnSettings;

        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;

        [Header("Camera")]
        [SerializeField] private CameraRig cameraRigPrefab;

        [Header("Objective")]
        [SerializeField] private ObjectiveParams objectiveParams;
        [SerializeField] private Transform extractionZonePrefab;

        [Header("Spawn Positions")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 enemySpawnOffset = new Vector3(0f, 0f, 50f);

        private Ship player;
        private Ship enemy;
        private KeyPickup keyPickup;
        private GameObject keyVisual;
        private Transform chaser;

        /// <summary>The player ship spawned by this sector.</summary>
        public Ship Player => player;
        public override Ship PresentationShip => player;

        protected override IEnumerator OnSetup()
        {
            yield return LoadScenePhase();
            SpawnWorldPhase();
            SpawnActorsPhase();
            WireWorldFollowerPhase();
            CameraPhase();
            RegistryBindingPhase();
            PlayerInputProjectionPhase();
            ObjectivePhase();
            RespawnPhase();
            yield return null;
        }

        private void Update()
        {
            if (keyPickup != null && player != null && objectiveParams != null)
            {
                if (keyPickup.CheckPickup(player.transform.position, objectiveParams.KeyPickupDistance))
                {
                    if (keyVisual != null)
                        keyVisual.SetActive(false);
                }
            }

            Services.ObjectiveService.Tick(Time.deltaTime);
        }

        private IEnumerator LoadScenePhase()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
        }

        private void SpawnWorldPhase()
        {
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);
        }

        private void SpawnActorsPhase()
        {
            player = Services.UnitService.SpawnShip(
                playerTemplate,
                playerCommander,
                shipSettings,
                0,
                playerSpawnPosition,
                Quaternion.identity);
            player.tag = "Player";

            if (!enemyTemplate)
                return;

            enemy = Services.UnitService.SpawnShip(
                enemyTemplate,
                enemyCommander,
                shipSettings,
                1,
                playerSpawnPosition + enemySpawnOffset,
                Quaternion.identity);
        }

        private void WireWorldFollowerPhase()
        {
            var world = Services.EnvironmentService.World;
            if (world && world.Follower && player)
                world.Follower.SetTarget(player.transform);
        }

        private void CameraPhase()
        {
            Services.CameraService.Initialize(cameraRigPrefab);

            if (player)
                Services.CameraService.SetSubject(player.transform);

            if (enemy)
                Services.CameraService.AddSecondarySubject(enemy.transform);
        }

        private void RegistryBindingPhase()
        {
            var registry = Services.UnitService.ActiveRegistry;
            if (registry == null)
                return;

            registry.ActiveShips.OnAdd += OnShipAddedToRegistry;
            registry.ActiveShips.OnRemove += OnShipRemovedFromRegistry;
        }

        private void PlayerInputProjectionPhase()
        {
            if (player?.Commander is PlayerCommander pc)
                Services.CameraService.ConfigurePlayerInputProjection(pc);
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
                    () => player ? player.transform.position : Vector3.zero,
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
                () => player != null && player.gameObject.activeSelf);

            // Subscribe to state changes for reactions
            Services.ObjectiveService.OnStateChanged += OnObjectiveStateChanged;
        }

        private bool IsExtractionBlocked()
        {
            if (chaser == null || player == null || objectiveParams == null)
                return false;

            return Vector3.Distance(chaser.position, player.transform.position) <= objectiveParams.ExtractionBlockDistance;
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
            if (player != null && !player.gameObject.activeSelf)
                player.ResetShip();

            Services.ObjectiveService.Restart();
        }

        private void RespawnPhase()
        {
            if (!RespawnRunner || !respawnSettings)
                return;

            RespawnRunner.Initialize(
                respawnSettings,
                Services.UnitService.ActiveRegistry,
                () => Services.EnvironmentService.WorldFollowerTransform);
        }

        protected override IEnumerator OnTeardown()
        {
            // Unsubscribe from objective events
            if (Services?.ObjectiveService != null)
                Services.ObjectiveService.OnStateChanged -= OnObjectiveStateChanged;

            // Reset respawn runner
            if (RespawnRunner)
                RespawnRunner.ResetRunner();

            // Unbind registry events
            var registry = Services.UnitService.ActiveRegistry;
            if (registry != null)
            {
                registry.ActiveShips.OnAdd -= OnShipAddedToRegistry;
                registry.ActiveShips.OnRemove -= OnShipRemovedFromRegistry;
            }

            // Services handle their own object cleanup
            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();
            Services.ObjectiveService.Clear();

            if (Config.LoadScene)
            {
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);
            }

            player = null;
            enemy = null;
            keyPickup = null;

            yield return null;
        }

        private void OnShipAddedToRegistry(Ship ship)
        {
            if (!ship || ship == player) return;
            Services.CameraService.AddSecondarySubject(ship.transform);
        }

        private void OnShipRemovedFromRegistry(Ship ship)
        {
            if (!ship) return;
            Services.CameraService.RemoveSecondarySubject(ship.transform);
        }
    }
}
