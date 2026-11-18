var report = new System.Text.StringBuilder();
foreach (var name in new[] { "Vanguard", "Bulwark" })
{
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Vfx/KeystoneTrails/Materials/KeystoneStreak_" + name + ".mat");
    material.SetFloat("_Fill", 0.6f);
    material.SetFloat("_Gradient", 0.15f);
    material.SetFloat("_Hatch", 0.06f);
    UnityEditor.EditorUtility.SetDirty(material);
    var path = "Assets/Prefabs/Keystones/" + name + "_Dash.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var flow = root.GetComponentInChildren<Ships.Keystones.KeystoneSlipstream>(true);
        var settings = new UnityEditor.SerializedObject(flow);
        settings.FindProperty("tint").floatValue = name == "Vanguard" ? .55f : .35f;
        var values = new float[,] {
            { -.56f,-.28f,2.4f,.50f,1f,4,.55f },
            { .56f,-.28f,2.4f,.50f,1f,9,.55f },
            { -.44f,-.50f,2f,.30f,.82f,10,.75f },
            { .44f,-.50f,2f,.30f,1f,2,.75f },
            { -.30f,-.82f,1.8f,.12f,.72f,3,.95f },
            { .30f,-.82f,1.8f,.12f,.80f,8,.95f },
            { -.24f,-.92f,1.6f,.08f,.90f,6,.85f },
            { .24f,-.92f,1.6f,.08f,.68f,9,.85f },
            { -.40f,-.60f,1.5f,.18f,.75f,0,.75f },
            { .40f,-.60f,1.5f,.18f,.85f,1,.75f }
        };
        var ribbons = settings.FindProperty("ribbons");
        ribbons.arraySize = name == "Vanguard" ? 10 : 14;
        for (var i = 0; i < 10; i++)
        {
            var ribbon = ribbons.GetArrayElementAtIndex(i);
            var scaleX = name == "Vanguard" ? 1f : 1.25f;
            var scaleY = name == "Vanguard" ? 1f : 1.55f;
            ribbon.FindPropertyRelative("anchor").vector2Value = new UnityEngine.Vector2(values[i,0]*scaleX, values[i,1]*scaleY);
            ribbon.FindPropertyRelative("width").floatValue = values[i,2]*scaleX*1.1f;
            ribbon.FindPropertyRelative("spread").floatValue = values[i,3]*scaleX;
            ribbon.FindPropertyRelative("lengthScale").floatValue = values[i,4];
            ribbon.FindPropertyRelative("atlasCell").intValue = (int)values[i,5];
            ribbon.FindPropertyRelative("opacity").floatValue = values[i,6];
        }
        if (name == "Bulwark")
        {
            var bow = new float[,] {
                { -.90f,.99f,5f,1.05f,1f,16,.85f },
                { .90f,.99f,5f,1.05f,1f,21,.85f },
                { -.86f,.91f,4.2f,.65f,.65f,16,1f },
                { .86f,.91f,4.2f,.65f,.65f,21,1f }
            };
            for (var i = 0; i < 4; i++)
            {
                var ribbon = ribbons.GetArrayElementAtIndex(i+10);
                ribbon.FindPropertyRelative("anchor").vector2Value = new UnityEngine.Vector2(bow[i,0],bow[i,1]);
                ribbon.FindPropertyRelative("width").floatValue = bow[i,2];
                ribbon.FindPropertyRelative("spread").floatValue = bow[i,3];
                ribbon.FindPropertyRelative("lengthScale").floatValue = bow[i,4];
                ribbon.FindPropertyRelative("atlasCell").intValue = (int)bow[i,5];
                ribbon.FindPropertyRelative("opacity").floatValue = bow[i,6];
            }
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path);
        report.AppendLine(name + ": " + ribbons.arraySize + " overlapping sheets.");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
UnityEditor.AssetDatabase.SaveAssets();
return report.ToString();
