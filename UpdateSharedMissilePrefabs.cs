using Combat.Weapons.Conditions;
using UI.Weapons;
using UnityEditor;
using UnityEngine;

public static class UpdateSharedMissilePrefabs
{
    public static string Main()
    {
        var path = "Assets/Prefabs/Weapons/Missiles.prefab";
        var missile = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(missile.GetComponent<Rounds>());
            serialized.FindProperty("reloadTime").floatValue = 10f;
            serialized.FindProperty("refill").intValue = (int)Rounds.RefillMode.Sequential;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(missile, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(missile); }
        path = "Assets/Prefabs/UI/Crosshair/WeaponKeystoneHud.prefab";
        var hud = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(hud.GetComponent<WeaponResourceHud>());
            serialized.FindProperty("threshold").colorValue = new Color(.04f, .06f, .1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(hud, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        return "Missiles use one ten-second recharge timer; HUD thresholds are two degrees wide.";
    }
}
