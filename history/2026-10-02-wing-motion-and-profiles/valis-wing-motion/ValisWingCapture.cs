#if UNITY_EDITOR
using System.Collections;
using Capture;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

public sealed class ValisWingCapture : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new CaptureConfig
    {
        clipName = "Valis-wing-motion-unity",
        outputRoot = "D:/amind/git/astronomical-home/results/valis-wing-motion/unity",
        width = 800, height = 800,
        minHalfHeight = 1.55f, padding = .15f,
        everyFixedSteps = 2,
        configureView = (camera, light) =>
        {
            camera.backgroundColor = new Color(.027f, .038f, .06f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
    };

    public override IEnumerator Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
        var ship = Session.Units.SpawnShip(prefab, null, 0, Vector3.zero, Quaternion.identity, null);
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        foreach (var t in ship.GetComponentsInChildren<Transform>(true))
            if (t.name == "MinimapMarker") t.gameObject.SetActive(false);
        yield return null;
        Film(ship);
        foreach (var thrust in new[] { 0f, 1f, 0f, -1f, 0f })
        {
            for (var step = 0; step < 85; step++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = thrust });
                ship.Rigidbody.position = Vector3.zero;
                ship.Rigidbody.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
        }
    }
}
#endif
