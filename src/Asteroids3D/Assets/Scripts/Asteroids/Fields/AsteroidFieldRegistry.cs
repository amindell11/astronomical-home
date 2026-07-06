using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields
{
    public static class AsteroidFieldRegistry
    {
        private static readonly List<UpdatingAsteroidField> Fields = new();

        public static void Register(UpdatingAsteroidField field)
        {
            if (field && !Fields.Contains(field))
                Fields.Add(field);
        }

        public static void Unregister(UpdatingAsteroidField field)
        {
            Fields.Remove(field);
        }

        public static int QueryLiveAsteroidsAabb(Vector2 center, Vector2 halfExtents, List<LiveAsteroidQueryHit> results)
        {
            results.Clear();
            for (var i = Fields.Count - 1; i >= 0; i--)
            {
                var field = Fields[i];
                if (!field)
                {
                    Fields.RemoveAt(i);
                    continue;
                }

                field.QueryLiveAsteroidsAabb(center, halfExtents, results);
            }

            return results.Count;
        }
    }
}
