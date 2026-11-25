using System.Collections.Generic;
using UnityEngine;

namespace AI.Scanning
{
    /// <summary>
    /// Fixed-capacity obstacle selection. An obstacle producer may find more obstacles in
    /// range than the consumer's buffer (and the solver's fixed obstacle array) can hold; the
    /// ones kept must be the <b>nearest</b> to the query point, never an arbitrary
    /// scan-order prefix — dropping a near obstacle in favour of a far one blinds avoidance to
    /// exactly the geometry that matters over the planning horizon.
    /// </summary>
    public static class ObstacleSelection
    {
        /// <summary>
        /// Copies the obstacles from <paramref name="candidates"/> nearest to
        /// <paramref name="centerPlane"/> into <paramref name="dst"/> (up to its length),
        /// ranked by squared plane distance to the obstacle centre. Returns the number written.
        /// When there are no more candidates than <paramref name="dst"/> can hold, all are
        /// copied (order preserved). Mutates <paramref name="candidates"/> (a scratch list) as
        /// working space; allocation-free.
        /// </summary>
        public static int KeepNearest(List<DetectedObstacle> candidates, Vector2 centerPlane,
            DetectedObstacle[] dst)
        {
            if (dst == null || dst.Length == 0) return 0;
            var n = candidates.Count;
            if (n <= dst.Length)
            {
                for (var i = 0; i < n; i++) dst[i] = candidates[i];
                return n;
            }

            // Partial selection sort: pull the k nearest to the front of the scratch list.
            // O(k·n) with k = dst.Length (small, fixed) — no heap, no allocation.
            var keep = dst.Length;
            for (var k = 0; k < keep; k++)
            {
                var best = k;
                var bestDist = DistSq(candidates[k].position, centerPlane);
                for (var i = k + 1; i < n; i++)
                {
                    var d = DistSq(candidates[i].position, centerPlane);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (best != k) (candidates[k], candidates[best]) = (candidates[best], candidates[k]);
                dst[k] = candidates[k];
            }
            return keep;
        }

        private static float DistSq(Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return dx * dx + dy * dy;
        }
    }
}
