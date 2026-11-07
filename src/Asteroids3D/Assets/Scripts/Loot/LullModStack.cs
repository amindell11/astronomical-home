using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Ships.Mods;

namespace Loot
{
    /// <summary>
    /// The lull mods a run has picked, in pick order. Mods ride the run, not the part: a mount's view
    /// (<see cref="For"/>) and the ship's (<see cref="ForShip"/>) are rebuilt on every read, and
    /// <see cref="Changed"/> tells the bound ship to re-read, so a pick that fits nothing carried stays
    /// held and idle until a fitting weapon is mounted.
    /// </summary>
    public sealed class LullModStack
    {
        private readonly List<LullMod> picks = new();

        public event Action Changed;

        public void Add(LullMod mod)
        {
            if (!mod) throw new ArgumentNullException(nameof(mod));
            picks.Add(mod);
            Changed?.Invoke();
        }

        public void Clear()
        {
            picks.Clear();
            Changed?.Invoke();
        }

        /// <summary>The held weapon mods that fit <paramref name="weapon"/>, a weapon prefab, for a weapon offer's card.</summary>
        public IReadOnlyList<HeldMod> Using(WeaponComponent weapon) => Held(mod => Fits(mod, weapon));

        /// <summary>The held weapon mods this swap would idle; all three arguments are prefabs, not live mounts.</summary>
        public IReadOnlyList<HeldMod> IdledBy(WeaponComponent replaced, WeaponComponent incoming, WeaponComponent kept) =>
            Held(mod => Fits(mod, replaced) && !Fits(mod, incoming) && !Fits(mod, kept));

        /// <summary>The picks that land on <paramref name="mount"/>, the live instance of <paramref name="prefab"/>, summed per field.</summary>
        public WeaponMods For(WeaponComponent mount, WeaponComponent prefab)
        {
            var any = false;
            var bonus = new float[WeaponModFields.Count];
            var rounds = 0;
            foreach (var pick in picks)
            {
                var mod = pick as WeaponMod;
                if (!mod || !mod.Fits(mount, prefab)) continue;
                any = true;
                if (mod.Field == WeaponModField.Magazine) rounds += mod.Rounds;
                else bonus[(int)mod.Field] += mod.Amount;
            }
            return any ? new WeaponMods(bonus, rounds) : WeaponMods.None;
        }

        /// <summary>The ship mod picks, summed per field group, for the ship's own resolve.</summary>
        public ShipMods ForShip()
        {
            var any = false;
            var bonus = new float[ShipModFields.Count];
            foreach (var pick in picks)
            {
                var mod = pick as ShipMod;
                if (!mod) continue;
                any = true;
                bonus[(int)mod.Field] += mod.Amount;
            }
            return any ? new ShipMods(bonus) : ShipMods.None;
        }

        private static bool Fits(WeaponMod mod, WeaponComponent weapon) => mod.Fits(weapon, weapon);

        private List<HeldMod> Held(Func<WeaponMod, bool> include)
        {
            var held = new List<HeldMod>();
            foreach (var pick in picks)
            {
                var mod = pick as WeaponMod;
                if (!mod || !include(mod)) continue;
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
