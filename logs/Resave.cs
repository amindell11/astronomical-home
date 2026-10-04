using System;
using UnityEditor;
public static class VanguardResave
{
    public static string Run(string path)
    {
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var was = contents.name;
            contents.name = System.IO.Path.GetFileNameWithoutExtension(path);
            PrefabUtility.SaveAsPrefabAsset(contents, path, out var ok);
            if (!ok) throw new Exception("save failed");
            return $"{path}: root {was} -> {contents.name}";
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
