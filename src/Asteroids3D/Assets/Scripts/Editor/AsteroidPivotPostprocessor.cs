using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Import-time repivot for the HD asteroid FBXs. Several of the source meshes
    /// have their origin offset from the geometric centre of mass by up to ~20% of
    /// their radius. That offset makes the frozen AI obstacle circle drift as the
    /// rock tumbles and makes the rigidbody gyrate about an off-centre pivot.
    ///
    /// This postprocessor recentres the imported geometry on its VOLUME centroid
    /// (signed-tetrahedron, not the tessellation-biased vertex mean) so the visual
    /// mesh, the physics centre of mass, and the AI circle all agree — without a
    /// destructive edit to the source FBX or any runtime cost.
    ///
    /// It is idempotent by construction: it always translates by the freshly
    /// computed centroid of the untouched source geometry, so re-importing an
    /// already-centred result recomputes a ~0 centroid and is a no-op. No "done"
    /// flag is persisted.
    /// </summary>
    public class AsteroidPivotPostprocessor : AssetPostprocessor
    {
        // Only asteroid models under this folder are recentred.
        private const string ModelsFolder =
            "Assets/Visuals/Environment/Asteroids/HD_Asteroids/Models/";

        // Below this absolute signed volume the mesh is treated as non-closed /
        // degenerate and left untouched (translating by a garbage centroid would
        // be worse than leaving the original pivot).
        private const float MinVolume = 1e-9f;

        private void OnPostprocessModel(GameObject root)
        {
            if (root == null) return;

            string path = assetImporter != null ? assetImporter.assetPath : assetPath;
            if (string.IsNullOrEmpty(path) || !path.Replace('\\', '/').StartsWith(ModelsFolder))
                return;

            // Collect every unique mesh in the model (visual filters + colliders),
            // deduped by reference so shared meshes are translated exactly once.
            var meshes = new List<Mesh>();
            var seen = new HashSet<Mesh>();

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                AddMesh(mf != null ? mf.sharedMesh : null, meshes, seen);
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                AddMesh(mc != null ? mc.sharedMesh : null, meshes, seen);

            if (meshes.Count == 0) return;

            // The primary visual mesh is the highest-vertex mesh (LOD0). We anchor
            // the whole model on ITS volume centroid so all LODs / colliders stay
            // registered to one another.
            Mesh primary = meshes[0];
            for (int i = 1; i < meshes.Count; i++)
                if (meshes[i].vertexCount > primary.vertexCount)
                    primary = meshes[i];

            if (!TryComputeVolumeCentroid(primary, out Vector3 centroid))
            {
                Debug.LogWarning(
                    $"[AsteroidPivot] {root.name}: primary mesh '{primary.name}' has degenerate " +
                    $"volume (|sumVol| < {MinVolume}); left un-recentred.");
                return;
            }

            if (centroid == Vector3.zero) return; // already centred; nothing to do.

            // Translate EVERY mesh by the same vector so visual and collider geometry
            // stay in lockstep, then refresh bounds.
            foreach (var mesh in meshes)
            {
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                    verts[i] -= centroid;
                mesh.vertices = verts;
                mesh.RecalculateBounds();
            }

            Debug.Log($"[AsteroidPivot] {root.name}: recentered by {centroid.ToString("F5")} " +
                      $"(primary '{primary.name}', {meshes.Count} mesh(es))");
        }

        private static void AddMesh(Mesh mesh, List<Mesh> meshes, HashSet<Mesh> seen)
        {
            if (mesh != null && seen.Add(mesh)) meshes.Add(mesh);
        }

        /// <summary>
        /// Signed-tetrahedron volume centroid. Each triangle (a,b,c) forms a
        /// tetrahedron with the origin; its signed volume is dot(a, cross(b,c))/6
        /// and its centroid is (a+b+c+origin)/4 = (a+b+c)*0.25. The volume-weighted
        /// mean of the tet centroids is the solid's centre of mass, independent of
        /// how densely each face is tessellated.
        /// </summary>
        private static bool TryComputeVolumeCentroid(Mesh mesh, out Vector3 centroid)
        {
            centroid = Vector3.zero;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            if (verts.Length == 0 || tris.Length < 3) return false;

            double sumVol = 0.0;
            double wx = 0.0, wy = 0.0, wz = 0.0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                double signedVol = Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                double tcx = (a.x + b.x + c.x) * 0.25;
                double tcy = (a.y + b.y + c.y) * 0.25;
                double tcz = (a.z + b.z + c.z) * 0.25;
                sumVol += signedVol;
                wx += signedVol * tcx;
                wy += signedVol * tcy;
                wz += signedVol * tcz;
            }

            if (System.Math.Abs(sumVol) < MinVolume) return false;

            centroid = new Vector3(
                (float)(wx / sumVol), (float)(wy / sumVol), (float)(wz / sumVol));
            return true;
        }
    }
}
