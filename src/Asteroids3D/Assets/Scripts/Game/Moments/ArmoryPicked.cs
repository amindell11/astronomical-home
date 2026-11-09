using System;
using System.Collections.Generic;
using System.Globalization;
using Combat.Weapons;

namespace Game.Moments
{
    public sealed class ArmoryPicked : IRunMoment
    {
        public const string PickSlot = "armory-pick";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public WeaponComponent Weapon { get; }
        public string Slot => PickSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public ArmoryPicked(int sector, WeaponComponent weapon)
        {
            if (!weapon) throw new ArgumentNullException(nameof(weapon));
            Sector = sector;
            Weapon = weapon;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["weapon"] = weapon.DisplayName
            };
        }
    }
}
