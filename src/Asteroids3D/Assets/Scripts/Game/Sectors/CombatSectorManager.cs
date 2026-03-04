using System.Collections;
using Cameras;
using Game.Bootstrap;
using Objectives;
using Player;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Combat sector: loads a scene, spawns player + enemies, sets extraction objective.
    /// Type-specific settings are serialized on this prefab, NOT in SectorConfigSO.
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
        private ObjectiveTrackerController objectiveController;

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
            objectiveController = FindObjectOfType<ObjectiveTrackerController>();
            if (objectiveController == null || objectiveParams == null)
                return;

            var factory = new ObjectiveStateFactory(
                objectiveController,
                objectiveController,
                objectiveController,
                objectiveController,
                objectiveController,
                objectiveParams);

            Services.ObjectiveService.SetObjective(
                MissionDefinition.CreateDefault(),
                factory,
                objectiveController);
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
