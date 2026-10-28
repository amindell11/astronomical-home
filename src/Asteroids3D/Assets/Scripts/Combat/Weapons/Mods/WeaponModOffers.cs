using System;
using System.Collections.Generic;

namespace Combat.Weapons.Mods
{
    /// <summary>Draws a lull's offer: distinct mods from the pool that fit a carried weapon, uniformly, up to the count.</summary>
    public static class WeaponModOffers
    {
        public static List<WeaponMod> Draw(IReadOnlyList<WeaponMod> pool, Func<WeaponMod, bool> fits, int count,
            Func<int, int> randomBelow)
        {
            var fitting = new List<WeaponMod>();
            foreach (var mod in pool)
                if (mod && fits(mod)) fitting.Add(mod);

            var offer = new List<WeaponMod>(count);
            while (offer.Count < count && fitting.Count > 0)
            {
                var index = randomBelow(fitting.Count);
                offer.Add(fitting[index]);
                fitting.RemoveAt(index);
            }
            return offer;
        }
    }
}
