using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Convex silhouette of a ship's hull in its local plane (+Y nose, +X right).
    /// <see cref="FromPoints"/> is Andrew's monotone chain, then resampled along the perimeter.
    /// </summary>
    public static class HullOutline
    {
        public static Vector2[] FromPoints(IEnumerable<Vector2> points, int samples)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (samples < 3) throw new ArgumentException("A hull needs at least 3 samples.", nameof(samples));

            var sorted = new List<Vector2>(points);
            sorted.Sort(CompareXThenY);
            var count = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                if (count > 0 && Same(sorted[i], sorted[count - 1])) continue;
                sorted[count++] = sorted[i];
            }

            var hull = Chain(sorted, count);
            if (hull.Length < 3)
                throw new ArgumentException("A hull needs at least 3 distinct non-collinear points.", nameof(points));
            return Resample(hull, samples);
        }

        public static Vector2 Normal(Vector2[] outline, int i)
        {
            if (outline == null) throw new ArgumentNullException(nameof(outline));
            if ((uint)i >= (uint)outline.Length) throw new ArgumentOutOfRangeException(nameof(i));

            var count = outline.Length;
            var chord = outline[(i + 1) % count] - outline[(i + count - 1) % count];
            var outward = new Vector2(chord.y, -chord.x);
            var length = outward.magnitude;
            if (!(length > 0f))
                throw new ArgumentException("Outline normal is undefined at a degenerate point.", nameof(outline));
            return outward / length;
        }

        public static (float min, float max) Extent(Vector2[] outline)
        {
            if (outline == null || outline.Length == 0)
                throw new ArgumentException("Outline needs a point.", nameof(outline));

            var min = outline[0].y;
            var max = min;
            for (var i = 1; i < outline.Length; i++)
            {
                var y = outline[i].y;
                if (y < min) min = y;
                else if (y > max) max = y;
            }

            return (min, max);
        }

        private static Vector2[] Chain(List<Vector2> sorted, int count)
        {
            if (count < 3) return Array.Empty<Vector2>();

            var lower = new List<Vector2>(count);
            for (var i = 0; i < count; i++)
            {
                var point = sorted[i];
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(point);
            }

            var upper = new List<Vector2>(count);
            for (var i = count - 1; i >= 0; i--)
            {
                var point = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(point);
            }

            var hullCount = lower.Count + upper.Count - 2;
            if (hullCount < 3) return Array.Empty<Vector2>();

            var hull = new Vector2[hullCount];
            for (var i = 0; i < lower.Count - 1; i++) hull[i] = lower[i];
            var upperStart = lower.Count - 1;
            for (var i = 0; i < upper.Count - 1; i++) hull[upperStart + i] = upper[i];
            return hull;
        }

        private static Vector2[] Resample(Vector2[] hull, int samples)
        {
            var count = hull.Length;
            var cumulative = new float[count + 1];
            for (var i = 0; i < count; i++)
                cumulative[i + 1] = cumulative[i] + Vector2.Distance(hull[i], hull[(i + 1) % count]);
            if (!(cumulative[count] > 0f))
                throw new ArgumentException("A hull needs at least 3 distinct non-collinear points.");

            var step = cumulative[count] / samples;
            var result = new Vector2[samples];
            var segment = 0;
            for (var s = 0; s < samples; s++)
            {
                var target = step * s;
                while (segment < count - 1 && cumulative[segment + 1] < target) segment++;
                var span = cumulative[segment + 1] - cumulative[segment];
                var t = span > 0f ? (target - cumulative[segment]) / span : 0f;
                var next = (segment + 1) % count;
                result[s] = Vector2.Lerp(hull[segment], hull[next], t);
            }

            return result;
        }

        private static int CompareXThenY(Vector2 a, Vector2 b)
        {
            var byX = a.x.CompareTo(b.x);
            return byX != 0 ? byX : a.y.CompareTo(b.y);
        }

        private static bool Same(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b) =>
            (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
    }
}
