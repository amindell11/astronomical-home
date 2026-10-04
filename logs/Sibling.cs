using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class VanguardSibling
{
    public static string Restore(string filePath, string chassisPath, int index)
    {
        var contents = PrefabUtility.LoadPrefabContents(filePath);
        try
        {
            var inst = contents.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                .Single(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == chassisPath);
            var was = inst.transform.GetSiblingIndex();
            inst.transform.SetSiblingIndex(index);
            PrefabUtility.SaveAsPrefabAsset(contents, filePath, out var ok);
            if (!ok) throw new Exception("save failed");
            return $"{filePath}: {inst.name} sibling {was} -> {inst.transform.GetSiblingIndex()}";
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
