using System;
using System.Linq;
using System.Text;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Scratch editor tool for slice 2 of #1381 (#1588): the outpost models into the shipped station prefabs.
public static class SwapOutpostStations
{
    const string FeelPath = "Assets/Settings/Game/StationMotionFeel.asset";
    const string OldModelPath = "Assets/Prefabs/Structures/Station 3.prefab";
    const string OldModelFolder = "Assets/Visuals/Environment/Stations/Station 3";
    const string EditScenePath = "Assets/Scenes/EditScene.unity";

    static readonly (string station, string model, float[] rates)[] Kinds =
    {
        ("Assets/Prefabs/Objectives/Repair Station.prefab",
            "Assets/Visuals/Environment/Stations/Repair Station/Repair Station Model.prefab", new[] { 1f }),
        ("Assets/Prefabs/Objectives/Mod Station.prefab",
            "Assets/Visuals/Environment/Stations/Mod Station/Mod Station Model.prefab", new[] { 1f, -3f }),
        ("Assets/Prefabs/Objectives/Armory.prefab",
            "Assets/Visuals/Environment/Stations/Armory/Armory Model.prefab", new[] { 1f }),
    };

    public static void Run()
    {
        try
        {
            var log = Feel() + Swap() + RetireOldModel();
            AssetDatabase.SaveAssets();
            Debug.Log("SWAP_OK\n" + log);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("SWAP_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    public static string Feel()
    {
        var log = new StringBuilder();
        var feel = AssetDatabase.LoadAssetAtPath<StationMotionFeelProfile>(FeelPath);
        if (!feel)
        {
            feel = ScriptableObject.CreateInstance<StationMotionFeelProfile>();
            AssetDatabase.CreateAsset(feel, FeelPath);
            log.AppendLine($"created {FeelPath}");
        }

        foreach (var kind in Kinds)
        {
            var root = PrefabUtility.LoadPrefabContents(kind.model);
            try
            {
                var motion = root.GetComponent<StationMotion>();
                if (!motion) throw new InvalidOperationException($"{kind.model} has no StationMotion on its root");
                var so = new SerializedObject(motion);
                so.FindProperty("feel").objectReferenceValue = feel;
                var spins = so.FindProperty("spins");
                if (spins.arraySize != kind.rates.Length)
                    throw new InvalidOperationException($"{kind.model} has {spins.arraySize} spins, expected {kind.rates.Length}");
                for (var i = 0; i < kind.rates.Length; i++)
                    spins.GetArrayElementAtIndex(i).FindPropertyRelative("rate").floatValue = kind.rates[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, kind.model);
                log.AppendLine($"{kind.model}: feel set, rates {string.Join(",", kind.rates)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    public static string Swap()
    {
        var log = new StringBuilder();
        var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(OldModelPath);
        foreach (var kind in Kinds)
        {
            var root = PrefabUtility.LoadPrefabContents(kind.station);
            try
            {
                var body = root.GetComponentInChildren<FindStationBody>(true);
                var old = body.transform.Cast<Transform>()
                    .Single(child => PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == oldModel);
                var index = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(kind.model);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, body.transform);
                model.transform.SetSiblingIndex(index);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                var glow = body.Lit.GetComponentsInChildren<Light>(true).Single();
                Object.DestroyImmediate(glow.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, kind.station);
                log.AppendLine($"{kind.station}: '{model.name}' at sibling {index}, glow removed");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    public static string RetireOldModel()
    {
        var log = new StringBuilder();
        var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(OldModelPath);
        var scene = EditorSceneManager.OpenScene(EditScenePath, OpenSceneMode.Additive);
        try
        {
            var strays = scene.GetRootGameObjects()
                .Where(go => PrefabUtility.GetCorrespondingObjectFromSource(go) == oldModel).ToArray();
            foreach (var stray in strays)
            {
                log.AppendLine($"EditScene: removed '{stray.name}'");
                Object.DestroyImmediate(stray);
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        log.AppendLine($"delete {OldModelPath}: {AssetDatabase.DeleteAsset(OldModelPath)}");
        log.AppendLine($"delete {OldModelFolder}: {AssetDatabase.DeleteAsset(OldModelFolder)}");
        return log.ToString();
    }
}
