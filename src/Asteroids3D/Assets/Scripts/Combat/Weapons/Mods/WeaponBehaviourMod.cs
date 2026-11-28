using System;
using Balance;
using Loot;
using Ships;
using UnityEngine;

namespace Combat.Weapons.Mods
{
    [CreateAssetMenu(fileName = "WeaponBehaviourMod", menuName = "Weapons/Weapon Behaviour Mod")]
    public sealed class WeaponBehaviourMod : BehaviourMod
    {
        [Tooltip("Weapon or ordnance prefabs this mod lands on; offered only while one is carried. Required.")]
        [Stat, SerializeField] private WeaponComponent[] fits = Array.Empty<WeaponComponent>();

        public override bool LandsOn(WeaponComponent weaponPrefab)
        {
            if (fits.Length == 0)
                throw new InvalidOperationException($"Weapon behaviour mod '{name}' lists no weapon prefab to land on.");
            return weaponPrefab && Array.IndexOf(fits, weaponPrefab) >= 0;
        }

        public override bool Fits(Ship ship) => ship && ship.Weapons && ship.Weapons.Fits(this);

        public override string Landing(Ship ship) => ship.Weapons.LandingNames(this);

#if UNITY_EDITOR
        internal void Configure(string displayName, string effect, params WeaponComponent[] fits)
        {
            SetDisplayName(displayName);
            SetEffect(effect);
            this.fits = fits;
        }
#endif
    }
}
