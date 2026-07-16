using System;
using Game.Services;
using Ships.Command;
using UnityEngine;

namespace Ships
{
    /// <summary>Centralised factory for spawning and wiring <see cref="Ship"/> instances; callers control composition and no gameplay code does runtime GetComponent look-ups.</summary>
    public static class Factory
    {
        public static Ship CreateShip(
             Ship prefab,
             Commander commander,
             int team,
             int decisionSeed,
             IProjectileService projectiles,
             Vector3 position,
             Quaternion rotation,
             Action<Ship> postInitialize = null)
        {
            var ship = UnityEngine.Object.Instantiate(prefab, position, rotation);
            ship.AddCommander(commander);
            ship.Initialize(team, decisionSeed, projectiles);
            postInitialize?.Invoke(ship);
            return ship;
        }
    }
}
