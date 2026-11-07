using System;
using Balance;
using Loot;
using Ships;
using UnityEngine;

namespace Combat.Weapons.Mods
{
    /// <summary>
    /// One run-scoped stat modifier on a field carried weapons already have: a percent bonus, or
    /// whole rounds for Magazine. It fits any carried weapon that has the field, or only the listed
    /// prefabs. Picked at a lull, it rides the run and lands on every carried weapon it fits.
    /// Sibling of <see cref="Ships.Mods.ShipMod"/>. The rulings behind the layer: https://github.com/amindell11/astronomical-home/issues/1211#issuecomment-6464116762
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponMod", menuName = "Weapons/Weapon Mod")]
    public class WeaponMod : LullMod
    {
        [Stat, SerializeField] private WeaponModField field;

        [Tooltip("Bonus as a fraction of the authored value (0.25 = +25%); for Magazine, whole rounds added.")]
        [Stat, SerializeField, Min(0f)] private float amount = 0.25f;

        [Tooltip("Weapon prefabs this mod fits. Empty = any carried weapon that has the field.")]
        [Stat, SerializeField] private WeaponComponent[] fits = Array.Empty<WeaponComponent>();

        public WeaponModField Field => field;
        public float Amount => amount;
        public int Rounds => Mathf.RoundToInt(amount);

        public override string Effect => field == WeaponModField.Magazine
            ? $"+{Rounds} {(Rounds == 1 ? "round" : "rounds")}"
            : $"+{Mathf.RoundToInt(amount * 100f)}% {field.Label()}";

        /// <summary>Whether this mod lands on <paramref name="mount"/>, the live instance of <paramref name="prefab"/>.</summary>
        public bool Fits(WeaponComponent mount, WeaponComponent prefab) =>
            mount && mount.Supports(field) && (fits.Length == 0 || Array.IndexOf(fits, prefab) >= 0);

        public override bool Fits(Ship ship) => ship && ship.Weapons && ship.Weapons.Fits(this);

        public override string Landing(Ship ship) => ship.Weapons.LandingNames(this);

#if UNITY_EDITOR
        internal void Configure(string displayName, WeaponModField field, float amount, params WeaponComponent[] fits)
        {
            SetDisplayName(displayName);
            this.field = field;
            this.amount = amount;
            this.fits = fits;
        }
#endif
    }
}
