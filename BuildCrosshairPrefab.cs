using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UI.Weapons;

public static class BuildCrosshairPrefab
{
    public static string Build()
    {
        const string art = "Assets/Visuals/Ui/Crosshair/Generated/";
        foreach (var name in new[] { "circle", "pointer", "resource-ring", "reload-ring" })
        {
            var path = art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI/Crosshair"))
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Crosshair");
        const string prefabPath = "Assets/Prefabs/UI/Crosshair/AimCrosshair.prefab";
        var root = new GameObject("AimCrosshair", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(40f, 40f);
            var crosshair = root.AddComponent<AimCrosshair>();
            AddImage(root.transform, "Circle", art + "circle.png", false);
            var pointer = AddImage(root.transform, "Pointer", art + "pointer.png", false);
            var resource = AddImage(root.transform, "ResourceTemplate", art + "resource-ring.png", true);
            var reload = AddImage(root.transform, "ReloadTemplate", art + "reload-ring.png", true);
            var data = new SerializedObject(crosshair);
            data.FindProperty("pointer").objectReferenceValue = pointer.rectTransform;
            data.FindProperty("resourceTemplate").objectReferenceValue = resource;
            data.FindProperty("reloadTemplate").objectReferenceValue = reload;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { Object.DestroyImmediate(root); }

        const string overlayPath = "Assets/Prefabs/UI/UIOverlay.prefab";
        var overlay = PrefabUtility.LoadPrefabContents(overlayPath);
        try
        {
            var existing = overlay.transform.Find("AimCrosshair");
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), overlay.transform);
            instance.transform.localPosition = Vector3.zero;
            var data = new SerializedObject(overlay.GetComponent<UI.Overlay>());
            data.FindProperty("crosshair").objectReferenceValue = instance.GetComponent<AimCrosshair>();
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(overlay, overlayPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(overlay); }
        AssetDatabase.SaveAssets();
        return prefabPath;
    }

    private static Image AddImage(Transform parent, string name, string spritePath, bool template)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.raycastTarget = false;
        if (template)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Right;
            go.SetActive(false);
        }
        return image;
    }
}
