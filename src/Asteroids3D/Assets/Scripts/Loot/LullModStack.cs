using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Ships.Mods;

namespace Loot
{
    /// <summary>
    /// The lull mods a run has picked, in pick order, plus any a sector grants and takes back. Mods
    /// ride the run, not the part: a mount's view (<see cref="For"/>) and the ship's (<see cref="ForShip"/>)
    /// are rebuilt on every read, and <see cref="Changed"/> tells the bound ship and the HUD to re-read, so a pick that
    /// fits nothing carried stays held and idle until a fitting weapon is mounted.
    /// </summary>
    public sealed class LullModStack : IHeldMods
    {
        private readonly List<LullMod> picks = new();

        public event Action Changed;

        public void Add(LullMod mod)
        {
            if (!mod) throw new ArgumentNullException(nameof(mod));
            picks.Add(mod);
            Changed?.Invoke();
        }

        /// <summary>Takes back the last-added <paramref name="mod"/>; one never held is a wiring bug.</summary>
        public void Remove(LullMod mod)
        {
            var index = picks.LastIndexOf(mod);
            if (index < 0)
                throw new InvalidOperationException($"Lull mod '{(mod ? mod.name : "null")}' is not held, so it cannot be removed.");
            picks.RemoveAt(index);
            Changed?.Invoke();
        }

        public void Clear()
        {
            picks.Clear();
            Changed?.Invoke();
        }

        /// <summary>Every held mod, weapon or ship, once each with its copy count, in first-pick order.</summary>
        public IReadOnlyList<HeldMod> All() => Held(_ => true);

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

        private static bool Fits(LullMod pick, WeaponComponent weapon)
        {
            var mod = pick as WeaponMod;
            return mod && mod.Fits(weapon, weapon);
        }

        private List<HeldMod> Held(Func<LullMod, bool> include)
        {
            var held = new List<HeldMod>();
            foreach (var pick in picks)
            {
                if (!include(pick)) continue;
                var index = held.FindIndex(entry => entry.Mod == pick);
                if (index < 0) held.Add(new HeldMod(pick, 1));
                else held[index] = new HeldMod(pick, held[index].Count + 1);
            }
            return held;
        }
    }

    /// <summary>The HUD's read-only view of the run's held mods.</summary>
    public interface IHeldMods
    {
        event Action Changed;
        IReadOnlyList<HeldMod> All();
    }

    public readonly struct HeldMod
    {
        public LullMod Mod { get; }
        public int Count { get; }

        public HeldMod(LullMod mod, int count)
        {
            Mod = mod;
            Count = count;
        }

        public string Label => Count > 1 ? $"{Mod.DisplayName} ×{Count}" : Mod.DisplayName;
    }
}
