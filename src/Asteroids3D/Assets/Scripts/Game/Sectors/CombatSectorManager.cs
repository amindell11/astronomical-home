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

        protected override IEnumerator OnSetup()
        {
            // Phase 1: Load scene
            if (Config.LoadScene)
            {
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            }

            // Phase 2: Spawn world
            if (worldPrefab)
            {
                Services.EnvironmentService.SpawnWorld(worldPrefab);
            }

            // Phase 3: Spawn actors
            player = Services.UnitService.SpawnShip(
                playerTemplate, playerCommander, shipSettings,
                0, playerSpawnPosition, Quaternion.identity);
            player.tag = "Player";

            if (enemyTemplate)
            {
                enemy = Services.UnitService.SpawnShip(
                    enemyTemplate, enemyCommander, shipSettings,
                    1, playerSpawnPosition + enemySpawnOffset, Quaternion.identity);
            }

            // Wire world follower to player
            var world = Services.EnvironmentService.World;
            if (world && world.Follower && player)
            {
                world.Follower.SetTarget(player.transform);
            }

            // Phase 4: Camera
            Services.CameraService.Initialize(cameraRigPrefab);
            Services.CameraService.SetSubject(player.transform);

            // Add enemy as secondary subject
            if (enemy)
            {
                Services.CameraService.AddSecondarySubject(enemy.transform);
            }

            // Wire secondary subject tracking via registry events
            var registry = Services.UnitService.ActiveRegistry;
            if (registry != null)
            {
                registry.ActiveShips.OnAdd += OnShipAddedToRegistry;
                registry.ActiveShips.OnRemove += OnShipRemovedFromRegistry;
            }

            // Configure player input projection
            if (player.Commander is PlayerCommander pc)
            {
                Services.CameraService.ConfigurePlayerInputProjection(pc);
            }

            // Phase 5: Objective (if controller and params available)
            // ObjectiveService.SetObjective requires full factory wiring which depends on
            // scene-level components; deferred to ObjectiveTrackerController scene setup for MVP.

            yield return null;
        }

        protected override IEnumerator OnTeardown()
        {
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
