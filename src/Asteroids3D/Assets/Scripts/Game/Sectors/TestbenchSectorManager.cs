using System.Collections;
using Asteroids.Fields;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Minimal testbench sector: world, asteroids, and an optional enemy.
    /// No encounters, objectives, or sector completion — runs indefinitely.
    /// Both player and enemy ships respawn after death.
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

        [Header("Respawn")]
        [Tooltip("Enable respawn for the player ship when it dies.")]
        [SerializeField] private bool respawnPlayer = true;
        [Tooltip("Enable respawn for the enemy ship when it dies.")]
        [SerializeField] private bool respawnEnemy = true;
        [Tooltip("Delay in seconds before a ship respawns after death.")]
        [SerializeField] private float respawnDelay = 3f;
        [Tooltip("Radius around the spawn origin used for randomising the respawn position.")]
        [SerializeField] private float respawnRadius = 30f;

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
                    GamePlane.Rotation);
            }

            WireRespawn();
            InitializeAsteroidField();
        }

        private void WireRespawn()
        {
            if (respawnPlayer && player)
            {
                player.Damage.OnDeath += (deadShip, _) =>
                    Services.UnitService.WaitAndRespawnShip(deadShip,
                        playerSpawnPosition + Random.insideUnitCircle * respawnRadius,
                        0, respawnDelay);
            }

            if (respawnEnemy && enemy)
            {
                enemy.Damage.OnDeath += (deadShip, _) =>
                    Services.UnitService.WaitAndRespawnShip(deadShip,
                        enemySpawnPosition + Random.insideUnitCircle * respawnRadius,
                        0, respawnDelay);
            }
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
