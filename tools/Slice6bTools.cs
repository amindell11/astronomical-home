using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Slice6bTools
{
    const string Dir = "C:/Users/amind/AppData/Local/Temp/claude/D--amind-git-astronomical-home/6f5d1708-20a6-4fee-b8e9-0aeb9197aab4/scratchpad/";

    static string Id(UnityEngine.Object o)
    {
        if (o == null) return "null";
        string id = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long lid) ? g + ":" + lid : "scene:" + o.name;
        if (o is Material m)
            id += " sh=" + (m.shader != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m.shader, out string sg, out long _) ? sg : "none");
        return id;
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

    static string PathOf(Transform t)
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
            string head = $"{key}|{PathOf(c.transform)}|{c.GetType().Name}|{Array.IndexOf(c.GetComponents(c.GetType()), c)}";
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

    public static string Snapshot(string outName)
    {
        var lines = new List<string>();
        var guids = File.ReadAllLines(Dir + "targets.txt").Where(l => l.Trim().Length > 0).ToArray();
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
        File.WriteAllLines(Dir + outName, lines);
        return $"{guids.Length} targets, {lines.Count} component lines";
    }

    /// moves.tsv: from\tto per line. Creates folders, then MoveAsset each; throws on first error.
    public static string Move()
    {
        var pairs = File.ReadAllLines(Dir + "moves.tsv").Where(l => l.Trim().Length > 0).Select(l => l.Split('\t')).ToArray();
        foreach (var pr in pairs) EnsureFolder(Path.GetDirectoryName(pr[1]).Replace('\\', '/'));
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var parts in pairs)
            {
                string err = AssetDatabase.MoveAsset(parts[0], parts[1]);
                if (!string.IsNullOrEmpty(err)) throw new Exception($"MoveAsset {parts[0]} -> {parts[1]}: {err}");
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        return $"{n} moved";
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
            throw new Exception("CreateFolder failed " + folder);
    }

    /// deletes file: one asset path per line (files first, then folders deepest-first).
    public static string Delete(string listName)
    {
        int n = 0;
        var failed = new List<string>();
        var paths = File.ReadAllLines(Dir + listName).Where(l => l.Trim().Length > 0).ToArray();
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

    public static string Reimport(string listName)
    {
        var paths = File.ReadAllLines(Dir + listName).Where(l => l.Trim().Length > 0).ToArray();
        foreach (var p in paths)
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return $"{paths.Length} reimported";
    }

    /// shaders.txt: one shader GUID per line. Per shader: name, Shader.Find round trip, errors, pass names.
    public static string ShaderCheck(string outName)
    {
        var sb = new StringBuilder();
        foreach (var guid in File.ReadAllLines(Dir + "shaders.txt").Where(l => l.Trim().Length > 0))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) throw new Exception("no shader at " + path);
            var messages = ShaderUtil.GetShaderMessages(shader);
            int errors = messages.Count(m => m.severity == ShaderCompilerMessageSeverity.Error);
            var data = ShaderUtil.GetShaderData(shader);
            var subs = new List<string>();
            for (int s = 0; s < data.SubshaderCount; s++)
            {
                var sub = data.GetSubshader(s);
                var names = new List<string>();
                for (int p = 0; p < sub.PassCount; p++) names.Add(sub.GetPass(p).Name);
                subs.Add("[" + string.Join(",", names) + "]");
            }
            sb.AppendLine($"{guid}|{path}|name={shader.name}|find={(Shader.Find(shader.name) == shader)}|hasError={ShaderUtil.ShaderHasError(shader)}|errors={errors}|messages={messages.Length}|supported={shader.isSupported}|passes={string.Join(";", subs)}");
            foreach (var m in messages) sb.AppendLine($"  {m.severity}: {m.message} ({m.file}:{m.line})");
        }
        File.WriteAllText(Dir + outName, sb.ToString());
        return sb.ToString();
    }
}
