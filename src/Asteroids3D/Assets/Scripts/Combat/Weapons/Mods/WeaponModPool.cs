using System;
using Balance;
using UnityEngine;

namespace Combat.Weapons.Mods
{
    [CreateAssetMenu(fileName = "WeaponModPool", menuName = "Weapons/Weapon Mod Pool")]
    public class WeaponModPool : ScriptableObject
    {
        [Tooltip("Mods a lull may offer; an offer shows only those that fit a carried weapon.")]
        [Stat] public WeaponMod[] mods = Array.Empty<WeaponMod>();
    }
}
