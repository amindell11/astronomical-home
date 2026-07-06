using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Tight in-plane obstacle-circle radii from actual collider geometry, replacing
    /// axis-aligned-bounds estimates (which over-report rotated meshes and silently
    /// under-report down-scaled ones).
    ///
    /// For meshes the radius is the maximum vertex distance from the transform origin
    /// (times scale). This is rotation-INVARIANT: asteroids spawn with uniform random 3D
    /// rotation and tumble under ambient drift, so any rotation-dependent radius cached at
    /// spawn would go stale. The max vertex norm is exactly the smallest circle (centered
    /// on the pivot) that contains the plane projection of the collider under the
    /// worst-case rotation — i.e. the tightest safe bound for a tumbling body.
    ///
    /// The per-mesh vertex pass runs once per mesh (static cache), never per query.
    /// </summary>
    public static class ColliderPlanarRadius
    {
        private static readonly Dictionary<Mesh, float> MaxNormCache = new();

        /// <summary>
        /// Max distance of any vertex from the mesh origin (local space, unscaled).
        /// Cached per mesh; the vertex pass runs once. Falls back to the local AABB's
        /// farthest corner for non-readable meshes (conservative, never under-reports).
        /// </summary>
        public static float MaxVertexNorm(Mesh mesh)
        {
            if (!mesh) return 0f;
            if (MaxNormCache.TryGetValue(mesh, out var cached)) return cached;

            float maxSq;
            if (mesh.isReadable)
            {
                maxSq = 0f;
                var verts = mesh.vertices;
                for (var i = 0; i < verts.Length; i++)
                {
                    var sq = verts[i].sqrMagnitude;
                    if (sq > maxSq) maxSq = sq;
                }
            }
            else
            {
                var b = mesh.bounds;
                var corner = new Vector3(
                    Mathf.Abs(b.center.x) + b.extents.x,
                    Mathf.Abs(b.center.y) + b.extents.y,
                    Mathf.Abs(b.center.z) + b.extents.z);
                maxSq = corner.sqrMagnitude;
            }

            var result = Mathf.Sqrt(maxSq);
            MaxNormCache[mesh] = result;
            return result;
        }

        /// <summary>Largest per-axis scale magnitude — the factor a vertex norm can grow by.</summary>
        public static float MaxAbsScale(Vector3 lossyScale) =>
            Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));

        /// <summary>World-space rotation-invariant bounding radius of a mesh collider's geometry
        /// about its transform origin.</summary>
        public static float MeshWorldRadius(Mesh mesh, Vector3 lossyScale) =>
            MaxVertexNorm(mesh) * MaxAbsScale(lossyScale);
    }
}
