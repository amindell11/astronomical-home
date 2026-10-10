using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ValisMigrate
{
    const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
    const string ValisGuid = "2198c10e93bde5f42b7b9a44dbff3e6a";
    const string BasePath = "Assets/Prefabs/Ships/ShipBase.prefab";
    const string ColliderMesh = "Assets/Visuals/Ships/Valis/Meshes/Valis collider.asset";
    const string DebrisPrefab = "Assets/Visuals/Ships/Valis/Breakup/ValisBreakup.prefab";
    static readonly CultureInfo C = CultureInfo.InvariantCulture;
    static string F(float f) => f.ToString("R", C);
    static string V(Vector3 v) => $"({F(v.x)},{F(v.y)},{F(v.z)})";
    static string Q(Quaternion q) => $"({F(q.x)},{F(q.y)},{F(q.z)},{F(q.w)})";

    static readonly string[] YamlExt = { ".prefab", ".unity", ".asset", ".mat", ".controller", ".overrideController", ".anim", ".playable", ".mask", ".preset", ".lighting", ".physicMaterial", ".signal", ".spriteatlas", ".shadergraph", ".shadersubgraph", ".vfx", ".terrainlayer", ".brush", ".mixer", ".guiskin", ".fontsettings", ".cubemap", ".flare", ".renderTexture", ".giparams", ".asmdef", ".asmref", ".inputactions", ".uss", ".uxml", ".tss" };

    /// Files under Assets/ and ProjectSettings/ (other than the asset itself and its .meta) whose text mentions the guid.
    public static string Referrers(string assetPath)
    {
        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) throw new Exception("no guid for " + assetPath);
        var hits = new List<string>();
        var scanned = 0;
        foreach (var root in new[] { "Assets", "ProjectSettings" })
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var n = f.Replace('\\', '/');
                if (n == assetPath || n == assetPath + ".meta" || n.EndsWith(".meta")) continue;
                if (!YamlExt.Any(e => n.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                scanned++;
                var lines = File.ReadAllLines(n);
                for (var i = 0; i < lines.Length; i++)
                    if (lines[i].Contains(guid)) hits.Add($"{n}:{i + 1}: {lines[i].Trim()}");
            }
        return $"guid {guid}: scanned {scanned} files; {hits.Count} hits\n" + string.Join("\n", hits) + "\n";
    }

    static Transform Find(Transform root, string path)
    {
        var t = root.Find(path);
        if (!t) throw new Exception("missing " + path + " under " + root.name);
        return t;
    }

    static string PathOf(Transform t, Transform root)
    {
        var names = new List<string>();
        for (var c = t; c && c != root; c = c.parent) names.Add(c.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static void Require(bool ok, string what)
    {
        if (!ok) throw new Exception("invariant: " + what);
    }

    /// Overwrites Valis.prefab (same GUID) with a depth-1 variant of ShipBase carrying Valis's hull and slot values.
    public static string BuildVariant(string logPath)
    {
        var log = new StringBuilder();
        var guidBefore = AssetDatabase.AssetPathToGUID(ValisPath);
        Require(guidBefore == ValisGuid, "Valis guid");
        var scene = SceneManager.GetActiveScene();
        var legacyAsset = (GameObject)AssetDatabase.LoadMainAssetAtPath(ValisPath);
        Require(PrefabUtility.GetPrefabAssetType(legacyAsset) == PrefabAssetType.Regular, "Valis is a regular prefab before the build");
        var legacy = (GameObject)PrefabUtility.InstantiatePrefab(legacyAsset, scene);
        var shipBase = (GameObject)AssetDatabase.LoadMainAssetAtPath(BasePath);
        var variant = (GameObject)PrefabUtility.InstantiatePrefab(shipBase, scene);
        try
        {
            legacy.transform.SetPositionAndRotation(Vector3.zero, legacyAsset.transform.rotation);
            variant.transform.SetPositionAndRotation(Vector3.zero, legacyAsset.transform.rotation);
            variant.name = legacyAsset.name;
            log.Append($"root rotation {Q(legacyAsset.transform.rotation)} (default override), name {variant.name}\n");

            variant.transform.localScale = legacy.transform.localScale;
            log.Append($"root scale {V(variant.transform.localScale)}\n");

            foreach (var hp in new[] { "Hardpoints/Primary", "Hardpoints/Secondary" })
            {
                Find(variant.transform, hp).localPosition = Find(legacy.transform, hp).localPosition;
                log.Append($"{hp} local={V(Find(variant.transform, hp).localPosition)}\n");
            }

            var colliderMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ColliderMesh);
            var meshCollider = Find(variant.transform, "Mesh").GetComponent<MeshCollider>();
            var legacyCollider = Find(legacy.transform, "Mesh").GetComponent<MeshCollider>();
            Require(legacyCollider.sharedMesh == colliderMesh, "Valis collider mesh is " + ColliderMesh);
            meshCollider.sharedMesh = legacyCollider.sharedMesh;

            var rig = Find(variant.transform, "ShipBaseRig");
            var legacyRig = Find(legacy.transform, "Valis VisualRig");
            var minimap = Find(rig, "MinimapMarker").GetComponent<MeshFilter>();
            Require(Find(legacyRig, "MinimapMarker").GetComponent<MeshFilter>().sharedMesh == colliderMesh, "Valis minimap mesh is " + ColliderMesh);
            minimap.sharedMesh = colliderMesh;

            var exhaust = Find(rig, "Thruster/EngineExhaust");
            var legacyMain = Find(legacyRig, "Thruster/ThrustMain");
            var legacySmall = Find(legacyRig, "Thruster/ThrustSmall");
            Require(legacyMain.localPosition == legacySmall.localPosition, "both legacy flames share one origin");
            exhaust.position = legacyMain.position;
            log.Append($"EngineExhaust local={V(exhaust.localPosition)} world={V(exhaust.position)} (legacy ThrustMain world={V(legacyMain.position)}; legacy Thruster local={V(Find(legacyRig, "Thruster").localPosition)})\n");
            Require(!Find(rig, "Thruster/EngineExhaustTwin").gameObject.activeSelf, "twin exhaust inactive in the base");

            var tuned = new List<Object>();
            foreach (var flame in new[] { "ThrustMain", "ThrustSmall" })
            {
                var from = Find(legacyRig, "Thruster/" + flame);
                var to = Find(exhaust, flame);
                if (to.localScale != from.localScale)
                {
                    log.Append($"flame {flame} scale {V(to.localScale)} -> {V(from.localScale)}\n");
                    to.localScale = from.localScale;
                    tuned.Add(to);
                }
                var fromRenderer = from.GetComponent<ParticleSystemRenderer>();
                var toRenderer = to.GetComponent<ParticleSystemRenderer>();
                if (!fromRenderer.sharedMaterials.SequenceEqual(toRenderer.sharedMaterials))
                {
                    log.Append($"flame {flame} materials [{string.Join(",", toRenderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))}] -> [{string.Join(",", fromRenderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))}]\n");
                    toRenderer.sharedMaterials = fromRenderer.sharedMaterials;
                    tuned.Add(toRenderer);
                }
                var fromColor = new SerializedObject(from.GetComponent<ParticleSystem>());
                var toSystem = new SerializedObject(to.GetComponent<ParticleSystem>());
                if (!SerializedProperty.DataEquals(fromColor.FindProperty("InitialModule.startColor"), toSystem.FindProperty("InitialModule.startColor")))
                {
                    log.Append($"flame {flame} startColor copied\n");
                    toSystem.CopyFromSerializedProperty(fromColor.FindProperty("InitialModule.startColor"));
                    toSystem.ApplyModifiedPropertiesWithoutUndo();
                    tuned.Add(to.GetComponent<ParticleSystem>());
                }
            }

            var hullSlot = Find(rig, "Hull");
            var legacyHull = Find(legacyRig, "Valis");
            var hull = Object.Instantiate(legacyHull.gameObject, hullSlot, false);
            hull.name = legacyHull.name;

            var breakup = new SerializedObject(rig.GetComponent("ShipBreakupVisual"));
            var legacyBreakup = new SerializedObject(legacyRig.GetComponent("ShipBreakupVisual"));
            Require(legacyBreakup.FindProperty("hull").objectReferenceValue == legacyHull, "legacy breakup hull is the skinned hull object");
            var debris = legacyBreakup.FindProperty("debrisPrefab").objectReferenceValue;
            Require(AssetDatabase.GetAssetPath(debris) == DebrisPrefab, "debris is " + DebrisPrefab);
            breakup.FindProperty("debrisPrefab").objectReferenceValue = debris;
            breakup.FindProperty("hull").objectReferenceValue = hull.transform;
            var legacyPoses = legacyBreakup.FindProperty("poseSources");
            var poses = breakup.FindProperty("poseSources");
            poses.arraySize = legacyPoses.arraySize;
            for (var i = 0; i < legacyPoses.arraySize; i++)
            {
                var source = (Transform)legacyPoses.GetArrayElementAtIndex(i).objectReferenceValue;
                var path = PathOf(source, legacyHull);
                poses.GetArrayElementAtIndex(i).objectReferenceValue = Find(hull.transform, path);
                log.Append($"poseSources[{i}] = Hull/Valis/{path}\n");
            }
            breakup.ApplyModifiedPropertiesWithoutUndo();

            var legacyBody = new SerializedObject(legacy.GetComponent<Rigidbody>());
            var body = new SerializedObject(variant.GetComponent<Rigidbody>());
            foreach (var same in new[] { "m_Mass", "m_LinearDamping", "m_AngularDamping", "m_CenterOfMass", "m_InertiaRotation", "m_ImplicitCom", "m_Interpolate", "m_Constraints", "m_CollisionDetection", "m_UseGravity", "m_IsKinematic" })
                Require(SerializedProperty.DataEquals(legacyBody.FindProperty(same), body.FindProperty(same)), "Rigidbody." + same + " equals the base");
            body.FindProperty("m_ImplicitTensor").boolValue = legacyBody.FindProperty("m_ImplicitTensor").boolValue;
            body.FindProperty("m_InertiaTensor").vector3Value = legacyBody.FindProperty("m_InertiaTensor").vector3Value;
            body.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"Rigidbody implicitTensor={legacyBody.FindProperty("m_ImplicitTensor").boolValue} tensor={V(legacyBody.FindProperty("m_InertiaTensor").vector3Value)}\n");

            var legacyShip = new SerializedObject(legacy.GetComponent("Ship"));
            var ship = new SerializedObject(variant.GetComponent("Ship"));
            foreach (var field in new[] { "mass", "startingLives", "maxHealth", "maxBankAngle", "engine", "shield", "teamNumber" })
                Require(SerializedProperty.DataEquals(legacyShip.FindProperty(field), ship.FindProperty(field)), "Ship." + field + " equals the base");
            log.Append("Ship stats equal the base: no override\n");

            foreach (var c in new Object[] { variant.transform, Find(variant.transform, "Hardpoints/Primary"), Find(variant.transform, "Hardpoints/Secondary"),
                         meshCollider, minimap, exhaust, variant.GetComponent<Rigidbody>(), rig.GetComponent("ShipBreakupVisual") }.Concat(tuned))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);

            Object.DestroyImmediate(legacy);
            legacy = null;
            var saved = PrefabUtility.SaveAsPrefabAsset(variant, ValisPath, out var success);
            if (!success || !saved) throw new Exception("SaveAsPrefabAsset failed");
            var guidAfter = AssetDatabase.AssetPathToGUID(ValisPath);
            log.Append($"saved {ValisPath}: guid {guidBefore} -> {guidAfter}; type={PrefabUtility.GetPrefabAssetType(saved)}; source={AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(saved))}; root name={saved.name}\n");
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

    /// Retargets every serialized pointer to Valis's old Ship component (by persistent fileID, through the editor's
    /// SerializedObject API) to the rebuilt chassis's Ship component.
    public static string RemapShip(string filePath, long oldShipFileId, string logPath)
    {
        var log = new StringBuilder();
        var chassis = (GameObject)AssetDatabase.LoadMainAssetAtPath(ValisPath);
        var newShip = chassis.GetComponent("Ship");
        Require(newShip, "rebuilt chassis has a Ship component");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newShip, out var newGuid, out long newId);
        var objects = AssetDatabase.LoadAllAssetsAtPath(filePath).Where(o => o).ToList();
        var remapped = 0;
        foreach (var o in objects)
        {
            var so = new SerializedObject(o);
            var it = so.GetIterator();
            var changed = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var id = it.objectReferenceInstanceIDValue;
                if (id == 0 || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out var guid, out long fileId) || guid != ValisGuid) continue;
                if (fileId != oldShipFileId)
                {
                    log.Append($"OTHER {o.name} .{it.propertyPath} -> {guid}:{fileId} resolves={(bool)it.objectReferenceValue}\n");
                    continue;
                }
                if (it.objectReferenceValue) throw new Exception($"{filePath} .{it.propertyPath}: the old Ship fileID still resolves");
                it.objectReferenceValue = newShip;
                log.Append($"REMAP {o.name} .{it.propertyPath}: {guid}:{fileId} -> {newGuid}:{newId} (Valis#Ship)\n");
                remapped++;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(o);
                AssetDatabase.SaveAssetIfDirty(o);
            }
        }
        log.Insert(0, $"{filePath}: remapped={remapped}\n");
        File.AppendAllText(logPath, log.ToString());
        return log.ToString();
    }

    /// Every reference into Valis.prefab from the given file, with what it resolves to now.
    public static string Resolve(string filePath)
    {
        var sb = new StringBuilder();
        var chassisRoot = ((GameObject)AssetDatabase.LoadMainAssetAtPath(ValisPath)).transform;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(filePath).Where(o => o))
        {
            var it = new SerializedObject(o).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var id = it.objectReferenceInstanceIDValue;
                if (id == 0 || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out var guid, out long fileId) || guid != ValisGuid) continue;
                var v = it.objectReferenceValue;
                var comp = v as Component;
                sb.Append($"{filePath} {o.name} .{it.propertyPath} -> {guid}:{fileId} resolves={(bool)v}" +
                          (comp ? $" to {AssetDatabase.GetAssetPath(comp)}:/{PathOf(comp.transform, chassisRoot)}#{comp.GetType().Name} (asset root {comp.transform == chassisRoot})" : "") + "\n");
            }
        }
        return sb.ToString();
    }
}
