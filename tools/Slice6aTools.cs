using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Slice6aTools
{
    static string Id(UnityEngine.Object o)
    {
        if (o == null) return "null";
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long lid))
            return g + ":" + lid;
        return "scene:" + o.name;
    }

    static string V(Vector3 v) => $"({v.x:F5},{v.y:F5},{v.z:F5})";

    static string M(Matrix4x4 m)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 16; i++) sb.Append(m[i].ToString("F5")).Append(i < 15 ? "," : "");
        return sb.ToString();
    }

    static string MeshDesc(Mesh mesh)
    {
        if (mesh == null) return "null";
        var b = mesh.bounds;
        return $"{Id(mesh)} v={mesh.vertexCount} c={V(b.center)} e={V(b.extents)}";
    }

    static string Path(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name + "#" + t.GetSiblingIndex());
        parts.Reverse();
        return string.Join("/", parts);
    }

    static void Describe(GameObject root, string key, List<string> lines)
    {
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            string head = $"{key}|{Path(c.transform)}|{c.GetType().Name}|{Array.IndexOf(c.GetComponents(c.GetType()), c)}";
            string body;
            switch (c)
            {
                case ParticleSystemRenderer psr:
                    var ms = new Mesh[psr.meshCount];
                    psr.GetMeshes(ms);
                    body = $"mesh={MeshDesc(psr.mesh)} meshes=[{string.Join(";", ms.Select(MeshDesc))}] mats=[{string.Join(";", psr.sharedMaterials.Select(Id))}]";
                    break;
                case SkinnedMeshRenderer smr:
                    body = $"mesh={MeshDesc(smr.sharedMesh)} mats=[{string.Join(";", smr.sharedMaterials.Select(Id))}]";
                    break;
                case Renderer r:
                    var mf = r.GetComponent<MeshFilter>();
                    body = $"mesh={MeshDesc(mf != null ? mf.sharedMesh : null)} mats=[{string.Join(";", r.sharedMaterials.Select(Id))}]";
                    break;
                case MeshCollider mc:
                    body = $"mesh={MeshDesc(mc.sharedMesh)} convex={mc.convex} pm={Id(mc.sharedMaterial)}";
                    break;
                case BoxCollider bc:
                    body = $"center={V(bc.center)} size={V(bc.size)} pm={Id(bc.sharedMaterial)}";
                    break;
                case SphereCollider sc:
                    body = $"center={V(sc.center)} r={sc.radius:F5} pm={Id(sc.sharedMaterial)}";
                    break;
                case CapsuleCollider cc:
                    body = $"center={V(cc.center)} r={cc.radius:F5} h={cc.height:F5} d={cc.direction} pm={Id(cc.sharedMaterial)}";
                    break;
                case Collider col:
                    body = $"pm={Id(col.sharedMaterial)}";
                    break;
                default:
                    continue;
            }
            lines.Add($"{head}|{body}|world={M(c.transform.localToWorldMatrix)}");
        }
    }

    /// targetsFile: one asset GUID per line (prefabs and scenes). Writes outFile, returns line count.
    public static string Snapshot(string targetsFile, string outFile)
    {
        var lines = new List<string>();
        var guids = File.ReadAllLines(targetsFile).Where(l => l.Trim().Length > 0).ToArray();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) throw new Exception("unresolved target " + guid);
            if (path.EndsWith(".prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Describe(root, guid, lines); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            else if (path.EndsWith(".unity"))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var go in scene.GetRootGameObjects()) Describe(go, guid, lines);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            else throw new Exception("unexpected target type " + path);
        }
        lines.Sort(StringComparer.Ordinal);
        File.WriteAllLines(outFile, lines);
        return $"{guids.Length} targets, {lines.Count} component lines";
    }

    /// Opens EditScene, deletes every root GameObject whose renderers/filters use a mesh from the
    /// given folders (Wheel/Spindle/Dildo), saves. Returns the deleted roots.
    public static string DeleteEditScenePlacements(string scenePath, string[] folders, bool dryRun)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var deleted = new List<string>();
        try
        {
            var doomed = new HashSet<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var deps = new List<UnityEngine.Object>();
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf) deps.Add(mf.sharedMesh);
                    var r = t.GetComponent<Renderer>();
                    if (r) deps.AddRange(r.sharedMaterials);
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                    if (src) deps.Add(src);
                    foreach (var d in deps)
                    {
                        if (d == null) continue;
                        string p = AssetDatabase.GetAssetPath(d);
                        if (folders.Any(f => p.StartsWith(f + "/")))
                        {
                            // delete the outermost prefab instance root (or the object itself)
                            var victim = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) ?? t.gameObject;
                            doomed.Add(victim);
                        }
                    }
                }
            }
            foreach (var go in doomed)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                deleted.Add(Path(go.transform) + " src=" + (src ? AssetDatabase.GetAssetPath(src) : "none"));
            }
            if (dryRun) return string.Join("\n", deleted);
            foreach (var go in doomed) UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("SaveScene failed");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        return string.Join("\n", deleted);
    }

    /// moves.tsv: from\tto per line. Creates folders, MoveAsset each; throws on first error.
    public static string Move(string movesFile)
    {
        var log = new List<string>();
        var pairs = File.ReadAllLines(movesFile).Where(l => l.Trim().Length > 0).Select(l => l.Split('\t')).ToArray();
        foreach (var pr in pairs) EnsureFolder(System.IO.Path.GetDirectoryName(pr[1]).Replace('\\', '/'));
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var parts in pairs)
            {
                string from = parts[0], to = parts[1];
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) throw new Exception($"MoveAsset {from} -> {to}: {err}");
                log.Add(to);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        return $"{log.Count} moved";
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        if (string.IsNullOrEmpty(guid)) throw new Exception("CreateFolder failed " + folder);
    }

    /// deletes.txt: one asset path per line (files first, then folders deepest-first).
    public static string Delete(string deletesFile)
    {
        int n = 0;
        var failed = new List<string>();
        var paths = File.ReadAllLines(deletesFile).Where(l => l.Trim().Length > 0).ToArray();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var p in paths)
            {
                if (AssetDatabase.DeleteAsset(p)) n++;
                else failed.Add(p);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        if (failed.Count > 0) throw new Exception("DeleteAsset failed: " + string.Join(", ", failed));
        return $"{n} deleted";
    }

    /// Force-reimports each path (one per line).
    public static string Reimport(string pathsFile)
    {
        var paths = File.ReadAllLines(pathsFile).Where(l => l.Trim().Length > 0).ToArray();
        foreach (var p in paths)
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return $"{paths.Length} reimported";
    }

    /// Volume centroid of each model's primary (highest-vertex) mesh, after import.
    public static string PivotProof(string folder)
    {
        var sb = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToList();
            var primary = meshes.OrderByDescending(m => m.vertexCount).First();
            if (!Asteroids.AsteroidMeshVolume.TryCompute(primary, out float vol, out Vector3 c))
                throw new Exception("degenerate " + path);
            sb.AppendLine($"{System.IO.Path.GetFileName(path)} primary='{primary.name}' verts={primary.vertexCount} |centroid|={c.magnitude:E3} centroid={c.ToString("F6")}");
        }
        return sb.ToString();
    }
}
