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

        /// <summary>
        /// <see cref="Draw{T}"/>, plus <paramref name="sure"/> whenever it is eligible: added to a short offer, or in
        /// place of one random card of a full one.
        /// </summary>
        public static List<T> DrawWith<T>(IReadOnlyList<T> pool, T sure, Func<T, bool> eligible, int count,
            Func<int, int> randomBelow) where T : UnityEngine.Object
        {
            var offer = Draw(pool, item => item != sure && eligible(item), count, randomBelow);
            if (!sure || !eligible(sure)) return offer;
            if (offer.Count < count) offer.Add(sure);
            else offer[randomBelow(offer.Count)] = sure;
            return offer;
        }
    }
}
