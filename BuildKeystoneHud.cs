using System;
using System.IO;
using UI;
using UI.PlayerState;
using UI.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BuildKeystoneHud
{
    private const string Art = "Assets/Visuals/Ui/Crosshair/Generated/";
    private const string HudPath = "Assets/Prefabs/UI/Crosshair/WeaponKeystoneHud.prefab";
    private static readonly Color Ink = new(.953f, .949f, .875f);

    public static string Main()
    {
        foreach (var name in new[] { "resource-ring", "keystone-ring", "keystone-disc", "keystone-placeholder" })
        {
            var path = Art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        var aimPath = "Assets/Prefabs/UI/Crosshair/AimCrosshair.prefab";
        var aim = PrefabUtility.LoadPrefabContents(aimPath);
        try
        {
            UnityEngine.Object.DestroyImmediate(aim.transform.Find("ResourceTemplate").gameObject);
            UnityEngine.Object.DestroyImmediate(aim.transform.Find("ReloadTemplate").gameObject);
            PrefabUtility.SaveAsPrefabAsset(aim, aimPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(aim); }

        var hud = new GameObject("WeaponKeystoneHud", typeof(RectTransform));
        hud.SetActive(false);
        var hudRect = (RectTransform)hud.transform;
        hudRect.anchorMin = hudRect.anchorMax = new Vector2(.5f, 0f);
        hudRect.anchoredPosition = new Vector2(0f, 58.5f);
        hudRect.sizeDelta = new Vector2(92f, 92f);
        var resources = hud.AddComponent<WeaponResourceHud>();
        AddImage(hud.transform, "KeystoneDisc", "keystone-disc", new Color(.043f, .078f, .149f, .65f));
        AddImage(hud.transform, "KeystoneTrack", "keystone-ring", new Color(.478f, .518f, .584f, .6f));
        var fill = AddImage(hud.transform, "KeystoneFill", "keystone-ring", Ink);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        var gauge = fill.gameObject.AddComponent<BoostGaugeUI>();
        var gaugeData = new SerializedObject(gauge);
        gaugeData.FindProperty("coolingColor").colorValue = Ink;
        gaugeData.FindProperty("readyColor").colorValue = Ink;
        gaugeData.ApplyModifiedPropertiesWithoutUndo();
        AddImage(hud.transform, "KeystonePlaceholder", "keystone-placeholder", new Color(.639f, .651f, .667f));

        var resourceTemplate = ArcTemplate(hud.transform, "ResourceTemplate", "resource-ring");
        var reloadTemplate = ArcTemplate(hud.transform, "ReloadTemplate", "reload-ring");
        var resourceData = new SerializedObject(resources);
        resourceData.FindProperty("resourceTemplate").objectReferenceValue = resourceTemplate;
        resourceData.FindProperty("reloadTemplate").objectReferenceValue = reloadTemplate;
        resourceData.ApplyModifiedPropertiesWithoutUndo();
        try { PrefabUtility.SaveAsPrefabAsset(hud, HudPath); }
        finally { UnityEngine.Object.DestroyImmediate(hud); }

        var overlayPath = "Assets/Prefabs/UI/UIOverlay.prefab";
        var overlay = PrefabUtility.LoadPrefabContents(overlayPath);
        try
        {
            var oldGauge = overlay.GetComponentInChildren<BoostGaugeUI>(true);
            UnityEngine.Object.DestroyImmediate(oldGauge.gameObject);
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath), overlay.transform);
            nested.name = "WeaponKeystoneHud";
            var overlayData = new SerializedObject(overlay.GetComponent<Overlay>());
            overlayData.FindProperty("weaponResources").objectReferenceValue = nested.GetComponent<WeaponResourceHud>();
            overlayData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(overlay, overlayPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(overlay); }
        AssetDatabase.SaveAssets();
        return HudPath;
    }

    private static Image AddImage(Transform parent, string name, string asset, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.sizeDelta = Vector2.zero;
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset + ".png");
        if (!image.sprite) throw new InvalidOperationException("Missing sprite " + asset);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image ArcTemplate(Transform parent, string name, string asset)
    {
        var image = AddImage(parent, name, asset, Color.white);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Right;
        image.gameObject.SetActive(false);
        return image;
    }
}
