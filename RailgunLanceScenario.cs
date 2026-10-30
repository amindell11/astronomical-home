#if UNITY_EDITOR
using System;
using System.Collections;
using Capture;
using Capture.Recording;
using Capture.Views;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Recording
{
    public sealed class RailgunLanceScenario : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            var shooter = Session.Units.SpawnShip(
                TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"), null, 0,
                Session.Frame.Place(new Vector2(-30f, 0f)),
                GamePlane.Rotation * Quaternion.AngleAxis(-90f, Vector3.forward), null);
            var target = Session.Units.SpawnShip(TestAssets.LoadCrimsonPrefab(), null, 1,
                Session.Frame.Place(new Vector2(30f, 8f)),
                GamePlane.Rotation * Quaternion.AngleAxis(90f, Vector3.forward), null);
            shooter.Body.constraints = RigidbodyConstraints.FreezeAll;
            target.Body.constraints = RigidbodyConstraints.FreezeAll;
            target.Damage.SetInvulnerability(60f);
            shooter.Reequip(TestAssets.Load<WeaponComponent>("Assets/Prefabs/Weapons/Railgun.prefab"), null);
            Session.Units.WireShipDependencies(shooter, null);
            var railgun = (Railguns)shooter.Weapons.Primary;
            var shots = 0;
            railgun.OnBeamFired += (_, _) => shots++;

            var observer = TestAssets.NewObserverCam();
            Assert.IsNotNull(observer);
            var lightRig = new GameObject("RailgunCaptureLight");
            try
            {
                observer.SetManualCenter(Vector2.zero);
                observer.SetManualZoom(22f);
                var light = lightRig.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.LookRotation(GamePlane.Rotation * new Vector3(0.4f, -0.3f, 1f));
                using var pacing = CapturePacing.Locked();
                using var view = (IDisposable)Capture.GetType().GetMethod("PrepareGameTopdown")
                    .Invoke(Capture, new object[] { observer });
                using var recording = Capture.Prepare(new CaptureRequest("results/capture",
                    nameof(RailgunLanceScenario), Commit, 1280, 720));
                yield return recording.Ready;
                yield return new WaitForSeconds(1f);
                recording.Start();
                yield return new WaitForSeconds(0.5f);
                for (var shot = 0; shot < 3; shot++)
                {
                    var distance = shot == 0 ? 60f : shot == 1 ? 55f : 24f;
                    var offset = shot == 0 ? GamePlane.PlaneDirToWorld(Vector2.up) * 8f : Vector3.zero;
                    target.Body.position = railgun.FireOrigin + railgun.FireDirection * distance + offset;
                    Physics.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForSeconds(0.3f);
                    var before = shots;
                    for (var step = 0; step < 150 && shots == before; step++)
                    {
                        railgun.HandleTrigger(new WeaponCommand { held = true }, Session.Projectiles);
                        yield return new WaitForFixedUpdate();
                    }
                    Assert.AreEqual(before + 1, shots, "the real mounted railgun completes its authored charge");
                    railgun.HandleTrigger(default, Session.Projectiles);
                    yield return new WaitForSeconds(1f);
                }
                Result = recording.Finish();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightRig);
                if (observer) UnityEngine.Object.DestroyImmediate(observer.gameObject);
            }
        }
    }
}
#endif
