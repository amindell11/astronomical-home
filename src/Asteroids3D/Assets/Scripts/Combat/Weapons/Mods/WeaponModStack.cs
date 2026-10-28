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

        /// <summary>The held mods that fit <paramref name="weapon"/>, a weapon prefab, for a weapon offer's card.</summary>
        public IReadOnlyList<HeldMod> Using(WeaponComponent weapon) => Held(mod => Fits(mod, weapon));

        /// <summary>The held mods this swap would idle; all three arguments are prefabs, not live mounts.</summary>
        public IReadOnlyList<HeldMod> IdledBy(WeaponComponent replaced, WeaponComponent incoming, WeaponComponent kept) =>
            Held(mod => Fits(mod, replaced) && !Fits(mod, incoming) && !Fits(mod, kept));

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

        private static bool Fits(WeaponMod mod, WeaponComponent weapon) => mod.Fits(weapon, weapon);

        private List<HeldMod> Held(Func<WeaponMod, bool> include)
        {
            var held = new List<HeldMod>();
            foreach (var mod in picks)
            {
                if (!include(mod)) continue;
                var index = held.FindIndex(entry => entry.Mod == mod);
                if (index < 0) held.Add(new HeldMod(mod, 1));
                else held[index] = new HeldMod(mod, held[index].Count + 1);
            }
            return held;
        }
    }

    public readonly struct HeldMod
    {
        public WeaponMod Mod { get; }
        public int Count { get; }

        public HeldMod(WeaponMod mod, int count)
        {
            Mod = mod;
            Count = count;
        }

        public string Label => Count > 1 ? $"{Mod.DisplayName} ×{Count}" : Mod.DisplayName;
    }
}
