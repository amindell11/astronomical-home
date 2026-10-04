using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class VanguardDump
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;
    static string F(float f) => f.ToString("R", C);
    static string V(Vector3 v) => $"({F(v.x)},{F(v.y)},{F(v.z)})";
    static string Q(Quaternion q) => $"({F(q.x)},{F(q.y)},{F(q.z)},{F(q.w)})";
    static string Mx(Matrix4x4 m) { var sb = new StringBuilder("["); for (var i = 0; i < 16; i++) sb.Append(i == 0 ? "" : ",").Append(F(m[i % 4, i / 4])); return sb.Append("]").ToString(); }

    public static string PathOf(Transform t, Transform root)
    {
        var names = new List<string>();
        for (var c = t; c && c != root; c = c.parent) names.Add(c.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string Logical(Object o, Transform root)
    {
        if (!o) return "null";
        var go = o as GameObject;
        var comp = o as Component;
        var t = go ? go.transform : comp ? comp.transform : null;
        if (!t) return $"<{o.GetType().Name}:{o.name}>";
        var idx = "";
        if (comp)
        {
            var same = t.GetComponents(comp.GetType());
            if (same.Length > 1) idx = "[" + Array.IndexOf(same, comp) + "]";
        }
        return $"/{PathOf(t, root)}#{o.GetType().Name}{idx}";
    }

    static string RefString(Object o, Transform prefabRoot)
    {
        if (ReferenceEquals(o, null)) return "null";
        if (!o)
        {
            var id = o.GetInstanceID();
            return id == 0 ? "null" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out var g, out long l) ? $"MISSING({g}:{l})" : $"MISSING(iid {id})";
        }
        var path = AssetDatabase.GetAssetPath(o);
        var comp = o as Component; var go = o as GameObject;
        var t = go ? go.transform : comp ? comp.transform : null;
        if (t && prefabRoot && t.root == prefabRoot.root) return "self:" + Logical(o, prefabRoot);
        if (string.IsNullOrEmpty(path)) return $"scene:{(t ? Logical(o, t.root) : o.name)}";
        if (t) return $"{path}:{Logical(o, t.root)}";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out var gg, out long ll);
        return $"{path}::{o.GetType().Name}:{o.name}:{ll}";
    }

    static void DumpProps(StringBuilder sb, Object o, Transform root, string indent)
    {
        var so = new SerializedObject(o);
        var it = so.GetIterator();
        var enter = true;
        while (it.Next(enter))
        {
            enter = it.propertyType == SerializedPropertyType.Generic || it.isArray && it.propertyType != SerializedPropertyType.String;
            if (it.propertyPath == "m_ObjectHideFlags" || it.propertyPath.StartsWith("m_CorrespondingSourceObject") || it.propertyPath.StartsWith("m_PrefabInstance") || it.propertyPath.StartsWith("m_PrefabAsset") || it.propertyPath == "m_GameObject" || it.propertyPath == "m_Father" || it.propertyPath.StartsWith("m_Children") || it.propertyPath.StartsWith("m_Component")) continue;
            string val;
            switch (it.propertyType)
            {
                case SerializedPropertyType.ObjectReference: val = RefString(it.objectReferenceValue, root); break;
                case SerializedPropertyType.Float: val = F(it.floatValue); break;
                case SerializedPropertyType.Integer: val = it.longValue.ToString(C); break;
                case SerializedPropertyType.Boolean: val = it.boolValue ? "1" : "0"; break;
                case SerializedPropertyType.String: val = it.stringValue; break;
                case SerializedPropertyType.Enum: val = it.enumValueIndex.ToString(C); break;
                case SerializedPropertyType.Color: var c = it.colorValue; val = $"({F(c.r)},{F(c.g)},{F(c.b)},{F(c.a)})"; break;
                case SerializedPropertyType.Vector2: val = it.vector2Value.ToString("R"); break;
                case SerializedPropertyType.Vector3: val = V(it.vector3Value); break;
                case SerializedPropertyType.Vector4: val = it.vector4Value.ToString("R"); break;
                case SerializedPropertyType.Quaternion: val = Q(it.quaternionValue); break;
                case SerializedPropertyType.LayerMask: val = it.intValue.ToString(C); break;
                case SerializedPropertyType.ArraySize: val = it.intValue.ToString(C); break;
                case SerializedPropertyType.Rect: val = it.rectValue.ToString("R"); break;
                case SerializedPropertyType.Bounds: val = it.boundsValue.ToString("R"); break;
                case SerializedPropertyType.AnimationCurve: val = string.Join(";", it.animationCurveValue.keys.Select(k => $"{F(k.time)}:{F(k.value)}:{F(k.inTangent)}:{F(k.outTangent)}")); break;
                case SerializedPropertyType.Gradient: val = "gradient"; break;
                case SerializedPropertyType.ManagedReference: val = "managedref"; break;
                default: if (it.hasChildren) continue; val = $"<{it.propertyType}>"; break;
            }
            sb.Append(indent).Append(it.propertyPath).Append(" = ").Append(val).Append('\n');
        }
    }

    /// Full serialized dump plus world matrices of the prefab asset at prefabPath.
    public static string DumpStructure(string prefabPath, string outPath)
    {
        var root = (GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath);
        var sb = new StringBuilder();
        var world = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var p = PathOf(t, root.transform);
            sb.Append($"== /{p} active={t.gameObject.activeSelf} layer={t.gameObject.layer} tag={t.gameObject.tag}\n");
            foreach (var comp in t.GetComponents<Component>())
            {
                if (!comp) { sb.Append("  -- <missing script>\n"); continue; }
                sb.Append($"  -- {Logical(comp, root.transform)}\n");
                DumpProps(sb, comp, root.transform, "     ");
                var kind = comp is Renderer ? "renderer" : comp is ParticleSystem ? "emitter" : comp is Collider ? "collider" : comp is Light ? "light" : comp is AudioSource ? "audio" : comp is Canvas ? "canvas" : null;
                if (kind != null) world.Append($"{kind} {Logical(comp, root.transform)} {Mx(t.localToWorldMatrix)}\n");
            }
            world.Append($"transform {Logical(t, root.transform)} {Mx(t.localToWorldMatrix)}\n");
        }
        var weapons = root.GetComponent("WeaponsController");
        if (weapons)
        {
            var hp = new SerializedObject(weapons).FindProperty("hardpoints");
            for (var i = 0; i < hp.arraySize; i++)
            {
                var mount = (Transform)hp.GetArrayElementAtIndex(i).FindPropertyRelative("mount").objectReferenceValue;
                world.Append($"hardpoint[{i}] {Logical(mount, root.transform)} {Mx(mount.localToWorldMatrix)}\n");
            }
        }
        File.WriteAllText(outPath + ".props.txt", sb.ToString());
        File.WriteAllText(outPath + ".world.txt", world.ToString());
        return $"ok {prefabPath}: {root.GetComponentsInChildren<Transform>(true).Length} transforms";
    }

    /// PhysX-computed inertia of a live instance, at the authored mass and at the runtime mass (Ship.mass).
    public static string DumpInertia(string prefabPath, string outPath)
    {
        var prefab = (GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        var sb = new StringBuilder();
        try
        {
            inst.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.rotation);
            Physics.SyncTransforms();
            var rb = inst.GetComponent<Rigidbody>();
            void Line(string label) => sb.Append($"{label}: mass={F(rb.mass)} automaticTensor={rb.automaticInertiaTensor} automaticCOM={rb.automaticCenterOfMass} tensor={V(rb.inertiaTensor)} rotation={Q(rb.inertiaTensorRotation)} com={V(rb.centerOfMass)} drag={F(rb.linearDamping)} angDrag={F(rb.angularDamping)}\n");
            Line("authored");
            var shipMass = new SerializedObject(inst.GetComponent("Ship")).FindProperty("mass").floatValue;
            rb.mass = shipMass;
            Line("after rb.mass=Ship.mass");
            if (rb.automaticInertiaTensor) rb.ResetInertiaTensor();
            Line("runtime (Initialize order: mass, then ResetInertiaTensor if automatic)");
        }
        finally { Object.DestroyImmediate(inst); }
        File.WriteAllText(outPath, sb.ToString());
        return sb.ToString();
    }

    /// fileID -> logical object for every object the prefab asset exposes.
    public static string DumpFileIds(string prefabPath, string outPath)
    {
        var root = ((GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath)).transform;
        var sb = new StringBuilder();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(prefabPath))
        {
            if (!o) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out var g, out long id);
            sb.Append($"{id} {Logical(o, root)}\n");
        }
        File.WriteAllText(outPath, sb.ToString());
        return "ok";
    }

    static IEnumerable<Object> ObjectsUnder(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            yield return t.gameObject;
            foreach (var c in t.GetComponents<Component>()) if (c) yield return c;
        }
    }

    static string ModLine(PropertyModification m, Transform srcRoot)
    {
        return $"target={RefString(m.target, srcRoot)} path={m.propertyPath} value={m.value} ref={RefString(m.objectReference, null)}";
    }

    /// Every reference to the chassis asset (direct or through a nested instance) in one file, by logical object.
    public static string DumpReferences(string chassisPath, string filePath, string outPath)
    {
        var chassisRoot = ((GameObject)AssetDatabase.LoadMainAssetAtPath(chassisPath)).transform;
        var sb = new StringBuilder();
        var roots = new List<GameObject>();
        Scene scene = default;
        var isScene = filePath.EndsWith(".unity");
        var isPrefab = filePath.EndsWith(".prefab");
        GameObject contents = null;
        try
        {
            if (isScene) { scene = EditorSceneManager.OpenScene(filePath, OpenSceneMode.Additive); roots.AddRange(scene.GetRootGameObjects()); }
            else if (isPrefab) { contents = PrefabUtility.LoadPrefabContents(filePath); roots.Add(contents); }
            var all = new List<Object>();
            if (roots.Count > 0) foreach (var r in roots) all.AddRange(ObjectsUnder(r));
            else all.AddRange(AssetDatabase.LoadAllAssetsAtPath(filePath).Where(o => o));

            // nested instances of the chassis
            var instances = new List<GameObject>();
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == chassisPath)
                        instances.Add(t.gameObject);
            foreach (var inst in instances)
            {
                var ip = PathOf(inst.transform, inst.transform.root);
                sb.Append($"INSTANCE /{(inst.transform.parent ? PathOf(inst.transform, inst.transform.root) : inst.name)} world={Mx(inst.transform.localToWorldMatrix)} activeSelf={inst.activeSelf}\n");
                foreach (var m in PrefabUtility.GetPropertyModifications(inst))
                    sb.Append($"  MOD {ModLine(m, chassisRoot)} default={(m.target ? PrefabUtility.IsDefaultOverride(m) : false)}\n");
                foreach (var rc in PrefabUtility.GetRemovedComponents(inst)) sb.Append($"  REMOVED-COMPONENT {RefString(rc.assetComponent, chassisRoot)}\n");
                foreach (var rg in PrefabUtility.GetRemovedGameObjects(inst)) sb.Append($"  REMOVED-GO {RefString(rg.assetGameObject, chassisRoot)}\n");
                foreach (var ag in PrefabUtility.GetAddedGameObjects(inst)) sb.Append($"  ADDED-GO {Logical(ag.instanceGameObject, inst.transform)}\n");
                foreach (var ac in PrefabUtility.GetAddedComponents(inst)) sb.Append($"  ADDED-COMPONENT {Logical(ac.instanceComponent, inst.transform)}\n");
                foreach (var o in ObjectsUnder(inst))
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(o);
                    if (src && AssetDatabase.GetAssetPath(src) == chassisPath && Logical(o, inst.transform) != Logical(src, chassisRoot))
                        sb.Append($"  MISMATCH {Logical(o, inst.transform)} <- {Logical(src, chassisRoot)}\n");
                }
            }

            // object references anywhere in the file that land in the chassis asset or inside one of its instances
            foreach (var o in all)
            {
                var so = new SerializedObject(o);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.propertyPath == "m_CorrespondingSourceObject" || it.propertyPath == "m_PrefabAsset" || it.propertyPath == "m_PrefabInstance") continue;
                    var v = it.objectReferenceValue;
                    if (!v) continue;
                    var target = v;
                    string where = null;
                    if (AssetDatabase.GetAssetPath(v) == chassisPath) where = "asset:" + Logical(v, chassisRoot);
                    else
                    {
                        var comp = v as Component; var go = v as GameObject;
                        var t = go ? go.transform : comp ? comp.transform : null;
                        var inst = t ? instances.FirstOrDefault(i => t.IsChildOf(i.transform)) : null;
                        if (inst) where = $"instance:{inst.name}:" + Logical(v, inst.transform);
                    }
                    if (where == null) continue;
                    var holder = o is Component hc ? Logical(hc, hc.transform.root) : o is GameObject hg ? Logical(hg, hg.transform.root) : $"<{o.GetType().Name}:{o.name}>";
                    var insideInstance = (o is Component ic && instances.Any(i => ic.transform.IsChildOf(i.transform))) || (o is GameObject ig && instances.Any(i => ig.transform.IsChildOf(i.transform)));
                    if (insideInstance && where.StartsWith("instance:")) continue; // the chassis's own internal wiring
                    sb.Append($"REF {holder} .{it.propertyPath} -> {where}\n");
                }
            }
        }
        finally
        {
            if (contents) PrefabUtility.UnloadPrefabContents(contents);
            if (isScene && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
        File.WriteAllText(outPath, sb.ToString());
        return sb.ToString();
    }

    public static string DumpStats(string prefabPath, string outPath)
    {
        var ship = ((GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath)).GetComponent<Ships.Ship>();
        var lines = (System.Collections.Generic.SortedSet<string>)typeof(Balance.StatHash).GetMethod("Lines", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { ship });
        var hash = Balance.StatHash.OfLoadout(ship);
        File.WriteAllText(outPath, "hash=" + hash + "\n" + string.Join("\n", lines) + "\n");
        return $"hash={hash} lines={lines.Count}";
    }
}
