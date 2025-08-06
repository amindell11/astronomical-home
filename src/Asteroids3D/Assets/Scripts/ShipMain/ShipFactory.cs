using UnityEngine;
using ShipMain.Control;
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
        /// <param name="shipSettings">Settings asset governing movement, damage, etc.</param>
        /// <param name="team">Team id to assign to the ship.</param>
        /// <param name="position">Spawn position in world space.</param>
        /// <param name="rotation">Spawn rotation.</param>
        /// <returns>The fully initialised <see cref="Ship"/> instance.</returns>
        public static Ship CreateShip<TCommander>(GameObject prefab,
                                                 Settings shipSettings,
                                                 int team,
                                                 Vector3 position,
                                                 Quaternion rotation)
            where TCommander : MonoBehaviour, ICommandSource
        {
            var go = Object.Instantiate(prefab, position, rotation);
            var ship = go.GetComponent<Ship>();
            ship.Movement        = go.GetComponent<Movement>();
            ship.LaserGun        = go.GetComponentInChildren<LaserGun>();
            ship.MissileLauncher = go.GetComponentInChildren<MissileLauncher>();
            ship.DamageHandler   = go.GetComponent<DamageHandler>();
            ship.Hull            = go.GetComponent<Hull>();
            ship.Commander = CreateCommandSource<TCommander>(go);
            ship.Initialize(shipSettings, team);

            return ship;
        }
        private static ICommandSource CreateCommandSource<TCommander>(GameObject parent)
            where TCommander : MonoBehaviour, ICommandSource
        {
            var go = new GameObject("Commander");
            go.transform.parent = parent.transform;
            var commander = go.AddComponent<TCommander>();
            return commander;
        }
    }
}