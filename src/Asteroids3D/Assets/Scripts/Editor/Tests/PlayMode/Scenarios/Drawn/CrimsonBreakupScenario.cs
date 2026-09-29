#if UNITY_EDITOR
using System.Collections;
using Capture;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.PlayMode.Scenarios.Drawn
{
    public sealed class CrimsonBreakupScenario : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
        public override CaptureConfig Config => new()
        {
            clipName = "CrimsonBreakup", width = 1440, height = 810,
            everyFixedSteps = 1, minHalfHeight = 5, padding = 0,
            configureView = (camera, light) =>
            {
                camera.allowHDR = true;
                camera.backgroundColor = new Color(.025f, .04f, .075f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                light.intensity = 1.2f;
            }
        };

        public override IEnumerator Run()
        {
            var template = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
            var ship = Session.Units.SpawnShip(template, null, 0, Vector3.zero, Quaternion.Euler(0, 0, -20), null);
            var stage = new GameObject("Crimson breakup lighting");
            var volume = stage.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Visuals/Studies/DrawnArt/Lighting/PodBloom.asset");
            try
            {
                yield return null;
                Film(ship);
                for (var frame = 0; frame < 260; frame++)
                {
                    if (frame == 65)
                    {
                        ship.Body.linearVelocity = new Vector3(.4f, .2f, 0);
                        TestDamage.Kill(ship);
                    }
                    yield return new WaitForFixedUpdate();
                    FilmStep();
                }
            }
            finally
            {
                Object.Destroy(stage);
            }
        }
    }
}
#endif
