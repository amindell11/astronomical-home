using UnityEngine;
using ShipMain.Control;
using ShipMain.Movement;
using Weapons;
using ShipMain.Visuals;

namespace ShipMain
{
    /// <summary>
    /// Centralised factory responsible for spawning and wiring up <see cref="Ship"/> instances.
    /// This removes all runtime GetComponent look-ups from the gameplay code and gives callers
    /// full control over how a ship is composed.
    /// </summary>
    public static class ShipFactory
    {
        /// <summary>
        /// Spawns a ship prefab at the requested position and orientation, attaches the specified
        /// commander component, injects settings/team data, and returns the fully initialised
        /// instance.
        /// </summary>
        /// <typeparam name="TCommander">The commander component type that drives the ship. Must be a MonoBehaviour implementing <see cref="ICommandSource"/>.</typeparam>
        /// <param name="prefab">The ship prefab to clone.</param>
        /// <param name="commander">The ship command module prefab</param>
        /// <param name="shipSettings">Settings asset governing movement, damage, etc.</param>
        /// <param name="team">Team id to assign to the ship.</param>
        /// <param name="position">Spawn position in world space.</param>
        /// <param name="rotation">Spawn rotation.</param>
        /// <returns>The fully initialised <see cref="Ship"/> instance.</returns>
        public static Ship CreateShip(
             Ship prefab,
             Commander commander,
             Settings shipSettings,
             int team,
             Vector3 position,
             Quaternion rotation)
        {
            Ship ship = Object.Instantiate(prefab, position, rotation);
            var cmdr = Object.Instantiate(commander, ship.transform);
            ship.Initialize(shipSettings, team);
            return ship;
        }
    }
}