using Game.Player;
using Ships.Command;
using Ships.Damage;

namespace UI
{
    /// <summary>
    /// The read-only bundle the HUD binds to — the UI's counterpart of <see cref="ShipControl"/>:
    /// narrow, event-driven surfaces of the ship it displays plus the run tally and the player's flight
    /// controls, assembled by the owning rig.
    /// The UI never sees the Ship or any sim MonoBehaviour behind these.
    /// </summary>
    public readonly struct HudBinding
    {
        /// <summary>Read-only ship status (health/shield fractions, kinematics…).</summary>
        public readonly IShipStatus Status;

        /// <summary>Damage/health event source (drives the low-health alarm).</summary>
        public readonly IDamageEvents Damage;

        /// <summary>Slot-keyed weapon display view, or null if the ship is unarmed.</summary>
        public readonly IWeaponReadouts Weapons;

        public readonly IRunTally Tally;

        /// <summary>The player's flight controls, or null when no player commander flies the ship.</summary>
        public readonly IFlightInput Input;

        public HudBinding(IShipStatus status, IDamageEvents damage, IWeaponReadouts weapons, IRunTally tally,
            IFlightInput input)
        {
            Status = status;
            Damage = damage;
            Weapons = weapons;
            Tally = tally;
            Input = input;
        }
    }
}
