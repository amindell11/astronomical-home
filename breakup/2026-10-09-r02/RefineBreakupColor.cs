using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
public static class RefineBreakupColor
{
    public static object Main()
    {
        var soot = (float)JObject.Parse(File.ReadAllText("../../art/ships/nightshade/breakup.json"))["soot"];
        var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Visuals/Ships/Nightshade/Breakup" }).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m => m.HasProperty("_SootStrength")).ToArray();
        foreach (var material in materials) { material.SetFloat("_SootStrength", soot); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        return new { soot, materials = materials.Select(m => m.name).ToArray() };
    }
}