using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// Renders a chassis with its engine particle systems simulated (seeded, so two prefabs compare like for like),
/// idle and under thrust, whole ship and a tail close-up. Same camera, light and ambient as the pixel harness,
/// but culling Ship + Default so the flames and the reactor glow show.
public static class EngineStill
{
    const int Size = 768;

    public static string Render(string prefabPath, string stem, string outDir, float seconds, string skip)
    {
        var log = new StringBuilder();
        foreach (var thrust in new[] { false, true })
            foreach (var (frame, center, ortho) in new[] { ("whole", Vector3.zero, 1.4f), ("tail", new Vector3(0, -0.85f, 0), 0.45f) })
                log.Append(RenderOne(prefabPath, Path.Combine(outDir, $"{stem}-{(thrust ? "thrust" : "idle")}-{frame}.png"), thrust, center, ortho, seconds, skip)).Append('\n');
        return log.ToString();
    }

    static string RenderOne(string prefabPath, string outPath, bool thrust, Vector3 center, float ortho, float seconds, string skip)
    {
        var root = new GameObject("Engine still");
        var priorTarget = RenderTexture.active;
        var priorAmbient = RenderSettings.ambientLight;
        var priorMode = RenderSettings.ambientMode;
        var target = new RenderTexture(Size, Size, 24);
        var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        try
        {
            var prefab = (GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath);
            root.SetActive(false);
            var subject = Object.Instantiate(prefab, root.transform);
            var thrusters = subject.GetComponentsInChildren<Ships.Visuals.ThrusterVisuals>(true).SelectMany(t => t.thrustParticles).Where(p => p).ToArray();
            foreach (var behaviour in subject.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour.GetType().Namespace ?? "").StartsWith("UnityEngine", StringComparison.Ordinal))
                    Object.DestroyImmediate(behaviour);
            foreach (var canvas in subject.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            subject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = center + new Vector3(0, 0, -6);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = ortho;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.25f, .28f, .35f);
            camera.cullingMask = (1 << 7) | 1;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = target;
            var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(25, -35, 0);
            light.color = new Color(1, .96f, .9f);
            light.intensity = 1.15f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
            root.SetActive(true);
            var played = new StringBuilder();
            foreach (var system in subject.GetComponentsInChildren<ParticleSystem>(false))
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var on = system.main.playOnAwake || thrust && thrusters.Contains(system);
                if (!on || system.name == skip) continue;
                system.useAutoRandomSeed = false;
                system.randomSeed = 1234;
                system.Simulate(seconds, false, true, true);
                played.Append(system.name).Append(' ');
            }
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            File.WriteAllBytes(outPath, image.EncodeToPNG());
            return $"{Path.GetFileName(outPath)}: simulated [{played.ToString().Trim()}]";
        }
        finally
        {
            RenderTexture.active = priorTarget;
            RenderSettings.ambientLight = priorAmbient;
            RenderSettings.ambientMode = priorMode;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    public static string Import(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var go = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
        return go ? $"{path}: {AssetDatabase.AssetPathToGUID(path)} {PrefabUtility.GetPrefabAssetType(go)}" : "not imported";
    }

    public static string Delete(string path) => AssetDatabase.DeleteAsset(path) ? "deleted " + path : "delete failed " + path;
}
