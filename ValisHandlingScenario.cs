using System.Collections;
using Capture.Views;
using Ships;
using Ships.Command;
using Ships.Keystones.Kits;
using Substrate;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;

public sealed class ValisHandlingScenario : CaptureScenario
{
    public static string Load() => "Valis handling scenario loaded";
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    protected override DiagnosticViewConfig View => new() { minHalfHeight = 8f, padding = 4f };

    public override IEnumerator Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
        var current = Session.Units.SpawnShip(prefab, null, 0, new Vector3(-7f, 0f, 0f), GamePlane.Rotation, null);
        var proposed = Session.Units.SpawnShip(prefab, null, 0, new Vector3(7f, 0f, 0f), GamePlane.Rotation, null);
        var settings = new SerializedObject(current.Keystone);
        settings.FindProperty("maneuverabilityScale").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        yield return Film(current, proposed);
        var step = new WaitForFixedUpdate();
        for (var i = 0; i < 25; i++) yield return step;
        for (var i = 0; i < 280; i++)
        {
            var command = new PilotCommand { boost = i == 0 ? 1f : 0f, yawTorque = i < 35 ? .35f : 0f };
            current.Movement.Drive(command);
            proposed.Movement.Drive(command);
            yield return step;
        }
        current.ResetShip();
        proposed.ResetShip();
        current.transform.SetPositionAndRotation(new Vector3(-7f, 0f, 0f), GamePlane.Rotation);
        proposed.transform.SetPositionAndRotation(new Vector3(7f, 0f, 0f), GamePlane.Rotation);
        current.Rigidbody.position = current.transform.position;
        proposed.Rigidbody.position = proposed.transform.position;
        current.Rigidbody.rotation = GamePlane.Rotation;
        proposed.Rigidbody.rotation = GamePlane.Rotation;
        Physics.SyncTransforms();
        for (var i = 0; i < 25; i++) yield return step;
        for (var i = 0; i < 280; i++)
        {
            var command = new PilotCommand { boost = i == 0 ? 1f : 0f, strafe = i < 70 ? 1f : 0f };
            current.Movement.Drive(command);
            proposed.Movement.Drive(command);
            yield return step;
        }
    }
}
