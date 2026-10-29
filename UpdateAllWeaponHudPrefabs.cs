using UnityEditor;
using UnityEngine;

public static class UpdateAllWeaponHudPrefabs
{
    public static string Main()
    {
        var path = "Assets/Prefabs/UI/Crosshair/WeaponKeystoneHud.prefab";
        var hud = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var reload = hud.transform.Find("ReloadTemplate");
            if (reload) Object.DestroyImmediate(reload.gameObject);
            PrefabUtility.SaveAsPrefabAsset(hud, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        path = "Assets/Prefabs/UI/UIOverlay.prefab";
        var overlay = PrefabUtility.LoadPrefabContents(path);
        try { PrefabUtility.SaveAsPrefabAsset(overlay, path); }
        finally { PrefabUtility.UnloadPrefabContents(overlay); }
        AssetDatabase.SaveAssets();
        return "Removed the unused reload template and charge-widget reference.";
    }
}