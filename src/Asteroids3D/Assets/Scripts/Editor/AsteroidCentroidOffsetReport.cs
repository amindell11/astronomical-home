using System.Collections.Generic;
using System.Text;
using Asteroids.Spawning;
using UnityEditor;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Temporary diagnostic: for every mesh referenced by an AsteroidSpawnSettings
    /// asset, report how far the vertex centroid sits from the mesh origin (the
    /// current cheap-circle center) relative to the mean-vertex radius, plus an
    /// area-weighted PCA shape classification. A per-asset spin readout is printed
    /// once at the end (spin is a spawn property, not a per-mesh property).
    /// </summary>
    public static class AsteroidCentroidOffsetReport
    {
        // Nominal MPC horizon used for silhouette-swing / fast-spin-gate math.
        private const float HorizonSeconds = 1.7f;

        // A 25 deg swing over the horizon is the gate above which a rock is
        // treated as too fast-spinning to keep a stable multi-sphere silhouette.
        private const float SwingGateDeg = 25f;

        // Aspect thresholds for the shape classifier.
        private const float RodAspect = 1.3f;   // e1/e2
        private const float PlateAspect = 1.3f; // e2/e3

        [MenuItem("Tools/Asteroids/Log Centroid Offsets")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Asteroid mesh centroid offset report ===");
            sb.AppendLine("offset = |vertex centroid - mesh origin| (local units, unscaled)");
            sb.AppendLine("PCA = area-weighted principal extents; verdict uses e1/e2>=1.3 -> ROD, e2/e3>=1.3 -> PLATE, else BLOB");

            var spinRows = new List<string>();
            var seen = new HashSet<Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:AsteroidSpawnSettings"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(path);
                if (settings == null) continue;

                spinRows.Add(BuildSpinRow(path, settings));

                if (settings.meshInfos == null) continue;

                sb.AppendLine($"-- {path}");
                foreach (var info in settings.meshInfos)
                {
                    if (info.mesh == null) { sb.AppendLine("   (null mesh)"); continue; }
                    if (!seen.Add(info.mesh)) { sb.AppendLine($"   {info.mesh.name}: (already reported)"); continue; }
                    AppendMeshStats(sb, info.mesh);
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== Asteroid spin readout ===");
            sb.AppendLine($"horizon={HorizonSeconds:F2}s  swingGate={SwingGateDeg:F0}deg  omegaGate=swingGate/horizon={SwingGateDeg / HorizonSeconds:F2} deg/s");
            sb.AppendLine("swingMaxDeg = max(|x|,|y|)*horizon; fastFrac = fraction of [min,max] base spin with |w| >= omegaGate (trips gate -> stays single-circle)");
            foreach (var row in spinRows) sb.AppendLine(row);

            Debug.Log(sb.ToString());
        }

        private static void AppendMeshStats(StringBuilder sb, Mesh mesh)
        {
            var verts = mesh.vertices;
            if (verts.Length == 0) { sb.AppendLine($"   {mesh.name}: 0 vertices"); return; }

            var centroid = Vector3.zero;
            foreach (var v in verts) centroid += v;
            centroid /= verts.Length;

            float meanFromOrigin = 0f, meanFromCentroid = 0f;
            foreach (var v in verts)
            {
                meanFromOrigin += v.magnitude;
                meanFromCentroid += (v - centroid).magnitude;
            }
            meanFromOrigin /= verts.Length;
            meanFromCentroid /= verts.Length;

            sb.AppendLine(
                $"   {mesh.name}: verts={verts.Length} " +
                $"offset={centroid.magnitude:F4} ({centroid.x:F3}, {centroid.y:F3}, {centroid.z:F3}) " +
                $"meanR(origin)={meanFromOrigin:F4} meanR(centroid)={meanFromCentroid:F4} " +
                $"offset/meanR={centroid.magnitude / meanFromOrigin:P1}");

            AppendPcaShape(sb, mesh, verts);
        }

        /// <summary>
        /// Area-weighted PCA. Each triangle is treated as a point mass at its
        /// centroid, weighted by its area (so tessellation density does not bias
        /// the covariance). Principal axes come from a Jacobi eigen-decomposition
        /// of the 3x3 covariance; extents are the true projected (max-min) spans.
        /// </summary>
        private static void AppendPcaShape(StringBuilder sb, Mesh mesh, Vector3[] verts)
        {
            var tris = mesh.triangles;
            if (tris.Length < 3)
            {
                sb.AppendLine($"      PCA: (no triangles)");
                return;
            }

            // Pass 1: area-weighted centroid.
            double totalArea = 0.0;
            var wc = new Vector3d();
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double area = 0.5 * (double)Vector3.Cross(b - a, c - a).magnitude;
                if (area <= 0.0) continue;
                Vector3 tc = (a + b + c) / 3f;
                totalArea += area;
                wc.x += area * tc.x; wc.y += area * tc.y; wc.z += area * tc.z;
            }
            if (totalArea <= 0.0)
            {
                sb.AppendLine($"      PCA: (degenerate mesh area)");
                return;
            }
            double cx = wc.x / totalArea, cy = wc.y / totalArea, cz = wc.z / totalArea;

            // Pass 2: area-weighted covariance of triangle centroids.
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
            c00 /= totalArea; c01 /= totalArea; c02 /= totalArea;
            c11 /= totalArea; c12 /= totalArea; c22 /= totalArea;

            var cov = new double[3, 3]
            {
                { c00, c01, c02 },
                { c01, c11, c12 },
                { c02, c12, c22 },
            };

            JacobiEigen(cov, out double[] eigVals, out double[,] eigVecs);

            // For each eigenvector, take the true projected extent across ALL verts.
            var axes = new AxisResult[3];
            for (int k = 0; k < 3; k++)
            {
                var axis = new Vector3(
                    (float)eigVecs[0, k], (float)eigVecs[1, k], (float)eigVecs[2, k]).normalized;
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var v in verts)
                {
                    float p = Vector3.Dot(v, axis);
                    if (p < min) min = p;
                    if (p > max) max = p;
                }
                axes[k] = new AxisResult
                {
                    extent = max - min,
                    eigenValue = eigVals[k],
                };
            }

            // Sort so extent e1 >= e2 >= e3 (deterministic).
            System.Array.Sort(axes, (l, r) => r.extent.CompareTo(l.extent));

            float e1 = axes[0].extent, e2 = axes[1].extent, e3 = axes[2].extent;
            float a12 = e2 > 1e-8f ? e1 / e2 : float.PositiveInfinity;
            float a23 = e3 > 1e-8f ? e2 / e3 : float.PositiveInfinity;

            string verdict;
            if (a12 >= RodAspect) verdict = "ROD(multi-sphere candidate)";
            else if (a23 >= PlateAspect) verdict = "PLATE(stays single-circle)";
            else verdict = "BLOB(single-circle)";

            double s1 = System.Math.Sqrt(System.Math.Max(0.0, axes[0].eigenValue));
            double s2 = System.Math.Sqrt(System.Math.Max(0.0, axes[1].eigenValue));
            double s3 = System.Math.Sqrt(System.Math.Max(0.0, axes[2].eigenValue));

            sb.AppendLine(
                $"      PCA: e1={e1:F3} e2={e2:F3} e3={e3:F3} " +
                $"e1/e2={a12:F2} e2/e3={a23:F2} " +
                $"stddev=({s1:F3},{s2:F3},{s3:F3}) -> {verdict}");
        }

        private struct AxisResult
        {
            public float extent;
            public double eigenValue;
        }

        private struct Vector3d { public double x, y, z; }

        /// <summary>
        /// Deterministic cyclic Jacobi eigenvalue algorithm for a symmetric 3x3
        /// matrix. Rotates away off-diagonal entries over a fixed sweep budget.
        /// eigVecs columns are the orthonormal eigenvectors; eigVals[k] pairs with
        /// column k.
        /// </summary>
        private static void JacobiEigen(double[,] input, out double[] eigVals, out double[,] eigVecs)
        {
            // Work on a copy so the caller's matrix is untouched.
            var a = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    a[i, j] = input[i, j];

            // Eigenvector accumulator starts as identity.
            var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

            const int maxSweeps = 50;
            const double eps = 1e-12;
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double off = System.Math.Abs(a[0, 1]) + System.Math.Abs(a[0, 2]) + System.Math.Abs(a[1, 2]);
                if (off < eps) break;

                // Fixed cyclic order over the three upper-triangle pairs.
                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        double apq = a[p, q];
                        if (System.Math.Abs(apq) < eps) continue;

                        double app = a[p, p], aqq = a[q, q];
                        double phi = 0.5 * (aqq - app) / apq;
                        double sign = phi >= 0 ? 1.0 : -1.0;
                        double tval = sign / (System.Math.Abs(phi) + System.Math.Sqrt(phi * phi + 1.0));
                        double cval = 1.0 / System.Math.Sqrt(tval * tval + 1.0);
                        double sval = tval * cval;

                        // Apply rotation to A: A = J^T A J.
                        for (int k = 0; k < 3; k++)
                        {
                            double akp = a[k, p], akq = a[k, q];
                            a[k, p] = cval * akp - sval * akq;
                            a[k, q] = sval * akp + cval * akq;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            double apk = a[p, k], aqk = a[q, k];
                            a[p, k] = cval * apk - sval * aqk;
                            a[q, k] = sval * apk + cval * aqk;
                        }

                        // Accumulate rotation into eigenvector matrix.
                        for (int k = 0; k < 3; k++)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = cval * vkp - sval * vkq;
                            v[k, q] = sval * vkp + cval * vkq;
                        }
                    }
                }
            }

            eigVals = new double[3] { a[0, 0], a[1, 1], a[2, 2] };
            eigVecs = v;
        }

        private static string BuildSpinRow(string path, AsteroidSpawnSettings settings)
        {
            Vector2 spin = settings.spinRange;
            float swingMaxDeg = Mathf.Max(Mathf.Abs(spin.x), Mathf.Abs(spin.y)) * HorizonSeconds;
            float omegaGate = SwingGateDeg / HorizonSeconds;
            float fastFrac = FractionAboveGate(spin.x, spin.y, omegaGate);

            return
                $"   {path}: spinRange=({spin.x:F1},{spin.y:F1}) deg/s  " +
                $"swingMaxDeg={swingMaxDeg:F1}  fastFrac={fastFrac:P0} of range trips omegaGate={omegaGate:F2} deg/s";
        }

        /// <summary>
        /// Fraction of the base-spin interval [min,max] whose magnitude meets or
        /// exceeds the gate (i.e. would trip the fast-spin gate and stay a single
        /// circle). Handles intervals that straddle zero.
        /// </summary>
        private static float FractionAboveGate(float x, float y, float gate)
        {
            float lo = Mathf.Min(x, y);
            float hi = Mathf.Max(x, y);
            float span = hi - lo;
            if (span <= 1e-6f)
            {
                return Mathf.Abs(lo) >= gate ? 1f : 0f;
            }

            // Length of [lo,hi] with |t| >= gate = (below -gate) + (above +gate).
            float below = Mathf.Clamp(-gate - lo, 0f, span);
            float above = Mathf.Clamp(hi - gate, 0f, span);
            // Guard against double-counting if the interval is narrower than 2*gate.
            float covered = Mathf.Min(below + above, span);
            return covered / span;
        }
    }
}
