using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Signed-tetrahedron volume and volume centroid of a closed mesh. Each triangle
    /// (a,b,c) forms a tetrahedron with the origin; its signed volume is
    /// dot(a, cross(b,c))/6 and its centroid is (a+b+c)/4. The volume-weighted mean of
    /// the tet centroids is the solid's centre of mass, independent of how densely each
    /// face is tessellated — unlike a vertex mean, which follows the triangulation.
    ///
    /// Editor-only by placement: baking, re-pivoting, and the build gate all run here,
    /// and the runtime is deliberately left with no mesh-vertex reads at all.
    /// </summary>
    public static class AsteroidMeshVolume
    {
        /// <summary>
        /// Below this absolute signed volume the mesh is treated as non-closed or
        /// degenerate; every caller must refuse to act on the result rather than
        /// translating by, or baking, a garbage number.
        /// </summary>
        public const float MinVolume = 1e-9f;

        /// <summary>
        /// Volume and centroid in one pass — they share the same accumulation, and
        /// splitting them would walk the triangle list twice for the two callers that
        /// want both. False means degenerate; outputs are then meaningless.
        /// </summary>
        public static bool TryCompute(Mesh mesh, out float volume, out Vector3 centroid)
        {
            volume = 0f;
            centroid = Vector3.zero;
            if (mesh == null) return false;

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            if (verts.Length == 0 || tris.Length < 3) return false;

            double sumVol = 0.0;
            double wx = 0.0, wy = 0.0, wz = 0.0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double signedVol = Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                sumVol += signedVol;
                wx += signedVol * (a.x + b.x + c.x) * 0.25;
                wy += signedVol * (a.y + b.y + c.y) * 0.25;
                wz += signedVol * (a.z + b.z + c.z) * 0.25;
            }

            if (System.Math.Abs(sumVol) < MinVolume) return false;

            volume = (float)System.Math.Abs(sumVol);
            centroid = new Vector3(
                (float)(wx / sumVol), (float)(wy / sumVol), (float)(wz / sumVol));
            return true;
        }
    }
}
