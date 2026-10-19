#if UNITY_EDITOR
using System.Collections;
using Capture;
using Damage;
using Ships;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
public sealed class BulwarkBreakupScenario : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new CaptureConfig {
        clipName = "BulwarkBreakup", outputRoot = "D:/amind/git/agent-3/results/bulwark/captures/2026-10-19/breakup",
        width = 960, height = 720, everyFixedSteps = 1, minHalfHeight = 3.2f, padding = 0,
        configureView = (camera, light) => { camera.backgroundColor = new Color(.035f,.045f,.075f); light.transform.rotation = Quaternion.Euler(45,-30,20); light.intensity = 1; }
    };
    public override IEnumerator Run()
    {
        var ship = Session.Units.SpawnShip(AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Bulwark.prefab"), null, 0, Vector3.zero, Quaternion.Euler(0,0,-22), null);
        yield return null;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        Film(ship);
        for (var i=0;i<170;i++) {
            yield return new WaitForFixedUpdate();
            if(i==60) { ship.Rigidbody.linearVelocity = new Vector3(.45f,.25f,0); ship.Damage.TakeDamage(new DamageInfo(100000, DamageKind.Collision, ShipId.Invalid, 0, Vector3.zero, ship.transform.position)); }
            FilmStep();
        }
    }
}
#endif
