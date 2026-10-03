#if UNITY_EDITOR
using System;
using System.Collections;
using Capture;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.PlayMode.Scenarios.Valis
{
    public sealed class ValisPaintCapture : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
        public override CaptureConfig Config => new()
        {
            clipName = "ValisPaintCapture", width = 1280, height = 720,
            everyFixedSteps = 5, minHalfHeight = 6, padding = 0,
            configureView = ConfigureView
        };

        public override IEnumerator Run()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
            var root = new GameObject("Valis capture lighting");
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Rendering/HighRes.asset");
            var template = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var ship = Session.Units.SpawnShip(template, null, 0, Vector3.zero, GamePlane.Rotation, null);
            var renderer = ship.transform.Find("Valis VisualRig/Valis").GetComponent<MeshRenderer>();
            var iris = Array.Find(renderer.materials, m => m.name.StartsWith("Lavender"));
            Film(ship);
            try
            {
                for (var i = 0; i < 150; i++)
                {
                    iris.SetFloat("_EmissionStrength", i < 50 ? 0 : .35f);
                    yield return new WaitForFixedUpdate();
                    FilmStep();
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static void ConfigureView(Camera camera, Light light)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .025f, .045f);
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            light.intensity = 1;
            light.transform.rotation = Quaternion.Euler(25, -35, 0);
        }
    }
}
#endif

