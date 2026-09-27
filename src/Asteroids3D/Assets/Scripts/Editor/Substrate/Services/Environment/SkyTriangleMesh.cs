using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// Generates the fullscreen triangle every environment layer renders with. The sky shaders read
    /// its vertices as clip-space positions; the huge bounds keep it from being frustum-culled
    /// wherever the flight camera travels.
    /// </summary>
    public static class SkyTriangleMesh
    {
        public const string Path = "Assets/Visuals/Environment/Sky/SkyTriangle.asset";

        [MenuItem("Tools/Environment/Generate Sky Triangle")]
        public static void Generate()
        {
            var mesh = new Mesh
            {
                name = "SkyTriangle",
                vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(mesh, Path);
                return;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            AssetDatabase.SaveAssets();
        }
    }
}
