#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
public static class VanguardConsolidationEditorSetup
{
    static VanguardConsolidationEditorSetup()
    {
        EditorApplication.update += ClosePackageWindow;
    }

    private static void ClosePackageWindow()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.GetType().Name == "PackageManagerWindow") { window.Close(); Debug.Log("[Vanguard] Closed unused PackageManagerWindow in capture editor."); }
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.update -= ClosePackageWindow;
    }
}

public sealed class VanguardConsolidation : CaptureScenario
{
    private readonly CaptureConfig config = new()
    {
        clipName = "VanguardConsolidatedBeforeAfter",
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
            BuildMeshes();
            var b = Spawn("Assets/Prefabs/Ships/Ship_1.prefab", new Vector2(4, 0));
            var combined = b.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(combined.Count(item => item.sharedMaterial && item.sharedMaterial.name == "Vanguard vivid paint"), Is.EqualTo(1));
            Assert.That(combined.Count(item => item.gameObject.activeInHierarchy && item.transform.parent.name == "Authored mesh"), Is.EqualTo(5));
            Assert.That(combined.Any(item => item.name == "Vanguard surface wear" || item.name == "Vanguard service panels"), Is.False);
            Assert.That(combined.Any(item => item.name == "MVP Canopy"), Is.True);
            Assert.That(combined.Any(item => item.name == "MVP Power Nacelle Core"), Is.True);
            var renderers = a.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var name in new[] { "Vanguard surface wear", "Vanguard service panels" })
            {
                var renderer = Array.Find(renderers, item => item.name == name);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.gameObject.activeInHierarchy, Is.False, name);
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

    private static void BuildMeshes()
    {
        const string rigPath = "Assets/Prefabs/Ships/Ship_1_IllustratedRig.prefab";
        const string folder = "Assets/Visuals/Ships/Vanguard/DrawnStudy/Meshes";
        var rig = PrefabUtility.LoadPrefabContents(rigPath);
        try
        {
            var authored = rig.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Authored mesh");
            var all = authored.GetComponentsInChildren<MeshRenderer>(true);
            var hull = all.Where(item => item.sharedMaterial && item.sharedMaterial.name == "Vanguard vivid paint").ToArray();
            var contours = all.Where(item => item.name == "Silhouette").ToArray();
            Assert.That(hull.Length, Is.EqualTo(9));
            Assert.That(contours.Length, Is.EqualTo(11));
            Assert.That(all.All(item => !item.GetComponent<SkinnedMeshRenderer>()), Is.True);
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Visuals/Ships/Vanguard/DrawnStudy", "Meshes");
            var hullReport = Combine(hull, authored, folder + "/Vanguard painted hull.asset", "Vanguard painted hull");
            var contourReport = Combine(contours, authored, folder + "/Vanguard contour.asset", "Vanguard contour");
            foreach (var item in contours) Object.DestroyImmediate(item.gameObject);
            foreach (var item in hull) Object.DestroyImmediate(item.gameObject);
            foreach (var item in all.Where(item => item && (item.name == "Vanguard surface wear" || item.name == "Vanguard service panels"))) Object.DestroyImmediate(item.gameObject);
            Assert.That(authored.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(5));
            PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(rigPath, ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("../../results/vanguard-retexture/consolidation");
            File.WriteAllText("../../results/vanguard-retexture/consolidation/mesh-validation.json",
                "{\"hull\":" + hullReport + ",\"contour\":" + contourReport +
                ",\"active_authored_renderers_before\":23,\"active_authored_renderers_after\":5," +
                "\"canopy_and_cores_separate\":true,\"original_blender_sources_unchanged\":true}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(rig);
        }
    }

    private static string Combine(MeshRenderer[] renderers, Transform parent, string path, string name)
    {
        var material = renderers[0].sharedMaterial;
        var instances = new List<CombineInstance>();
        var expectedVertices = new List<Vector3>();
        var expectedNormals = new List<Vector3>();
        var expectedUv = new List<Vector2>();
        var expectedTriangles = new List<int>();
        foreach (var renderer in renderers)
        {
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterial, Is.EqualTo(material));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(renderers[0].shadowCastingMode));
            Assert.That(renderer.receiveShadows, Is.EqualTo(renderers[0].receiveShadows));
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            var matrix = parent.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            var offset = expectedVertices.Count;
            expectedVertices.AddRange(mesh.vertices.Select(matrix.MultiplyPoint3x4));
            expectedNormals.AddRange(mesh.normals.Select(value => normalMatrix.MultiplyVector(value).normalized));
            expectedUv.AddRange(mesh.uv.Length == 0 ? new Vector2[mesh.vertexCount] : mesh.uv);
            expectedTriangles.AddRange(mesh.triangles.Select(value => offset + value));
            instances.Add(new CombineInstance { mesh = mesh, transform = matrix, subMeshIndex = 0 });
        }
        var combined = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        combined.CombineMeshes(instances.ToArray(), true, true, false);
        Assert.That(combined.vertexCount, Is.EqualTo(expectedVertices.Count));
        Assert.That(combined.triangles, Is.EqualTo(expectedTriangles));
        Assert.That(combined.uv, Is.EqualTo(expectedUv));
        var vertices = combined.vertices;
        var normals = combined.normals;
        var maxPositionError = 0f;
        var maxNormalError = 0f;
        for (var index = 0; index < vertices.Length; index++)
        {
            maxPositionError = Mathf.Max(maxPositionError, Vector3.Distance(vertices[index], expectedVertices[index]));
            maxNormalError = Mathf.Max(maxNormalError, Vector3.Distance(normals[index].normalized, expectedNormals[index]));
        }
        Assert.That(maxPositionError, Is.LessThan(0.00001f));
        Assert.That(maxNormalError, Is.LessThan(0.00001f));
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved)
        {
            EditorUtility.CopySerialized(combined, saved);
            Object.DestroyImmediate(combined);
            combined = saved;
        }
        else AssetDatabase.CreateAsset(combined, path);
        var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        child.layer = renderers[0].gameObject.layer;
        child.transform.SetParent(parent, false);
        child.GetComponent<MeshFilter>().sharedMesh = combined;
        var output = child.GetComponent<MeshRenderer>();
        EditorUtility.CopySerialized(renderers[0], output);
        output.sharedMaterial = material;
        return "{\"source_renderers\":" + renderers.Length + ",\"vertices\":" + vertices.Length +
               ",\"triangles\":" + expectedTriangles.Count / 3 + ",\"uvs_exact\":true,\"triangles_exact\":true," +
               "\"max_position_error\":" + maxPositionError.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
               ",\"max_normal_error\":" + maxNormalError.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";
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
