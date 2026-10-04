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

public static class VanguardMigrate
{
    const string Ship1 = "Assets/Prefabs/Ships/Ship_1.prefab";
    const string Ship1Guid = "c3c15225cb3a8fd42a87c756c1d2d884";
    const string BasePath = "Assets/Prefabs/Ships/ShipBase.prefab";
    static readonly CultureInfo C = CultureInfo.InvariantCulture;
    static string F(float f) => f.ToString("R", C);
    static string V(Vector3 v) => $"({F(v.x)},{F(v.y)},{F(v.z)})";

    static readonly string[] YamlExt = { ".prefab", ".unity", ".asset", ".mat", ".controller", ".overrideController", ".anim", ".playable", ".mask", ".preset", ".lighting", ".physicMaterial", ".signal", ".spriteatlas", ".shadergraph", ".shadersubgraph", ".vfx", ".terrainlayer", ".brush", ".mixer", ".guiskin", ".fontsettings", ".cubemap", ".flare", ".renderTexture", ".giparams", ".asmdef", ".asmref", ".inputactions", ".uss", ".uxml", ".tss" };

    /// Files under Assets/ and ProjectSettings/ (other than the asset itself and its .meta) whose text mentions the guid.
    public static List<string> Referrers(string assetPath)
    {
        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) throw new Exception("no guid for " + assetPath);
        var hits = new List<string>();
        foreach (var root in new[] { "Assets", "ProjectSettings" })
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var n = f.Replace('\\', '/');
                if (n == assetPath || n == assetPath + ".meta" || n.EndsWith(".meta")) continue;
                if (!YamlExt.Any(e => n.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                if (File.ReadAllText(n).Contains(guid)) hits.Add(n);
            }
        return hits;
    }

    static void DeleteIfUnreferenced(string path, StringBuilder log)
    {
        var refs = Referrers(path);
        if (refs.Count > 0) throw new Exception($"{path} still referenced by {string.Join(", ", refs)}");
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (!AssetDatabase.DeleteAsset(path)) throw new Exception("DeleteAsset failed: " + path);
        log.Append($"deleted {path} ({guid}); referrers immediately before: 0\n");
    }

    public static string RetireJuly(string logPath)
    {
        var log = new StringBuilder();
        const string july = "Assets/Prefabs/Ships/Ship_1_Vanguard.prefab";
        var catalog = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Ships/ItemCatalog.asset");
        var so = new SerializedObject(catalog);
        var chassis = so.FindProperty("chassis");
        var before = chassis.arraySize;
        var index = -1;
        for (var i = 0; i < chassis.arraySize; i++)
            if (AssetDatabase.GetAssetPath(chassis.GetArrayElementAtIndex(i).objectReferenceValue) == july) index = i;
        if (index < 0) throw new Exception("July chassis not in the catalog");
        chassis.DeleteArrayElementAtIndex(index);
        if (chassis.arraySize == before) chassis.DeleteArrayElementAtIndex(index);
        if (chassis.arraySize != before - 1) throw new Exception("catalog removal failed");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        log.Append($"ItemCatalog.chassis: removed [{index}] {july}; {before} -> {chassis.arraySize}\n");
        DeleteIfUnreferenced(july, log);
        DeleteIfUnreferenced("Assets/Prefabs/Ships/Ship_1_Vanguard_VisualRig.prefab", log);
        DeleteIfUnreferenced("Assets/Visuals/Ships/Vanguard/Vanguard.fbx", log);
        foreach (var mat in Directory.GetFiles("Assets/Visuals/Ships/Vanguard/Materials", "VNG_*.mat").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            DeleteIfUnreferenced(mat, log);
        File.WriteAllText(logPath, log.ToString());
        return log.ToString();
    }

    static Transform Find(Transform root, string path)
    {
        var t = root.Find(path);
        if (!t) throw new Exception("missing " + path + " under " + root.name);
        return t;
    }

    /// Overwrites Ship_1.prefab (same GUID) with a depth-1 variant of ShipBase carrying Ship_1's hull and slot values.
    public static string BuildVariant(string logPath)
    {
        var log = new StringBuilder();
        var guidBefore = AssetDatabase.AssetPathToGUID(Ship1);
        var scene = SceneManager.GetActiveScene();
        var legacyAsset = (GameObject)AssetDatabase.LoadMainAssetAtPath(Ship1);
        var legacy = (GameObject)PrefabUtility.InstantiatePrefab(legacyAsset, scene);
        var shipBase = (GameObject)AssetDatabase.LoadMainAssetAtPath(BasePath);
        var variant = (GameObject)PrefabUtility.InstantiatePrefab(shipBase, scene);
        try
        {
            legacy.transform.SetPositionAndRotation(Vector3.zero, legacyAsset.transform.rotation);
            variant.transform.SetPositionAndRotation(Vector3.zero, legacyAsset.transform.rotation);

            Physics.SyncTransforms();
            var legacyBody = legacy.GetComponent<Rigidbody>();
            legacyBody.mass = new SerializedObject(legacy.GetComponent("Ship")).FindProperty("mass").floatValue;
            legacyBody.ResetInertiaTensor();
            var tensor = legacyBody.inertiaTensor;
            var tensorRotation = legacyBody.inertiaTensorRotation;
            log.Append($"runtime inertia of Ship_1 (mass {F(legacyBody.mass)}): tensor={V(tensor)} rotation=({F(tensorRotation.x)},{F(tensorRotation.y)},{F(tensorRotation.z)},{F(tensorRotation.w)})\n");

            variant.transform.localScale = legacy.transform.localScale;
            foreach (var hp in new[] { "Hardpoints/Primary", "Hardpoints/Secondary" })
                Find(variant.transform, hp).localPosition = Find(legacy.transform, hp).localPosition;

            var meshCollider = Find(variant.transform, "Mesh").GetComponent<MeshCollider>();
            meshCollider.sharedMesh = Find(legacy.transform, "Mesh").GetComponent<MeshCollider>().sharedMesh;

            var rig = Find(variant.transform, "ShipBaseRig");
            var legacyRig = Find(legacy.transform, "Ship_1_IllustratedRig");
            Find(rig, "MinimapMarker").GetComponent<MeshFilter>().sharedMesh = Find(legacyRig, "MinimapMarker").GetComponent<MeshFilter>().sharedMesh;

            var exhaust = Find(rig, "Thruster/EngineExhaust");
            exhaust.position = Find(legacyRig, "Thruster/ThrustMain").position;
            var reactor = Find(rig, "Thruster/Reactor");
            reactor.position = Find(legacyRig, "Thruster/Reactor").position;
            log.Append($"EngineExhaust local={V(exhaust.localPosition)}; Reactor local={V(reactor.localPosition)}\n");

            var hullSlot = Find(rig, "Hull");
            var legacyHull = Find(legacyRig, "Model/Vanguard");
            var hull = Object.Instantiate(legacyHull.gameObject, hullSlot, false);
            hull.name = legacyHull.name;

            var breakup = new SerializedObject(rig.GetComponent("ShipBreakupVisual"));
            var legacyBreakup = new SerializedObject(legacyRig.GetComponent("ShipBreakupVisual"));
            breakup.FindProperty("debrisPrefab").objectReferenceValue = legacyBreakup.FindProperty("debrisPrefab").objectReferenceValue;
            breakup.FindProperty("hull").objectReferenceValue = hull.transform;
            breakup.ApplyModifiedPropertiesWithoutUndo();

            var body = new SerializedObject(variant.GetComponent<Rigidbody>());
            body.FindProperty("m_ImplicitTensor").boolValue = false;
            body.FindProperty("m_InertiaTensor").vector3Value = tensor;
            body.FindProperty("m_InertiaRotation").quaternionValue = tensorRotation;
            body.ApplyModifiedPropertiesWithoutUndo();

            foreach (var c in new Object[] { variant.transform, Find(variant.transform, "Hardpoints/Primary"), Find(variant.transform, "Hardpoints/Secondary"),
                         meshCollider, Find(rig, "MinimapMarker").GetComponent<MeshFilter>(), exhaust, reactor })
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);

            Object.DestroyImmediate(legacy);
            legacy = null;
            var saved = PrefabUtility.SaveAsPrefabAsset(variant, Ship1, out var success);
            if (!success || !saved) throw new Exception("SaveAsPrefabAsset failed");
            var guidAfter = AssetDatabase.AssetPathToGUID(Ship1);
            log.Append($"saved {Ship1}: guid {guidBefore} -> {guidAfter}; type={PrefabUtility.GetPrefabAssetType(saved)}; source={AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(saved))}\n");
            if (guidAfter != guidBefore) throw new Exception("GUID changed");
        }
        finally
        {
            if (legacy) Object.DestroyImmediate(legacy);
            if (variant) Object.DestroyImmediate(variant);
        }
        File.WriteAllText(logPath, log.ToString());
        return log.ToString();
    }

    static string Logical(Object o, Transform root)
    {
        var go = o as GameObject; var comp = o as Component;
        var t = go ? go.transform : comp ? comp.transform : null;
        if (!t) return null;
        var names = new List<string>();
        for (var c = t; c && c != root; c = c.parent) names.Add(c.name);
        names.Reverse();
        var idx = "";
        if (comp)
        {
            var same = t.GetComponents(comp.GetType());
            if (same.Length > 1) idx = "[" + Array.IndexOf(same, comp) + "]";
        }
        return $"/{string.Join("/", names)}#{o.GetType().Name}{idx}";
    }

    static IEnumerable<Object> ObjectsUnder(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            yield return t.gameObject;
            foreach (var c in t.GetComponents<Component>()) if (c) yield return c;
        }
    }

    /// Retargets every serialized pointer into the old Ship_1 objects (by persistent fileID, through the editor's
    /// SerializedObject API) to the same logical object of the rebuilt chassis; in-file pointers to a nested
    /// instance's objects are re-assigned from the recorded logical path. Unresolvable pointers stay as they are.
    public static string Remap(string filePath, string oldIdsPath, string inFileRefsJson, int siblingIndex, string logPath)
    {
        var chassisPath = AssetDatabase.GUIDToAssetPath(Ship1Guid);
        var log = new StringBuilder();
        var oldIds = File.ReadAllLines(oldIdsPath).Where(l => l.Length > 0).ToDictionary(l => long.Parse(l.Substring(0, l.IndexOf(' ')), C), l => l.Substring(l.IndexOf(' ') + 1));
        var chassisRoot = ((GameObject)AssetDatabase.LoadMainAssetAtPath(chassisPath)).transform;
        var newObjects = new Dictionary<string, Object>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(chassisPath))
        {
            var key = o ? Logical(o, chassisRoot) : null;
            if (key == null) continue;
            if (newObjects.ContainsKey(key)) throw new Exception("duplicate logical key " + key);
            newObjects[key] = o;
        }
        var isScene = filePath.EndsWith(".unity");
        var isPrefab = filePath.EndsWith(".prefab");
        Scene scene = default;
        GameObject contents = null;
        var objects = new List<Object>();
        var instances = new List<GameObject>();
        try
        {
            var roots = new List<GameObject>();
            if (isScene) { scene = EditorSceneManager.OpenScene(filePath, OpenSceneMode.Additive); roots.AddRange(scene.GetRootGameObjects()); }
            else if (isPrefab) { contents = PrefabUtility.LoadPrefabContents(filePath); roots.Add(contents); }
            else objects.AddRange(AssetDatabase.LoadAllAssetsAtPath(filePath).Where(o => o));
            foreach (var r in roots)
            {
                objects.AddRange(ObjectsUnder(r));
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    {
                        objects.Add(PrefabUtility.GetPrefabInstanceHandle(t.gameObject));
                        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == chassisPath) instances.Add(t.gameObject);
                    }
            }
            var remapped = 0; var dead = 0;
            foreach (var o in objects.Distinct())
            {
                var so = new SerializedObject(o);
                var it = so.GetIterator();
                var changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.propertyPath == "m_CorrespondingSourceObject" || it.propertyPath == "m_PrefabInstance" || it.propertyPath == "m_PrefabAsset") continue;
                    var id = it.objectReferenceInstanceIDValue;
                    if (id == 0 || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out var guid, out long fileId) || guid != Ship1Guid || fileId == 100100000) continue;
                    var holder = o is Component hc ? Logical(hc, hc.transform.root) : o is GameObject hg ? Logical(hg, hg.transform.root) : $"<{o.GetType().Name}:{o.name}>";
                    if (it.objectReferenceValue) { log.Append($"CURRENT {holder} .{it.propertyPath} -> {fileId} resolves to {Logical(it.objectReferenceValue, chassisRoot)}\n"); continue; }
                    if (!oldIds.TryGetValue(fileId, out var logical))
                    {
                        dead++;
                        log.Append($"DEAD-KEPT {holder} .{it.propertyPath} -> {fileId}\n");
                        continue;
                    }
                    if (!newObjects.TryGetValue(logical, out var target)) throw new Exception($"no new object for {logical}");
                    it.objectReferenceValue = target;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out var ng, out long nid);
                    log.Append($"REMAP {holder} .{it.propertyPath}: {fileId} -> {nid} {logical}\n");
                    remapped++;
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var inst in instances)
            {
                PrefabUtility.MergePrefabInstance(inst);
                log.AppendLine($"MERGED instance {inst.name}");
            }
            if (siblingIndex >= 0)
            {
                if (instances.Count != 1) throw new Exception("expected one chassis instance, found " + instances.Count);
                var was = instances[0].transform.GetSiblingIndex();
                instances[0].transform.SetSiblingIndex(siblingIndex);
                log.AppendLine($"SIBLING {instances[0].name}: {was} -> {instances[0].transform.GetSiblingIndex()}");
            }
            var inFile = 0;
            foreach (var entry in (inFileRefsJson ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // holderPath#Type|propertyPath|instanceLogical
                var parts = entry.Split('|');
                var holderKey = parts[0];
                var holder = objects.FirstOrDefault(o => (o is Component hc ? Logical(hc, hc.transform.root) : null) == holderKey);
                if (!holder) throw new Exception("holder not found " + holderKey);
                if (instances.Count != 1) throw new Exception("expected one chassis instance, found " + instances.Count);
                var target = ObjectsUnder(instances[0]).FirstOrDefault(o => Logical(o, instances[0].transform) == parts[2]);
                if (!target) throw new Exception("instance object not found " + parts[2]);
                var so = new SerializedObject(holder);
                var prop = so.FindProperty(parts[1]);
                var was = prop.objectReferenceValue;
                prop.objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append($"INFILE {holderKey} .{parts[1]}: {(was ? Logical(was, instances[0].transform) ?? was.name : "null")} -> instance{parts[2]}\n");
                inFile++;
            }
            if (isPrefab)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, filePath, out var ok);
                if (!ok) throw new Exception("save failed " + filePath);
            }
            else if (isScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception("scene save failed");
            }
            else
            {
                foreach (var o in objects) EditorUtility.SetDirty(o);
                AssetDatabase.SaveAssetIfDirty(objects[0]);
            }
            log.Insert(0, $"{filePath}: remapped={remapped} in-file={inFile} dead-kept={dead}\n");
        }
        finally
        {
            if (contents) PrefabUtility.UnloadPrefabContents(contents);
            if (isScene && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
        File.AppendAllText(logPath, log.ToString());
        return log.ToString();
    }

    public static string Move(string from, string to, string logPath)
    {
        var guid = AssetDatabase.AssetPathToGUID(from);
        var error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new Exception($"MoveAsset {from} -> {to}: {error}");
        var after = AssetDatabase.AssetPathToGUID(to);
        var line = $"moved {from} -> {to} guid {guid} -> {after}\n";
        if (guid != after) throw new Exception("guid changed: " + line);
        File.AppendAllText(logPath, line);
        return line;
    }

    public static string Delete(string path, string logPath)
    {
        var log = new StringBuilder();
        DeleteIfUnreferenced(path, log);
        File.AppendAllText(logPath, log.ToString());
        return log.ToString();
    }

    public static string CreateFolder(string parent, string name)
    {
        return AssetDatabase.CreateFolder(parent, name);
    }

    /// Production dependencies of the given roots that live under a study-named folder.
    public static string StudyDependencies(string rootsCsv)
    {
        var sb = new StringBuilder();
        foreach (var root in rootsCsv.Split(','))
            foreach (var d in AssetDatabase.GetDependencies(root, true).OrderBy(p => p))
                if (d.Contains("/Studies/") || d.Contains("DrawnStudy")) sb.Append($"{root} -> {d}\n");
        return sb.Length == 0 ? "none" : sb.ToString();
    }
}
