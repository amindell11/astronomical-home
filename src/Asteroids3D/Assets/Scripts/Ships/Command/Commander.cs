using UnityEngine;

namespace Ships.Command
{
    /// <summary>
    /// Base class for anything that drives a <see cref="Ship"/> (player input, AI, scripted).
    /// A commander is given a <see cref="ShipControl"/> bundle once at init and thereafter pushes
    /// commands to the injected actuators — it never holds a reference to the Ship itself.
    /// </summary>
    public abstract class Commander : MonoBehaviour
    {
        /// <summary>
        /// Injects the narrow control surface for the ship this commander drives. Called once by
        /// the ship after it has finished its own initialization.
        /// </summary>
        public abstract void Initialize(in ShipControl control);
    }
}
