using UnityEngine;

namespace Ships.Command
{
    /// <summary>Base class for anything that drives a <see cref="Ship"/>: given a <see cref="ShipControl"/> bundle once at init, it pushes commands to the injected actuators and never holds the Ship itself.</summary>
    public abstract class Commander : MonoBehaviour
    {
        /// <summary>Injects the narrow control surface for the ship this commander drives; called once by the ship after its own initialization.</summary>
        public abstract void Initialize(in ShipControl control);

        /// <summary>Restores the commander to its just-initialized state ("as if freshly spawned"); paired with <c>Ship.ResetShip</c> on respawn.</summary>
        public virtual void ResetState() { }
    }
}
