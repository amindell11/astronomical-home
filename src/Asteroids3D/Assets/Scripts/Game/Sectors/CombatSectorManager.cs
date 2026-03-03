using System.Collections;
using Game.Bootstrap;
using UnityEngine;
using Ships;

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

        // TODO: these will call into Services once agent-2's implementations exist
        protected override IEnumerator OnSetup()
        {
            // Phase 1: Load scene
            if (Config.LoadScene)
            {
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            }

            // Phase 2: Spawn world
            // Services.EnvironmentService.SpawnWorld(worldPrefab);

            // Phase 3: Spawn actors
            // Services.UnitService.SpawnPlayer(playerTemplate, playerCommander, shipSettings, ...);
            // Services.UnitService.SpawnEnemy(enemyTemplate, enemyCommander, shipSettings, ...);

            // Phase 4: Set objective
            // Services.ObjectiveService.SetObjective(...)

            // Phase 5: Bind camera
            // Services.CameraService.SetSubject(player.transform);

            yield return null;
        }

        protected override IEnumerator OnTeardown()
        {
            // Destroy spawned objects, unload scene
            // Services will handle their own cleanup via Clear()

            if (Config.LoadScene)
            {
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);
            }

            yield return null;
        }
    }
}
