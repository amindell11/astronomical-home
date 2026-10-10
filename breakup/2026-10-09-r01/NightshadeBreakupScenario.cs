#if UNITY_EDITOR
using System.Collections;
using Capture;
using Damage;
using Ships;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

public sealed class NightshadeBreakupScenario : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new CaptureConfig
    {
        clipName = "NightshadeBreakup",
        outputRoot = "C:/Users/amind/.codex/visualizations/2026/10/06/01a112c7-f840-7683-a496-b2b5e5eabf06/breakup-01/capture",
        width = 960, height = 720, everyFixedSteps = 1, minHalfHeight = 2.5f, padding = 0,
        configureView = (camera, light) =>
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.075f, .09f, .13f);
            light.transform.rotation = Quaternion.Euler(45f, -30f, 20f);
            light.intensity = 1f;
        }
    };
    public override IEnumerator Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
        var ship = Session.Units.SpawnShip(prefab, null, 0, Vector3.zero, Quaternion.Euler(0, 0, -22), null);
        yield return null;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        Film(ship);
        for (var i=0; i<145; i++)
        {
            yield return new WaitForFixedUpdate();
            if (i==45)
            {
                ship.Rigidbody.linearVelocity = new Vector3(.45f, .25f, 0);
                ship.Damage.TakeDamage(new DamageInfo(100000, DamageKind.Collision, ShipId.Invalid, 0, Vector3.zero, ship.transform.position));
            }
            FilmStep();
        }
    }
}
#endif
