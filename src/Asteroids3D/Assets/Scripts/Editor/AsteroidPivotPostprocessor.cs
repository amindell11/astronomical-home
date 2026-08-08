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

            if (!AsteroidMeshVolume.TryCompute(primary, out _, out Vector3 centroid))
            {
                Debug.LogWarning(
                    $"[AsteroidPivot] {root.name}: primary mesh '{primary.name}' has degenerate " +
                    $"volume (|sumVol| < {AsteroidMeshVolume.MinVolume}); left un-recentred.");
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

    }
}
