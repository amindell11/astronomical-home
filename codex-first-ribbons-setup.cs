var report = new System.Text.StringBuilder();
foreach (var name in new[] { "Vanguard", "Bulwark" })
{
    var path = "Assets/Prefabs/Keystones/" + name + "_Dash.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var old = root.GetComponentInChildren<Ships.Keystones.KeystoneTrail>(true);
        var oldSettings = new UnityEditor.SerializedObject(old);
        oldSettings.FindProperty("sheds").arraySize = 0;
        oldSettings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var system in root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true)) system.gameObject.SetActive(false);
        var flow = old.GetComponent<Ships.Keystones.KeystoneSlipstream>();
        if (!flow) flow = old.gameObject.AddComponent<Ships.Keystones.KeystoneSlipstream>();
        var settings = new UnityEditor.SerializedObject(flow);
        settings.FindProperty("keystone").objectReferenceValue = root.GetComponent<Ships.Keystones.Keystone>();
        settings.FindProperty("material").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Vfx/KeystoneTrails/Materials/KeystoneStreak_" + name + ".mat");
        settings.FindProperty("localShipLength").floatValue = name == "Vanguard" ? 2f : 3.17131f;
        settings.FindProperty("lifetime").floatValue = 0.32f;
        settings.FindProperty("maxLength").floatValue = 3.2f;
        settings.FindProperty("tint").floatValue = 0.35f;
        var values = name == "Vanguard"
            ? new float[,] { { -.56f,-.28f,1.45f,.24f,.95f,4,1 }, { .56f,-.28f,1.45f,.24f,1f,2,1 }, { -.28f,-.96f,1.15f,.10f,.90f,9,1 }, { .28f,-.96f,1.15f,.10f,.85f,6,1 } }
            : new float[,] { { -.72f,-.90f,1.90f,.34f,.95f,4,1 }, { .72f,-.90f,1.90f,.34f,1f,2,1 }, { -.30f,-1.46f,1.50f,.14f,.90f,9,1 }, { .30f,-1.46f,1.50f,.14f,.85f,6,1 }, { -.90f,.99f,2.0f,1.05f,.40f,16,.8f }, { .90f,.99f,2.0f,1.05f,.40f,17,.8f } };
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
        report.AppendLine(name + ": " + ribbons.arraySize + " sheets wired; particle objects inactive; existing overlays and flash retained.");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
return report.ToString();
