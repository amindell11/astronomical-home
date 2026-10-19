using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CanopyCapture
{
    const string Output = "D:/amind/git/agent-4/scratch/canopy-reflections/";
    public static string Setup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
        RenderSettings.fog = false;
        var report = new StringBuilder();
        string[] names = { "Crimson", "Nightshade", "Valis", "Vanguard" };
        for (int i = 0; i < names.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/" + names[i] + ".prefab");
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            ship.name = names[i];
            foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                if (!AnimationUtility.CalculateTransformPath(renderer.transform, ship.transform).StartsWith("ShipBaseRig/Hull/"))
                { renderer.enabled = false; continue; }
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter ? filter.sharedMesh : null;
                if (mesh == null) { renderer.enabled = false; continue; }
                report.AppendLine(names[i] + "/" + renderer.name + " mesh=" + mesh.name + " asset=" + AssetDatabase.GetAssetPath(mesh)
                    + " localBounds=" + mesh.bounds + " worldBounds=" + renderer.bounds + " scale=" + renderer.transform.lossyScale
                    + " rotation=" + renderer.transform.rotation.eulerAngles + " path=" + AnimationUtility.CalculateTransformPath(renderer.transform, ship.transform));
                report.AppendLine("  materials: " + string.Join(" | ", renderer.sharedMaterials.Select(m => m == null ? "NULL" : m.name + " (" + m.shader.name + ") " + AssetDatabase.GetAssetPath(m))));
                if (renderer is SkinnedMeshRenderer skin)
                    report.AppendLine("  bones=" + string.Join(",", skin.bones.Select(b => b.name)) + " root=" + (skin.rootBone ? skin.rootBone.name : "none"));
            }
            foreach (var ps in ship.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ship.transform.position = new Vector3((i - 1.5f) * 5, 0, 0);
        }
        var light = new GameObject("CaptureLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(20, -25, 0);
        RenderSettings.sun = light;
        var camera = new GameObject("CaptureCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3.2f;
        camera.transform.position = new Vector3(0, 0, -30);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .05f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 100;
        camera.cullingMask = -1;
        camera.allowHDR = false;
        File.WriteAllText(Output + "unity-inspection.txt", report.ToString());
        return report.ToString();
    }

    public static string Render(string filename, float yaw = 0, float bank = 0)
    {
        foreach (string name in new[] { "Crimson", "Nightshade", "Valis", "Vanguard" })
            GameObject.Find(name).transform.rotation = Quaternion.Euler(0, bank, yaw);
        var camera = GameObject.Find("CaptureCamera").GetComponent<Camera>();
        int width = 2400, height = 768;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Output + filename, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(rt);
        return filename;
    }

    public static string Stills()
    {
        Setup();
        Render("after.png");
        Render("after-bank.png", 25, 35);
        Render("after-underside.png", 0, 180);
        return "Final stills captured";
    }

    public static string FirstMotion() { return Motion(0, 60); }
    public static string LastMotion() { return Motion(60, 60); }
    public static string FirstExtreme() { return Motion(0, 80, true); }
    public static string LastExtreme() { return Motion(80, 80, true); }
    public static string GlintsOff()
    {
        Setup();
        var canopy = GameObject.Find("Nightshade").GetComponentsInChildren<Renderer>().First(r => r.name == "Canopy");
        var original = canopy.sharedMaterial;
        var without = new Material(original);
        without.SetVector("_SecondaryGlintLeft", new Vector4(.5f, .5f, 1, 0));
        without.SetVector("_SecondaryGlintRight", new Vector4(.5f, .5f, 1, 0));
        canopy.sharedMaterial = without;
        try { return Motion(0, 80, true, "accents-off"); }
        finally
        {
            canopy.sharedMaterial = original;
            UnityEngine.Object.DestroyImmediate(without);
        }
    }

    public static string Motion(int first, int count, bool extreme = false, string outputFolder = null)
    {
        string[] names = { "Crimson", "Nightshade", "Valis", "Vanguard" };
        var ships = names.Select(GameObject.Find).ToArray();
        var camera = GameObject.Find("CaptureCamera").GetComponent<Camera>();
        var rt = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tile = new Texture2D(512, 640, TextureFormat.RGB24, false);
        var sheet = new Texture2D(2048, 640, TextureFormat.RGB24, false);
        camera.orthographicSize = .48f;
        camera.targetTexture = rt;
        string folder = outputFolder ?? (extreme ? "extreme" : "motion");
        Directory.CreateDirectory(Output + folder);
        for (int frame = first; frame < first + count; frame++)
        {
            float phase = 2 * Mathf.PI * (extreme ? frame % 80 / 80f : frame / 120f);
            for (int ship = 0; ship < ships.Length; ship++)
            {
                ships[ship].transform.rotation = extreme
                    ? Quaternion.Euler(frame >= 80 ? Mathf.Sin(phase) * 65 : 0, frame < 80 ? Mathf.Sin(phase) * 75 : 0, 15)
                    : Quaternion.Euler(0, Mathf.Sin(phase) * 40, Mathf.Sin(phase * 2) * 35);
                var renderer = ships[ship].GetComponentsInChildren<Renderer>().First(r =>
                    ship == 0 ? r.name == "Hull" : ship == 2 ? r.name == "Valis" : r.name == "Canopy");
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
                Vector3 center = renderer.transform.TransformPoint(mesh.GetSubMesh(ship == 2 ? 5 : 0).bounds.center);
                camera.transform.position = new Vector3(center.x, center.y, -30);
                camera.Render();
                RenderTexture.active = rt;
                tile.ReadPixels(new Rect(0, 0, 512, 640), 0, 0);
                tile.Apply();
                sheet.SetPixels(ship * 512, 0, 512, 640, tile.GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes(Output + folder + "/" + frame.ToString("D3") + ".png", sheet.EncodeToPNG());
        }
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(sheet);
        UnityEngine.Object.DestroyImmediate(tile);
        UnityEngine.Object.DestroyImmediate(rt);
        return "Motion frames " + first + " through " + (first + count - 1);
    }
}
