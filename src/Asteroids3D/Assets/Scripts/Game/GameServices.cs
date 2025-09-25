using Ships;
using Ships.Control;
using UnityEngine;
using Utils;
using ShipSpawner = Ships.Spawner;

namespace Game
{
    public class GameServices
    {
        public Ship Player { get; }
        public Ship Enemy { get; }
        public SubscribedSet<Ship> ActiveShips { get; } = new();
        public ShipSpawner Spawner { get; }

        public GameServices(GameInitiatorConfig config)
        {
            Player = Factory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0,
                Vector3.zero, Quaternion.identity);
            Player.tag = TagNames.Player;

            Enemy = Factory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            
            ActiveShips.Add(Player);
            ActiveShips.Add(Enemy);
            
            Spawner = new ShipSpawner(Player, Enemy);
            ServiceLocator.Register(Spawner);
        }
    }
}
