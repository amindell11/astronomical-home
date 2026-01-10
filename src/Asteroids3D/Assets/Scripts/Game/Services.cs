using Ships;
using Ships.Control;
using UnityEngine;
using Utils;
using ShipFactory  = Ships.Factory;
using ShipSpawner = Ships.Spawner;

namespace Game
{
    public class Services
    {
        public Ship Player { get; }
        public Ship Enemy { get; }
        public SubscribedSet<Ship> ActiveShips { get; } = new();
        public ShipSpawner Spawner { get; }

        public Services(Config config)
        {
            Player = ShipFactory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0,
                Vector3.zero, Quaternion.identity);
            Player.tag = TagNames.Player;

            Enemy = ShipFactory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            
            ActiveShips.Add(Player);
            ActiveShips.Add(Enemy);
            
            Spawner = new ShipSpawner(config.SpawnerSettings, Player, Enemy);
        }

        public void Dispose()
        {
            // Unsubscribe spawner callbacks (death events etc.) first.
            Spawner?.SubscribedShips?.Clear();

            ActiveShips.Clear();

            if (Player != null)
                Object.Destroy(Player.gameObject);
            if (Enemy != null)
                Object.Destroy(Enemy.gameObject);
        }
    }
}
