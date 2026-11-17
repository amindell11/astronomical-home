var report = new System.Text.StringBuilder();
foreach (var name in new[] { "Vanguard", "Bulwark" })
{
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Vfx/KeystoneTrails/Materials/KeystoneStreak_" + name + ".mat");
    material.SetFloat("_Fill", 0.65f);
    material.SetFloat("_Gradient", 0.25f);
    material.SetFloat("_Edge", 0.8f);
    material.SetFloat("_InkOpacity", 0.35f);
    material.SetFloat("_InkWidth", 0.18f);
    material.SetFloat("_Hatch", 0.06f);
    UnityEditor.EditorUtility.SetDirty(material);
    var path = "Assets/Prefabs/Keystones/" + name + "_Dash.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var flow = root.GetComponentInChildren<Ships.Keystones.KeystoneSlipstream>(true);
        var settings = new UnityEditor.SerializedObject(flow);
        var values = name == "Vanguard"
            ? new float[,] { { -.56f,-.28f,2.0f,.24f,.95f,4,1 }, { .56f,-.28f,2.0f,.24f,1f,2,1 }, { -.28f,-.96f,1.5f,.10f,.90f,9,.85f }, { .28f,-.96f,1.5f,.10f,.85f,6,.85f } }
            : new float[,] { { -.72f,-.90f,2.0f,.34f,.95f,4,1 }, { .72f,-.90f,2.0f,.34f,1f,2,1 }, { -.30f,-1.46f,1.5f,.14f,.90f,9,.85f }, { .30f,-1.46f,1.5f,.14f,.85f,6,.85f }, { -.90f,.99f,4.2f,1.05f,.65f,16,1 }, { .90f,.99f,4.2f,1.05f,.65f,21,1 } };
        var ribbons = settings.FindProperty("ribbons");
        ribbons.arraySize = values.GetLength(0);
        for (var i = 0; i < ribbons.arraySize; i++)
        {
            var ribbon = ribbons.GetArrayElementAtIndex(i);
            ribbon.FindPropertyRelative("anchor").vector2Value = new UnityEngine.Vector2(values[i,0],values[i,1]);
            ribbon.FindPropertyRelative("width").floatValue = values[i,2];
            ribbon.FindPropertyRelative("spread").floatValue = values[i,3];
            ribbon.FindPropertyRelative("lengthScale").floatValue = values[i,4];
            ribbon.FindPropertyRelative("atlasCell").intValue = (int)values[i,5];
            ribbon.FindPropertyRelative("opacity").floatValue = values[i,6];
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
        report.AppendLine(name + ": revised sheet widths, fill and paired bow cells.");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
UnityEditor.AssetDatabase.SaveAssets();
return report.ToString();
