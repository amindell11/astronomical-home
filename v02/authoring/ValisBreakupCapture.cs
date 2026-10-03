#if UNITY_EDITOR
using System.Collections;
using Capture;
using Damage;
using Ships;
using Ships.Command;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

public sealed class ValisBreakupCapture : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new CaptureConfig
    {
        clipName = "Valis-breakup-rest-intermediate-swept",
        outputRoot = "D:/amind/git/agent-1/results/valis-breakup/v02/unity",
        width = 900, height = 900,
        minHalfHeight = 3.1f, padding = .2f,
        everyFixedSteps = 1,
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
        foreach (var prepareSteps in new[] { 0, 10, 30 })
        {
            if (!ship.gameObject.activeSelf) ship.ResetShip();
            for (var step = 0; step < 50; step++)
            {
                ship.Movement.Drive(default);
                ship.Rigidbody.position = Vector3.zero;
                ship.Rigidbody.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
            for (var step = 0; step < prepareSteps; step++)
            {
                ship.Movement.Drive(new PilotCommand { thrust = 1f });
                ship.Rigidbody.position = Vector3.zero;
                ship.Rigidbody.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
            ship.Rigidbody.linearVelocity = new Vector3(.3f, .15f, 0f);
            ship.Damage.TakeDamage(new DamageInfo(100000f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero));
            for (var step = 0; step < 110; step++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
        }
    }
}
#endif

