using System;
using Game;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Session
{
    public sealed class SectorSessionConfig
    {
        public const string DefaultWorldSceneName = "BasicWorld";
        public const float DefaultEnemySpawnRadius = 5f;

        public SectorSessionConfig(
            GameConfig gameConfig,
            string worldSceneName = DefaultWorldSceneName,
            bool loadWorldScene = true,
            bool spawnEnemy = true,
            float enemySpawnRadius = DefaultEnemySpawnRadius,
            Vector3? playerSpawnPosition = null,
            Quaternion? playerSpawnRotation = null,
            Func<Vector3> enemySpawnPositionProvider = null)
        {
            GameConfig = gameConfig ? gameConfig : throw new ArgumentNullException(nameof(gameConfig));
            WorldSceneName = worldSceneName;
            LoadWorldScene = loadWorldScene;
            SpawnEnemy = spawnEnemy;
            EnemySpawnRadius = enemySpawnRadius;
            PlayerSpawnPosition = playerSpawnPosition ?? Vector3.zero;
            PlayerSpawnRotation = playerSpawnRotation ?? Quaternion.identity;
            EnemySpawnPositionProvider = enemySpawnPositionProvider ?? (() =>
                GamePlane.PlanePointToWorld(Random.insideUnitCircle * enemySpawnRadius));
        }

        public GameConfig GameConfig { get; }
        public string WorldSceneName { get; }
        public bool LoadWorldScene { get; }
        public bool SpawnEnemy { get; }
        public float EnemySpawnRadius { get; }
        public Vector3 PlayerSpawnPosition { get; }
        public Quaternion PlayerSpawnRotation { get; }
        public Func<Vector3> EnemySpawnPositionProvider { get; }

        public Vector3 GetEnemySpawnPosition()
        {
            return EnemySpawnPositionProvider?.Invoke() ?? Vector3.zero;
        }

        public static SectorSessionConfig FromGameConfig(GameConfig config)
        {
            return new SectorSessionConfig(config);
        }
    }
}
