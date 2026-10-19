#if UNITY_EDITOR
using System.Collections;
using Capture;
using Damage;
using Ships;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
public sealed class BulwarkSideMountsScenario : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new CaptureConfig {
        clipName = "BulwarkSideMounts", outputRoot = "D:/amind/git/agent-3/results/bulwark/side-mounts/capture",
        width = 960, height = 720, everyFixedSteps = 1, minHalfHeight = 3.2f, padding = 0,
        configureView = (camera, light) => { camera.backgroundColor = new Color(.035f,.045f,.075f); light.transform.rotation = Quaternion.Euler(45,-30,20); light.intensity = 1; }
    };
    public override IEnumerator Run()
    {
        var ship = Session.Units.SpawnShip(AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Bulwark.prefab"), null, 0, Vector3.zero, Quaternion.Euler(0,0,-22), null);
        yield return null;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        Film(ship);
        for (var i=0;i<400;i++) {
            yield return new WaitForFixedUpdate();
            ship.Movement.Drive(new Ships.Command.PilotCommand { thrust = i<300 ? .65f : 0, yawTorque = i>60 && i<250 ? .3f : 0, strafe = i>280 && i<350 ? .5f : 0 });
            if (i>80 && i<290 && i%12==0) ship.Weapons.Arm(Session.Projectiles).Fire(Ships.Command.WeaponSlot.Primary, new Combat.Weapons.WeaponCommand { held=true, pressed=true, targetPoint=ship.transform.position+ship.transform.up*20 });
            if (i>100 && i<280 && i%50==0) ship.Weapons.Arm(Session.Projectiles).Fire(Ships.Command.WeaponSlot.Secondary, new Combat.Weapons.WeaponCommand { held=true, pressed=true, targetPoint=ship.transform.position+ship.transform.up*20 });
            FilmStep();
        }
    }
}
#endif

