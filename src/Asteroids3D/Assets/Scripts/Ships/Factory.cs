using System;
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
             ShipSettings shipSettings,
             int team,
             Vector3 position,
             Quaternion rotation,
             Action<Ship> preInitialize = null)
        {
            var ship = UnityEngine.Object.Instantiate(prefab, position, rotation);
            ship.AddCommander(commander);
            preInitialize?.Invoke(ship);
            ship.Initialize(shipSettings, team);
            return ship;
        }
    }
}
