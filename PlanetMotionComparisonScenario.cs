#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Capture;
using Capture.Recording;
using Capture.Views;
using NUnit.Framework;
using Ships;
using Substrate.Services.Locales;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlanetMotionComparisonScenario : CaptureScenario
{
    public override bool NeedsSession => false;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        var camera = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Camera>("Assets/Prefabs/Cameras/Main Camera.prefab"));
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -100), Quaternion.identity);
        camera.orthographic = true;
        camera.orthographicSize = 7;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowHDR = true;
        camera.cullingMask = (1 << LayerMask.NameToLayer("Sky")) | (1 << 31);
        var data = camera.GetComponent(Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"));
        var cameraFields = new SerializedObject(data);
        cameraFields.FindProperty("m_RenderPostProcessing").boolValue = true;
        cameraFields.ApplyModifiedPropertiesWithoutUndo();
        var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
        var rig = Object.Instantiate(ship.VisualRig);
        rig.transform.SetPositionAndRotation(new Vector3(-2, -.8f, 0), Quaternion.Euler(0, 0, -25));
        foreach (var child in rig.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        foreach (var particle in rig.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = rig.Silhouette.Contains(renderer) || (renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_ContourColor"));
        var previousScale = Time.timeScale;
        var previousLighting = LocaleLighting.Capture();
        var previousProbe = RenderSettings.ambientProbe;
        var materials = new List<Material>();
        LocaleSky locale = null;
        Time.timeScale = 1;
        try
        {
            using var pacing = CapturePacing.Locked(3);
            using var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null);
            for (var id = 6; id <= 7; id++)
            foreach (var stronger in new[] { false, true })
            {
                locale = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/Locale_{id}.prefab"));
                locale.ApplyLighting();
                foreach (var renderer in locale.GetComponentsInChildren<Renderer>(true)) materials.AddRange(renderer.materials);
                var planet = materials.Single(m => m.shader.name == "Locales/Planet");
                var rotation = planet.GetFloat("_RotationSpeed");
                var longitude = planet.GetFloat("_SurfaceLongitude");
                var banks = materials.Where(m => m.HasProperty("_NebulaSpeed")).ToArray();
                var bankSpeeds = banks.Select(m => m.GetFloat("_NebulaSpeed")).ToArray();
                if (stronger)
                {
                    var center = planet.GetVector("_PlanetCenter");
                    var origin = new Vector4(0, 0, -100, 0);
                    planet.SetVector("_PlanetCenter", origin + (center - origin) * .5f);
                    planet.SetFloat("_PlanetRadius", planet.GetFloat("_PlanetRadius") * .5f);
                }
                planet.SetFloat("_RotationSpeed", 0);
                foreach (var bank in banks) bank.SetFloat("_NebulaSpeed", 0);
                camera.transform.position = new Vector3(0, 0, -100);
                rig.transform.position = new Vector3(-2, -.8f, 0);
                yield return null;
                yield return null;
                var seconds = 0f;
                var animated = false;
                var variant = stronger ? "B-stronger" : "A-gentle";
                using (var recording = Capture.Prepare(new CaptureRequest("results/capture", $"Locale_{id}-{variant}-v6", Commit, 1280, 720,
                           measurements: new Dictionary<string, Func<double>>
                           {
                               ["locale_seconds"] = () => seconds,
                               ["camera_x"] = () => camera.transform.position.x,
                               ["camera_y"] = () => camera.transform.position.y,
                               ["camera_half_height"] = () => camera.orthographicSize,
                               ["surface_animated"] = () => animated ? 1 : 0,
                               ["motion_strength"] = () => stronger ? 2 : 1
                           })))
                {
                    yield return recording.Ready;
                    recording.Start();
                    var start = Time.time;
                    while (seconds < 20)
                    {
                        seconds = Time.time - start;
                        if (!animated && seconds >= 10)
                        {
                            animated = true;
                            planet.SetFloat("_SurfaceLongitude", longitude + Time.time * rotation * Mathf.Rad2Deg);
                            planet.SetFloat("_RotationSpeed", rotation);
                            for (var bank = 0; bank < banks.Length; bank++) banks[bank].SetFloat("_NebulaSpeed", bankSpeeds[bank]);
                        }
                        var phaseSeconds = animated ? seconds - 10 : seconds;
                        var travel = Mathf.Sin(Mathf.Clamp01((phaseSeconds - 2) / 6) * Mathf.PI);
                        var position = new Vector3(120, 60, 0) * travel;
                        camera.transform.position = position + new Vector3(0, 0, -100);
                        rig.transform.position = position + new Vector3(-2, -.8f, 0);
                        yield return null;
                    }
                    yield return null;
                    Result = recording.Finish();
                    Assert.That(Result.FrameCount, Is.GreaterThan(300));
                    Debug.Log("PLANET_COMPARISON=" + id + " " + variant + " FRAMES=" + Result.FrameDirectory);
                }
                Object.Destroy(locale.gameObject);
                locale = null;
                foreach (var material in materials) Object.Destroy(material);
                materials.Clear();
                yield return null;
            }
        }
        finally
        {
            if (locale) Object.DestroyImmediate(locale.gameObject);
            foreach (var material in materials) Object.DestroyImmediate(material);
            Object.DestroyImmediate(rig.gameObject);
            Object.DestroyImmediate(camera.gameObject);
            Time.timeScale = previousScale;
            previousLighting.Apply();
            RenderSettings.ambientProbe = previousProbe;
        }
    }
}
#endif
