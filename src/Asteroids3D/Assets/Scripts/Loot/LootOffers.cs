using System;
using System.Collections.Generic;

namespace Loot
{
    /// <summary>Draws a pick's offer: distinct eligible items from the pool, uniformly, up to the count.</summary>
    public static class LootOffers
    {
        public static List<T> Draw<T>(IReadOnlyList<T> pool, Func<T, bool> eligible, int count,
            Func<int, int> randomBelow) where T : UnityEngine.Object
        {
            var candidates = new List<T>();
            foreach (var item in pool)
                if (item && eligible(item)) candidates.Add(item);

            var offer = new List<T>(count);
            while (offer.Count < count && candidates.Count > 0)
            {
                var index = randomBelow(candidates.Count);
                offer.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            return offer;
        }
    }
}
