#if UNITY_EDITOR
using System;
using System.Linq;
using UI.Hangar.Hardware;
using UI.Hangar.Hologram;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StableHangarLayout
{
    public static void Apply()
    {
        const string modelPath = HangarConsoleBuilder.VisualsPath + "/Console.fbx";
        const string prefabPath = HangarConsoleBuilder.PrefabPath;
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        GameObject imported = null;
        try
        {
            var consoleRoot = contents.transform.Find("Console");
            if (!consoleRoot) throw new InvalidOperationException("The existing Console model root is missing.");
            var existing = consoleRoot.GetComponentsInChildren<MeshFilter>(true).ToDictionary(part => part.name);
            imported = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), contents.scene);
            imported.transform.SetParent(contents.transform, false);
            imported.transform.localPosition = consoleRoot.localPosition;
            imported.transform.localRotation = consoleRoot.localRotation;
            imported.transform.localScale = consoleRoot.localScale;
            var authored = imported.GetComponentsInChildren<MeshFilter>(true).ToDictionary(part => part.name);
            if (!existing.Keys.OrderBy(name => name).SequenceEqual(authored.Keys.OrderBy(name => name)))
                throw new InvalidOperationException("The exported model must preserve the existing named mesh parts.");

            foreach (var part in existing.Values.Where(part => part.transform.parent == consoleRoot))
                Copy(authored[part.name], part, directChild: true);
            foreach (var part in existing.Values.Where(part => part.transform.parent != consoleRoot))
            {
                var parent = part.transform.parent;
                if (!existing.ContainsKey(parent.name) || parent.parent != consoleRoot)
                    throw new InvalidOperationException("Unexpected nested hardware part: " + part.name);
                Copy(authored[part.name], part, directChild: false);
            }

            foreach (var part in existing.Values)
            {
                var expected = authored[part.name].transform.localToWorldMatrix;
                var actual = part.transform.localToWorldMatrix;
                for (var index = 0; index < 16; index++)
                    if (Mathf.Abs(expected[index] - actual[index]) > .00002f)
                        throw new InvalidOperationException("Authored world transform mismatch: " + part.name);
                if (part.sharedMesh != authored[part.name].sharedMesh)
                    throw new InvalidOperationException("Authored mesh mismatch: " + part.name);
            }

            var lens = existing["ProjectorLens"].transform;
            var launch = existing["LaunchButton"].transform;
            var projector = contents.GetComponentInChildren<HangarHologramProjector>(true);
            if (!projector) throw new InvalidOperationException("The existing hologram projector is missing.");
            projector.transform.position = lens.position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(projector.transform);
            contents.transform.Find("ProjectorSpill").position = lens.position + new Vector3(.05f, 2.774f, -.21f);
            contents.transform.Find("LaunchSpill").position = launch.position + new Vector3(.026f, 2.971f, -.081f);

            Object.DestroyImmediate(imported);
            imported = null;
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out var saved);
            if (!saved) throw new InvalidOperationException("The existing hangar prefab did not save.");
            Debug.Log("StableHangarLayout saved " + existing.Count + " authored parts without replacing their objects.");
        }
        finally
        {
            if (imported) Object.DestroyImmediate(imported);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Copy(MeshFilter source, MeshFilter target, bool directChild)
    {
        var destination = target.transform;
        var sourceTransform = source.transform;
        Vector3 position;
        Quaternion rotation;
        Vector3 scale;
        if (directChild)
        {
            position = sourceTransform.localPosition;
            rotation = sourceTransform.localRotation;
            scale = sourceTransform.localScale;
        }
        else
        {
            var local = destination.parent.worldToLocalMatrix * sourceTransform.localToWorldMatrix;
            position = local.GetColumn(3);
            rotation = local.rotation;
            scale = local.lossyScale;
        }

        if (Vector3.Distance(destination.localPosition, position) > .000001f) destination.localPosition = position;
        if (Quaternion.Angle(destination.localRotation, rotation) > .001f) destination.localRotation = rotation;
        if (Vector3.Distance(destination.localScale, scale) > .000001f) destination.localScale = scale;
        if (target.sharedMesh != source.sharedMesh) target.sharedMesh = source.sharedMesh;
    }
}
#endif
