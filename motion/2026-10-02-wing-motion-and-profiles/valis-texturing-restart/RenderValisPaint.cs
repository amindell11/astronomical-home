using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RenderValisPaint
{
    public static string Main()
    {
        var output = "D:/amind/git/agent-2/results/valis-integration/paint";
        Directory.CreateDirectory(output);
        var quality = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
        var root = new GameObject("Valis paint inspection");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab").transform.Find("Valis VisualRig/Valis");
        var hull = new GameObject("Valis saved hull", typeof(MeshFilter), typeof(MeshRenderer));
        hull.transform.SetParent(root.transform, false);
        hull.layer = 30;
        hull.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var materials = source.GetComponent<MeshRenderer>().sharedMaterials.Select(m => new Material(m)).ToArray();
        hull.GetComponent<MeshRenderer>().sharedMaterials = materials;
        var iris = materials.Single(m => m.name.StartsWith("Lavender"));
        var emission = iris.GetColor("_EmissionColor");
        var light = new GameObject("Ship key", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.type = LightType.Directional;
        light.intensity = 1;
        light.cullingMask = 1 << 30;
        light.transform.rotation = Quaternion.Euler(25, -35, 0);
        var camera = new GameObject("Valis camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.enabled = false;
        camera.scene = scene;
        camera.cullingMask = 1 << 30;
        camera.orthographic = true;
        camera.orthographicSize = 1.45f;
        camera.allowHDR = true;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 40;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.015f, .025f, .045f);
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = 1 << 30;
        var volume = new GameObject("Gameplay post processing", typeof(Volume)).GetComponent<Volume>();
        volume.transform.SetParent(root.transform, false);
        volume.gameObject.layer = 30;
        volume.isGlobal = true;
        volume.priority = 100;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Rendering/HighRes.asset");
        var target = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGBHalf);
        var encoded = new RenderTexture(1000, 1000, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        Color32[] Read(string name)
        {
            camera.Render();
            var srgb = GL.sRGBWrite;
            GL.sRGBWrite = true;
            Graphics.Blit(target, encoded);
            GL.sRGBWrite = srgb;
            RenderTexture.active = encoded;
            image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            return image.GetPixels32();
        }
        camera.transform.position = new Vector3(0, 0, -6);
        camera.transform.LookAt(Vector3.zero, Vector3.up);
        iris.SetColor("_EmissionColor", Color.black);
        var before = Read("top-emission-off");
        iris.SetColor("_EmissionColor", emission);
        var after = Read("top");
        var changed = before.Zip(after, (a, b) => a.r != b.r || a.g != b.g || a.b != b.b).Count(d => d);
        if (changed < 100) throw new InvalidOperationException("Iris emission made no visible change.");
        camera.orthographicSize = 9;
        Read("game-scale");
        camera.orthographicSize = 1.45f;
        camera.transform.position = new Vector3(3, 2, -5);
        camera.transform.LookAt(Vector3.zero, Vector3.up);
        Read("quarter");
        camera.targetTexture = null;
        RenderTexture.active = null;
        foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(encoded);
        UnityEngine.Object.DestroyImmediate(image);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        QualitySettings.SetQualityLevel(quality, true);
        return "Rendered saved Valis with gameplay post processing; changed emission pixels=" + changed;
    }
}
