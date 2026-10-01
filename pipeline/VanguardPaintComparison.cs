#if UNITY_EDITOR
using System;
using System.Collections;
using Capture;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanguardCaptureEditorSetup
{
    static VanguardCaptureEditorSetup()
    {
        EditorApplication.delayCall += ClosePackageWindow;
    }

    private static void ClosePackageWindow()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.GetType().Name == "PackageManagerWindow") window.Close();
    }
}

public sealed class VanguardPaintComparison : CaptureScenario
{
    private readonly CaptureConfig config = new()
    {
        clipName = "VanguardVividBesideCrimson",
        width = 1280,
        height = 720,
        everyFixedSteps = 2,
        minHalfHeight = 4,
        padding = 2,
        configureView = ConfigureView,
    };

    public override CaptureConfig Config => config;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        var oldQuality = QualitySettings.GetQualityLevel();
        var root = new GameObject("Vanguard paint evidence");
        try
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Rendering/HighRes.asset");
            Assert.That(volume.sharedProfile, Is.Not.Null);
            var a = Spawn("Assets/Prefabs/Ships/Ship_1.prefab", new Vector2(-4, 0));
            var b = Spawn("Assets/Prefabs/Ships/Ship_2.prefab", new Vector2(4, 0));
            var renderers = a.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var name in new[] { "Vanguard surface wear", "Vanguard service panels" })
            {
                var renderer = Array.Find(renderers, item => item.name == name);
                Assert.That(renderer, Is.Null, name);
            }
            var hull = Array.Find(renderers, item => item.sharedMaterial.name == "Vanguard vivid paint");
            Assert.That(hull, Is.Not.Null);
            Assert.That(hull.sharedMaterial.GetColor("_OrangeGain"), Is.EqualTo(Color.white));
            Assert.That(hull.sharedMaterial.GetTexture("_BaseMap").width, Is.EqualTo(4096));
            yield return null;
            Film(a, b);
            for (var step = 0; step < 240; step++)
            {
                if (step == 120) config.minHalfHeight = 18;
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            QualitySettings.SetQualityLevel(oldQuality, true);
        }
    }

    private Ship Spawn(string path, Vector2 position)
    {
        var template = TestAssets.LoadShipPrefab(path);
        Assert.That(template, Is.Not.Null);
        return Session.Units.SpawnShip(template, null, 0, Session.Frame.Place(position), GamePlane.Rotation, null);
    }

    private static void ConfigureView(Camera camera, Light light)
    {
        camera.backgroundColor = new Color(.02f, .025f, .045f);
        camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        light.intensity = 1.1f;
        light.color = new Color(.92f, .94f, 1);
    }
}
#endif
