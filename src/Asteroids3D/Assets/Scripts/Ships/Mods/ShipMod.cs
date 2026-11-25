using Balance;
using Loot;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>
    /// One run-scoped stat modifier on a field group of the player's own hull, engine or shield: a
    /// percent bonus the ship folds into its resolve, applied at once with no heal. Sibling of
    /// <see cref="Combat.Weapons.Mods.WeaponMod"/>. The rulings: https://github.com/amindell11/astronomical-home/issues/1243
    /// </summary>
    [CreateAssetMenu(fileName = "ShipMod", menuName = "Ship/Ship Mod")]
    public class ShipMod : LullMod
    {
        [Stat, SerializeField] private ShipModField field;

        [Tooltip("Bonus as a fraction of the authored value (0.25 = +25%).")]
        [Stat, SerializeField, Min(0f)] private float amount = 0.25f;

        public ShipModField Field => field;
        public float Amount => amount;

        public override string Effect => $"+{Mathf.RoundToInt(amount * 100f)}% {field.Label()}";

        /// <summary>Hull mods fit every ship; an engine or shield mod needs that module to scale.</summary>
        public override bool Fits(Ship ship)
        {
            if (!ship) return false;
            return field.Part() switch
            {
                ShipPart.Engine => ship.Engine != null,
                ShipPart.Shield => ship.Shield != null,
                _ => true,
            };
        }

        public override string Landing(Ship ship) => field.Part() switch
        {
            ShipPart.Engine => ship.Engine.DisplayName,
            ShipPart.Shield => ship.Shield.DisplayName,
            _ => "Hull",
        };

#if UNITY_EDITOR
        internal void Configure(string displayName, ShipModField field, float amount)
        {
            SetDisplayName(displayName);
            this.field = field;
            this.amount = amount;
        }
#endif
    }
}
