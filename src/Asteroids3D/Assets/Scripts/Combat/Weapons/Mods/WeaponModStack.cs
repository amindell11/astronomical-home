using System;
using System.Collections.Generic;

namespace Combat.Weapons.Mods
{
    /// <summary>
    /// The weapon mods a run has picked, in pick order. Mods ride the run, not the weapon: a mount's
    /// view (<see cref="For"/>) is rebuilt and re-handed whenever mounts or picks change, so a pick
    /// that fits nothing carried stays held and idle until a fitting weapon is mounted.
    /// </summary>
    public sealed class WeaponModStack
    {
        private readonly List<WeaponMod> picks = new();

        public void Add(WeaponMod mod)
        {
            if (!mod) throw new ArgumentNullException(nameof(mod));
            picks.Add(mod);
        }

        public void Clear() => picks.Clear();

        /// <summary>The picks that land on <paramref name="mount"/>, the live instance of <paramref name="prefab"/>, summed per field.</summary>
        public WeaponMods For(WeaponComponent mount, WeaponComponent prefab)
        {
            var any = false;
            var bonus = new float[WeaponModFields.Count];
            var rounds = 0;
            foreach (var mod in picks)
            {
                if (!mod.Fits(mount, prefab)) continue;
                any = true;
                if (mod.Field == WeaponModField.Magazine) rounds += mod.Rounds;
                else bonus[(int)mod.Field] += mod.Amount;
            }
            return any ? new WeaponMods(bonus, rounds) : WeaponMods.None;
        }
    }
}
