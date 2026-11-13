using Game.Player;
using Loot;
using Ships.Command;
using Ships.Damage;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The read-only bundle the HUD binds to — the UI's counterpart of <see cref="ShipControl"/>:
    /// narrow, event-driven surfaces of the ship it displays plus the run tally and held mods, the player's flight input
    /// and its keystone's name and icon, assembled by the owning rig.
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

        /// <summary>The run's held lull mods, for the badge row beside the weapon readout.</summary>
        public readonly IHeldMods Mods;

        /// <summary>The player's boost and fire input, or null when no player commander flies the ship.</summary>
        public readonly IFlightInput Input;

        public readonly string KeystoneName;
        public readonly Sprite KeystoneIcon;

        public HudBinding(IShipStatus status, IDamageEvents damage, IWeaponReadouts weapons, IRunTally tally,
            IHeldMods mods, IFlightInput input, string keystoneName, Sprite keystoneIcon)
        {
            Status = status;
            Damage = damage;
            Weapons = weapons;
            Tally = tally;
            Mods = mods;
            Input = input;
            KeystoneName = keystoneName;
            KeystoneIcon = keystoneIcon;
        }
    }
}
