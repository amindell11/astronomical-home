using System;
using Game.Services;
using Ships.Command;
using UnityEngine;

namespace Ships
{
    /// <summary>
    /// Centralised factory responsible for spawning and wiring up <see cref="Ship"/> instances.
    /// This removes all runtime GetComponent look-ups from the gameplay code and gives callers
    /// full control over how a ship is composed.
    /// </summary>
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
