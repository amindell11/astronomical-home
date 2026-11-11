using System;
using System.Collections.Generic;
using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Weapons;

namespace UI.Hangar.Selection
{
    /// <summary>
    /// The hangar's picks, written into the pending loadout. A ship that is neither the starter nor
    /// earned can still be shown, and launch is refused while it is selected. Changing the ship
    /// reseeds both weapons from that ship's mounts.
    /// </summary>
    public sealed class HangarSelection
    {
        private readonly ShipLoadout loadout;
        private readonly bool gated;
        private readonly Ship starter;
        private readonly Func<Ship, bool> earned;
        private readonly List<Ship> ships;
        private readonly List<WeaponComponent> weapons;
        private readonly List<WeaponComponent> ordnance;

        public HangarSelection(ItemSubset offer, ShipLoadout loadout, Func<Ship, bool> earned)
        {
            if (offer && offer.ships is { Length: > 0 } && !offer.starter)
                throw new InvalidOperationException(
                    $"Hangar offer '{offer.name}' offers ships but names no starter.");

            this.loadout = loadout;
            this.earned = earned;
            gated = offer && offer.starter;
            starter = offer ? offer.starter : null;
            ships = Truthy(offer ? offer.ships : null);
            weapons = Filling(WeaponSlot.Primary, offer ? offer.weapons : null);
            ordnance = Filling(WeaponSlot.Secondary, offer ? offer.weapons : null);

            if (gated && !Selectable(loadout.Ship))
                Reseed(offer.starter);
        }

        public HangarSlot Selected { get; private set; }

        public IReadOnlyList<Ship> Ships => (IReadOnlyList<Ship>)ships ?? Array.Empty<Ship>();
        public Ship Ship => loadout.Ship;
        public WeaponComponent Weapon => loadout.PrimaryWeapon;
        public WeaponComponent Ordnance => loadout.SecondaryWeapon;
        public bool ShipLocked => gated && !Selectable(loadout.Ship);
        public bool CanLaunch => !ShipLocked;

        public event Action Changed;
        public event Action<Ship, int> ShipChanged;

        public void Select(HangarSlot slot)
        {
            if (slot == Selected) return;
            Selected = slot;
            Changed?.Invoke();
        }

        public void Turn(int step)
        {
            if (step == 0) return;
            var index = ((int)Selected + step) % 3;
            if (index < 0) index += 3;
            Select((HangarSlot)index);
        }

        public void Step(int step)
        {
            switch (Selected)
            {
                case HangarSlot.Ship:
                    if (!Cycle(ships, loadout.Ship, step, out var ship)) return;
                    Reseed(ship);
                    ShipChanged?.Invoke(ship, step);
                    break;
                case HangarSlot.Weapon:
                    if (!Cycle(weapons, loadout.PrimaryWeapon, step, out var weapon)) return;
                    loadout.PrimaryWeapon = weapon;
                    break;
                case HangarSlot.Ordnance:
                    if (!Cycle(ordnance, loadout.SecondaryWeapon, step, out var picked)) return;
                    loadout.SecondaryWeapon = picked;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Selected), Selected, null);
            }
            Changed?.Invoke();
        }

        public string Label(HangarSlot slot)
        {
            switch (slot)
            {
                case HangarSlot.Ship:
                    if (!Ship) return "None";
                    return ShipLocked ? $"{Ship.name} (locked)" : Ship.name;
                case HangarSlot.Weapon:
                    return Weapon ? Weapon.DisplayName : "None";
                case HangarSlot.Ordnance:
                    return Ordnance ? Ordnance.DisplayName : "None";
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private bool Selectable(Ship ship) => ship == starter || earned(ship);

        private void Reseed(Ship ship)
        {
            loadout.Ship = ship;
            var mounts = ship.Weapons;
            loadout.PrimaryWeapon = mounts ? mounts.PrimaryMountPrefab : null;
            loadout.SecondaryWeapon = mounts ? mounts.SecondaryMountPrefab : null;
        }

        // A pick from outside the offer enters at the end the step points to.
        private static bool Cycle<T>(List<T> options, T current, int step, out T next) where T : UnityEngine.Object
        {
            next = null;
            if (options == null || options.Count == 0) return false;
            var index = IndexOf(options, current);
            if (index < 0 && step < 0) index = options.Count;
            index = (index + step) % options.Count;
            if (index < 0) index += options.Count;
            next = options[index];
            return true;
        }

        private static int IndexOf<T>(List<T> options, T current) where T : UnityEngine.Object
        {
            for (var i = 0; i < options.Count; i++)
                if (options[i] == current) return i;
            return -1;
        }

        private static List<Ship> Truthy(Ship[] offered)
        {
            if (offered == null) return null;
            var list = new List<Ship>();
            foreach (var ship in offered)
                if (ship) list.Add(ship);
            return list;
        }

        private static List<WeaponComponent> Filling(WeaponSlot slot, WeaponComponent[] pool)
        {
            if (pool == null) return null;
            var list = new List<WeaponComponent>();
            foreach (var weapon in pool)
                if (weapon && WeaponsController.SlotFor(weapon) == slot)
                    list.Add(weapon);
            return list;
        }
    }
}
