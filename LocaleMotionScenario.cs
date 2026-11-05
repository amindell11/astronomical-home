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

public sealed class LocaleMotionScenario : CaptureScenario
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
        LocaleSky locale = null;
        Time.timeScale = 1;
        try
        {
            using var pacing = CapturePacing.Locked(3);
            using var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null);
            for (var id = 4; id <= 7; id++)
            {
                locale = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/Locale_{id}.prefab"));
                locale.ApplyLighting();
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographicSize = 7;
                rig.transform.position = new Vector3(-2, -.8f, 0);
                yield return null;
                yield return null;
                var seconds = 0f;
                using (var recording = Capture.Prepare(new CaptureRequest("results/capture", $"Locale_{id}-motion", Commit, 1280, 720,
                           measurements: new Dictionary<string, Func<double>>
                           {
                               ["locale_seconds"] = () => seconds,
                               ["camera_x"] = () => camera.transform.position.x,
                               ["camera_y"] = () => camera.transform.position.y,
                               ["camera_half_height"] = () => camera.orthographicSize
                           })))
                {
                    yield return recording.Ready;
                    recording.Start();
                    var start = Time.time;
                    while (seconds < 12)
                    {
                        seconds = Time.time - start;
                        var travel = Mathf.Sin(Mathf.Clamp01((seconds - 3) / 5) * Mathf.PI);
                        var position = new Vector3(120, 60, 0) * travel;
                        camera.transform.position = position + new Vector3(0, 0, -100);
                        rig.transform.position = position + new Vector3(-2, -.8f, 0);
                        camera.orthographicSize = 7 + 7 * Mathf.Sin(Mathf.Clamp01((seconds - 8) / 4) * Mathf.PI);
                        yield return null;
                    }
                    yield return null;
                    Result = recording.Finish();
                    Assert.That(Result.FrameCount, Is.GreaterThan(30));
                    Debug.Log("LOCALE_MOTION=" + id + " FRAMES=" + Result.FrameDirectory);
                }
                Object.Destroy(locale.gameObject);
                locale = null;
                yield return null;
            }
        }
        finally
        {
            if (locale) Object.DestroyImmediate(locale.gameObject);
            Object.DestroyImmediate(rig.gameObject);
            Object.DestroyImmediate(camera.gameObject);
            Time.timeScale = previousScale;
            previousLighting.Apply();
            RenderSettings.ambientProbe = previousProbe;
        }
    }
}
#endif
