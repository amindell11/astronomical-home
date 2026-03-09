using System.Collections;
using Asteroids.Fields;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Minimal testbench sector: world, asteroids, and an optional enemy.
    /// No encounters, objectives, respawn logic, or sector completion — runs indefinitely.
    /// </summary>
    public class TestbenchSectorManager : PlaySector
    {
        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        [Header("Enemy (Optional)")]
        [SerializeField] private bool spawnEnemyOnStart;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private Ships.Command.Commander enemyCommander;
        [SerializeField] private Vector2 enemySpawnPosition = new Vector2(0f, 50f);

        private Ship enemy;
        private UpdatingAsteroidField asteroidFieldInstance;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            yield return base.OnSetup();

            if (spawnEnemyOnStart && enemyTemplate)
            {
                enemy = Services.UnitService.SpawnShip(
                    enemyTemplate, enemyCommander, shipSettings,
                    1, GamePlane.PlanePointToWorld(enemySpawnPosition),
                    Quaternion.identity);
            }

            InitializeAsteroidField();
        }

        private void InitializeAsteroidField()
        {
            if (!updatingAsteroidFieldPrefab) return;

            var cullingBoundary = Services.EnvironmentService.World
                ? Services.EnvironmentService.World.AsteroidCullingBoundary
                : null;

            if (!cullingBoundary)
            {
                Debug.LogWarning("[Testbench] No AsteroidCullingBoundary in spawned world.");
                return;
            }

            asteroidFieldInstance = Instantiate(updatingAsteroidFieldPrefab);
            asteroidFieldInstance.Initialize(cullingBoundary);
            asteroidFieldInstance.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            asteroidFieldInstance.CurrentAnchorPos = () =>
                player ? GamePlane.ProjectOntoPlane(player.transform.position) : asteroidFieldInstance.transform.position;
        }

        protected override IEnumerator OnTeardown()
        {
            if (asteroidFieldInstance) Destroy(asteroidFieldInstance.gameObject);

            yield return base.OnTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            enemy = null;
            asteroidFieldInstance = null;
        }
    }
}
