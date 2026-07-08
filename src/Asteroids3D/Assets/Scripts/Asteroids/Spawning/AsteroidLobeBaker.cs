using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Spawning
{
    /// <summary>
    /// Bakes a small set of covering spheres ("lobes") along a mesh's principal
    /// axis. Pure UnityEngine (no UnityEditor dependency) so it can be called from
    /// <see cref="AsteroidSpawnSettings"/>'s OnValidate, an editor menu, or headless.
    ///
    /// Deterministic by construction (RL reproducibility): identical mesh in →
    /// identical lobes out. No Random, no time, no unordered-set iteration.
    ///
    /// Nothing reads the lobes for gameplay yet — this is the eyeball-the-decomposition
    /// stage before the solver work.
    /// </summary>
    public static class AsteroidLobeBaker
    {
        // Classifier thresholds on the principal aspect e1/e2.
        private const float TwoLobeAspect = 1.3f;   // >= this → K=2
        private const float ThreeLobeAspect = 2.5f; // >= this → K=3
        private const int LloydIterations = 5;      // fixed, deterministic

        /// <summary>
        /// Bake covering spheres for <paramref name="mesh"/>. Returns 1..3 lobes.
        /// <paramref name="aspect"/> is λ1/λ2 (two largest principal extents).
        /// </summary>
        public static AsteroidSpawnSettings.MeshInfo.LobeSphere[] Bake(Mesh mesh, out float aspect)
        {
            aspect = 1f;
            if (mesh == null) return Array.Empty<AsteroidSpawnSettings.MeshInfo.LobeSphere>();

            var verts = mesh.vertices;
            if (verts.Length == 0) return Array.Empty<AsteroidSpawnSettings.MeshInfo.LobeSphere>();

            ComputePrincipalExtents(mesh, verts, out Vector3 principalAxis, out float e1, out float e2, out _);
            aspect = e2 > 1e-8f ? e1 / e2 : 1f;

            int k = ClassifyK(aspect);
            if (k <= 1)
            {
                // K=1 MUST reproduce today's single circle exactly: mesh is centroid-
                // pivoted, so center is the origin and radius is the mean-vertex radius.
                return new[]
                {
                    new AsteroidSpawnSettings.MeshInfo.LobeSphere
                    {
                        center = Vector3.zero,
                        radius = AsteroidController.MeanVertexRadius(mesh),
                    },
                };
            }

            return KMeansLobes(verts, principalAxis, k);
        }

        /// <summary>Map a principal aspect (e1/e2) to a lobe count.</summary>
        public static int ClassifyK(float aspect)
        {
            if (aspect < TwoLobeAspect) return 1;
            if (aspect < ThreeLobeAspect) return 2;
            return 3;
        }

        /// <summary>
        /// Area-weighted PCA (mirrors <c>AsteroidCentroidOffsetReport</c>): covariance
        /// of triangle centroids weighted by triangle area → deterministic 3×3 Jacobi
        /// eigensolve → principal axes. Projected extents of ALL verts give e1≥e2≥e3;
        /// the axis carrying the largest extent is returned as the principal axis.
        /// </summary>
        private static void ComputePrincipalExtents(
            Mesh mesh, Vector3[] verts, out Vector3 principalAxis, out float e1, out float e2, out float e3)
        {
            principalAxis = Vector3.right;
            e1 = e2 = e3 = 0f;

            var tris = mesh.triangles;
            if (tris.Length < 3) return;

            // Pass 1: area-weighted centroid of triangle centroids.
            double totalArea = 0.0, wcx = 0, wcy = 0, wcz = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double area = 0.5 * (double)Vector3.Cross(b - a, c - a).magnitude;
                if (area <= 0.0) continue;
                Vector3 tc = (a + b + c) / 3f;
                totalArea += area; wcx += area * tc.x; wcy += area * tc.y; wcz += area * tc.z;
            }
            if (totalArea <= 0.0) return;
            double cx = wcx / totalArea, cy = wcy / totalArea, cz = wcz / totalArea;

            // Pass 2: area-weighted covariance.
            double c00 = 0, c01 = 0, c02 = 0, c11 = 0, c12 = 0, c22 = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double area = 0.5 * (double)Vector3.Cross(b - a, c - a).magnitude;
                if (area <= 0.0) continue;
                Vector3 tc = (a + b + c) / 3f;
                double dx = tc.x - cx, dy = tc.y - cy, dz = tc.z - cz;
                c00 += area * dx * dx; c01 += area * dx * dy; c02 += area * dx * dz;
                c11 += area * dy * dy; c12 += area * dy * dz; c22 += area * dz * dz;
            }

            var cov = new double[3, 3]
            {
                { c00, c01, c02 },
                { c01, c11, c12 },
                { c02, c12, c22 },
            };
            JacobiEigen(cov, out _, out double[,] eigVecs);

            // Projected extent of ALL verts along each eigenvector.
            var axisVecs = new Vector3[3];
            var extents = new float[3];
            for (int kAxis = 0; kAxis < 3; kAxis++)
            {
                var axis = new Vector3(
                    (float)eigVecs[0, kAxis], (float)eigVecs[1, kAxis], (float)eigVecs[2, kAxis]).normalized;
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var v in verts)
                {
                    float p = Vector3.Dot(v, axis);
                    if (p < min) min = p;
                    if (p > max) max = p;
                }
                axisVecs[kAxis] = axis;
                extents[kAxis] = max - min;
            }

            // Sort axes by descending extent (deterministic).
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    if (extents[j] > extents[i])
                    {
                        (extents[i], extents[j]) = (extents[j], extents[i]);
                        (axisVecs[i], axisVecs[j]) = (axisVecs[j], axisVecs[i]);
                    }

            principalAxis = axisVecs[0];
            e1 = extents[0]; e2 = extents[1]; e3 = extents[2];
        }

        /// <summary>
        /// 1-D k-means along the principal axis. Centers seed at even quantiles of the
        /// axial coordinate (no Random); ~5 fixed Lloyd iterations. Each lobe's center
        /// is its cluster's 3D mean position; radius is the MEAN distance of assigned
        /// verts to that center (clip-over-berth: mean, not max).
        /// </summary>
        private static AsteroidSpawnSettings.MeshInfo.LobeSphere[] KMeansLobes(
            Vector3[] verts, Vector3 axis, int k)
        {
            int n = verts.Length;
            var coord = new float[n];
            for (int i = 0; i < n; i++) coord[i] = Vector3.Dot(verts[i], axis);

            var sorted = (float[])coord.Clone();
            Array.Sort(sorted);

            var centers = new float[k];
            for (int c = 0; c < k; c++)
            {
                float q = (c + 0.5f) / k;
                int idx = Mathf.Clamp(Mathf.RoundToInt(q * (n - 1)), 0, n - 1);
                centers[c] = sorted[idx];
            }

            var assign = new int[n];
            for (int iter = 0; iter < LloydIterations; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    int best = 0;
                    float bestD = Mathf.Abs(coord[i] - centers[0]);
                    for (int c = 1; c < k; c++)
                    {
                        float d = Mathf.Abs(coord[i] - centers[c]);
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    assign[i] = best;
                }

                var sum = new float[k];
                var cnt = new int[k];
                for (int i = 0; i < n; i++) { sum[assign[i]] += coord[i]; cnt[assign[i]]++; }
                for (int c = 0; c < k; c++) if (cnt[c] > 0) centers[c] = sum[c] / cnt[c];
            }

            var pos = new Vector3[k];
            var count = new int[k];
            for (int i = 0; i < n; i++) { pos[assign[i]] += verts[i]; count[assign[i]]++; }

            var lobes = new List<AsteroidSpawnSettings.MeshInfo.LobeSphere>(k);
            for (int c = 0; c < k; c++)
            {
                if (count[c] == 0) continue; // empty cluster → drop (rare; keeps result valid)
                Vector3 center = pos[c] / count[c];
                float rsum = 0f;
                for (int i = 0; i < n; i++) if (assign[i] == c) rsum += (verts[i] - center).magnitude;
                lobes.Add(new AsteroidSpawnSettings.MeshInfo.LobeSphere
                {
                    center = center,
                    radius = rsum / count[c],
                });
            }
            return lobes.ToArray();
        }

        /// <summary>
        /// Deterministic cyclic Jacobi eigenvalue algorithm for a symmetric 3×3 matrix.
        /// eigVecs columns are the orthonormal eigenvectors; eigVals[k] pairs with column k.
        /// </summary>
        private static void JacobiEigen(double[,] input, out double[] eigVals, out double[,] eigVecs)
        {
            var a = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    a[i, j] = input[i, j];

            var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

            const int maxSweeps = 50;
            const double eps = 1e-12;
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
                if (off < eps) break;

                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        double apq = a[p, q];
                        if (Math.Abs(apq) < eps) continue;

                        double app = a[p, p], aqq = a[q, q];
                        double phi = 0.5 * (aqq - app) / apq;
                        double sign = phi >= 0 ? 1.0 : -1.0;
                        double tval = sign / (Math.Abs(phi) + Math.Sqrt(phi * phi + 1.0));
                        double cval = 1.0 / Math.Sqrt(tval * tval + 1.0);
                        double sval = tval * cval;

                        for (int kk = 0; kk < 3; kk++)
                        {
                            double akp = a[kk, p], akq = a[kk, q];
                            a[kk, p] = cval * akp - sval * akq;
                            a[kk, q] = sval * akp + cval * akq;
                        }
                        for (int kk = 0; kk < 3; kk++)
                        {
                            double apk = a[p, kk], aqk = a[q, kk];
                            a[p, kk] = cval * apk - sval * aqk;
                            a[q, kk] = sval * apk + cval * aqk;
                        }
                        for (int kk = 0; kk < 3; kk++)
                        {
                            double vkp = v[kk, p], vkq = v[kk, q];
                            v[kk, p] = cval * vkp - sval * vkq;
                            v[kk, q] = sval * vkp + cval * vkq;
                        }
                    }
                }
            }

            eigVals = new double[3] { a[0, 0], a[1, 1], a[2, 2] };
            eigVecs = v;
        }
    }
}
