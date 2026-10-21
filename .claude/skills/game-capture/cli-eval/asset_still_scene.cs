// Worked example (asteroid meshes): adapt the mesh source; the wireframe half is subject-agnostic.
// Builds unsaved objects under MESH_TOUR — load an empty scene before closing the editor.
var old = UnityEngine.GameObject.Find("MESH_TOUR");
if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("MESH_TOUR");
var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Asteroids.Spawning.AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
var prefab = s.asteroidPrefab.GetComponent<UnityEngine.MeshRenderer>();
var unlit = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");
float x = 0f;
for (int i = 0; i < s.meshInfos.Length; i++)
{
    var m = s.meshInfos[i];
    float r = m.mesh.bounds.extents.magnitude;
    x += r;
    var go = new UnityEngine.GameObject("E" + (i + 1) + " render=" + m.mesh.name);
    go.transform.SetParent(root.transform);
    go.transform.position = new UnityEngine.Vector3(x, 0f, 0f);
    go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = m.mesh;
    go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = prefab.sharedMaterial;

    var cm = m.colliderMesh;
    bool embedded = UnityEditor.AssetDatabase.GetAssetPath(cm).EndsWith(".asset");
    var tris = cm.triangles;
    var lines = new int[tris.Length * 2];
    for (int t = 0; t < tris.Length; t += 3)
    {
        lines[t * 2] = tris[t]; lines[t * 2 + 1] = tris[t + 1];
        lines[t * 2 + 2] = tris[t + 1]; lines[t * 2 + 3] = tris[t + 2];
        lines[t * 2 + 4] = tris[t + 2]; lines[t * 2 + 5] = tris[t];
    }
    var wire = new UnityEngine.Mesh { name = cm.name + "_wire", hideFlags = UnityEngine.HideFlags.DontSave };
    wire.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    wire.vertices = cm.vertices;
    wire.SetIndices(lines, UnityEngine.MeshTopology.Lines, 0);
    var wgo = new UnityEngine.GameObject("collider=" + cm.name + (embedded ? " [EMBEDDED]" : ""));
    wgo.transform.SetParent(go.transform, false);
    wgo.AddComponent<UnityEngine.MeshFilter>().sharedMesh = wire;
    var mat = new UnityEngine.Material(unlit) { hideFlags = UnityEngine.HideFlags.DontSave };
    mat.SetColor("_BaseColor", embedded ? UnityEngine.Color.red : UnityEngine.Color.green);
    wgo.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = mat;
    x += r + 1.5f;
}
return "built " + s.meshInfos.Length + " rocks, row length " + x.ToString("F1");
