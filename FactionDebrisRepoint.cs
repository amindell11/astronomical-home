using System;
using System.Collections.Generic;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;

// One-off batch edit for #1339 (deleted before commit): points the Crimson and Nightshade debris
// prefabs at the hull paint, moves their soot and draw order onto the debris, and deletes the
// debris-only paint copies.
public static class FactionDebrisRepoint
{
    private const string Crimson = "Assets/Visuals/Ships/Crimson/";
    private const string Nightshade = "Assets/Visuals/Ships/Nightshade/";
    private static readonly string[] NightshadePaint = { "Edge", "Glass", "Midnight", "Plum", "Recess", "Violet" };

    public static void Run()
    {
        try
        {
            var retired = new List<string>();
            Repoint(Crimson + "Breakup/CrimsonBreakup.prefab", .9f, 3100, new Dictionary<string, string>
            {
                { Crimson + "Breakup/Debris paint.mat", Crimson + "Hull paint.mat" },
            }, retired);
            var nightshade = new Dictionary<string, string>();
            foreach (var name in NightshadePaint)
                nightshade.Add($"{Nightshade}Breakup/Nightshade {name}.mat", $"{Nightshade}Materials/nightshade/Nightshade {name}.mat");
            Repoint(Nightshade + "Breakup/NightshadeBreakup.prefab", .1f, 3100, nightshade, retired);
            AssetDatabase.SaveAssets();
            foreach (var path in retired)
                if (!AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("could not delete " + path);
            AssetDatabase.SaveAssets();
            Debug.Log("FACTION_DEBRIS_REPOINT=ok");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.Log("FACTION_DEBRIS_REPOINT=failed");
            EditorApplication.Exit(1);
        }
    }

    private static void Repoint(string prefabPath, float soot, int queue, Dictionary<string, string> swaps, List<string> retired)
    {
        var map = new Dictionary<Material, Material>();
        foreach (var swap in swaps)
        {
            map.Add(Load(swap.Key), Load(swap.Value));
            retired.Add(swap.Key);
        }
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var swapped = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !map.TryGetValue(materials[i], out var hull)) continue;
                    materials[i] = hull;
                    changed = true;
                    swapped++;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            if (swapped == 0) throw new InvalidOperationException("nothing to repoint in " + prefabPath);
            var debris = root.GetComponent<ShipBreakupDebris>();
            if (!debris) throw new InvalidOperationException("no ShipBreakupDebris on the root of " + prefabPath);
            using (var serialized = new SerializedObject(debris))
            {
                var sootProperty = serialized.FindProperty("soot");
                var queueProperty = serialized.FindProperty("paintQueue");
                if (sootProperty == null || queueProperty == null)
                    throw new InvalidOperationException("ShipBreakupDebris has no soot/paintQueue fields");
                sootProperty.floatValue = soot;
                queueProperty.intValue = queue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"FACTION_DEBRIS_REPOINT {prefabPath}: {swapped} slots, soot {soot}, queue {queue}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Material Load(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) throw new InvalidOperationException("missing " + path);
        return material;
    }
}
