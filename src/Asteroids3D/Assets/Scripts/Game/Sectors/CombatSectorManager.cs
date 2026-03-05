using System.Collections;
using Cameras;
using Codice.Client.Common.WebApi;
using Game.Bootstrap;
using Game.Sectors.Utils;
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
        [SerializeField] private ObserverCam observerCamPrefab;

        [Header("Objective")]
        [SerializeField] private ObjectiveParams objectiveParams;
        [SerializeField] private Transform extractionZonePrefab;

        [Header("Spawn Positions")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private Vector3 enemySpawnOffset = new Vector3(0f, 0f, 50f);

        [Header("Ship Spawn Settings")] [SerializeField]
        private ShipSpawnerSettings spawnerSettings;
        
        private Ship enemy;
        private ObjectiveTrackerController objectiveController;

        /// <summary>The player ship spawned by this sector.</summary>
        public Ship Player { get; private set; }

        protected void Awake()
        {
            objectiveController = GetComponent<ObjectiveTrackerController>();
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
            if (!objectiveController || !objectiveParams)
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


        protected override IEnumerator OnTeardown()
        {
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

            yield return null;
        }
    }
}
