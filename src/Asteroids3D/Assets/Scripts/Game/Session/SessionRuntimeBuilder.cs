using System;
using Ships;
using UnityEngine;
using Utils;
using World;
using ShipFactory = Ships.Factory;

namespace Game.Session
{
    internal sealed class SessionRuntimeBuilder
    {
        public void BuildRuntimeServices(SessionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            InitializeWorld(context);
            context.ShipRegistry = new ShipRegistry(context.Config.GameConfig);
        }

        public void SpawnActors(
            SessionContext context,
            Action<Ship> wireShipDependencies,
            Func<Transform> worldFollowerProvider)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (wireShipDependencies == null)
                throw new ArgumentNullException(nameof(wireShipDependencies));

            var config = context.Config.GameConfig;
            var shipRegistry = context.ShipRegistry;

            context.Player = ShipFactory.CreateShip(
                config.PlayerTemplate,
                config.PlayerCommander,
                config.ShipSettings,
                0,
                context.Config.PlayerSpawnPosition,
                context.Config.PlayerSpawnRotation,
                postInitialize: wireShipDependencies);
            context.Player.tag = TagNames.Player;

            shipRegistry.ActiveShips.Add(context.Player);

            if (context.Config.SpawnEnemy && config.EnemyTemplate)
            {
                var enemySpawn = context.Config.GetEnemySpawnPosition();
                context.Enemy = ShipFactory.CreateShip(
                    config.EnemyTemplate,
                    config.EnemyCommander,
                    config.ShipSettings,
                    1,
                    enemySpawn,
                    Quaternion.identity,
                    wireShipDependencies);

                shipRegistry.ActiveShips.Add(context.Enemy);
            }

            context.RespawnRunner.Initialize(config.ShipSpawnerSettings, shipRegistry, worldFollowerProvider);

            if (context.World?.Follower && context.Player)
                context.World.Follower.SetTarget(context.Player.transform);
        }

        private static void InitializeWorld(SessionContext context)
        {
            if (!context.Config.GameConfig.World)
                return;

            context.World = UnityEngine.Object.Instantiate(context.Config.GameConfig.World);
        }
    }
}
