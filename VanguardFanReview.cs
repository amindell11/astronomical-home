#if UNITY_EDITOR
using System.Collections;
using Capture.Recording;
using Capture.Views;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class VanguardFanReview : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
        protected override CaptureRequest Request => new("results/capture", GetType().Name, Commit, 1920, 1080);
        protected override DiagnosticViewConfig View => new() { minHalfHeight = 22f, padding = 5f };
        public override SectorEntry SectorEntry => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>("Assets/Prefabs/Sectors/Preview/CrowdedJumpArrival.prefab"),
            locale = AssetDatabase.LoadAssetAtPath<LocaleSky>("Assets/Prefabs/Locales/IllustratedBlue.prefab")
        };

        public override IEnumerator Run()
        {
            const string materials = "Assets/Visuals/Vfx/KeystoneTrails/Materials/";
            var template = TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            var oldPaint = AssetDatabase.LoadAssetAtPath<Material>(materials + "IonWake.mat");
            var newPaint = AssetDatabase.LoadAssetAtPath<Material>(materials + "VanguardWake.mat");
            Assert.IsNotNull(oldPaint);
            Assert.IsNotNull(newPaint);
            var ships = new Ship[2];
            var strokes = new ParticleSystem[2];
            for (var i = 0; i < ships.Length; i++)
            {
                ships[i] = Session.Units.SpawnShip(template, null, i,
                    Session.Frame.Place(new Vector2(i == 0 ? -4.5f : 4.5f, -10f)), GamePlane.Rotation,
                    Session.ActiveSector ? Session.ActiveSector.ObstacleField : null);
                strokes[i] = ships[i].Keystone.transform.Find("Trail/Streaks").GetComponent<ParticleSystem>();
                var renderer = strokes[i].GetComponent<ParticleSystemRenderer>();
                Assert.AreEqual(newPaint, renderer.sharedMaterial, "Production Vanguard must carry the new fan.");
                if (i == 0) renderer.sharedMaterial = oldPaint;
            }

            var left = ships[0].Body;
            var right = ships[1].Body;
            var separation = right.position - left.position;
            yield return Film(ships);
            var dt = Time.fixedDeltaTime;
            var first = Mathf.RoundToInt(1.2f / dt);
            var second = Mathf.RoundToInt(6.4f / dt);
            var turning = second - Mathf.RoundToInt(0.7f / dt);
            var steps = second + Mathf.CeilToInt(1.15f / dt);
            var lastActive = false;
            var lastParticles = 0;
            for (var step = 0; step < steps; step++)
            {
                var input = new PilotCommand
                {
                    thrust = 0.7f,
                    yawTorque = step >= turning ? 1f : 0f,
                    boost = step == first || step == second ? 1f : 0f
                };
                foreach (var ship in ships) ship.Movement.Drive(input);
                yield return new WaitForFixedUpdate();
                Assert.Less(Quaternion.Angle(left.rotation, right.rotation), 0.1f, $"Headings differed at step {step}.");
                Assert.Less(Vector3.Distance(left.linearVelocity, right.linearVelocity), 0.02f, $"Velocities differed at step {step}.");
                Assert.Less(Vector3.Distance(right.position - left.position, separation), 0.04f, $"Separation changed at step {step}.");
                var active = ships[0].Keystone.Active;
                var count = strokes[0].particleCount;
                Assert.AreEqual(active, ships[1].Keystone.Active, $"Guard timing differed at step {step}.");
                Assert.AreEqual(count, strokes[1].particleCount, $"Wake timing differed at step {step}.");
                if (active != lastActive || count != lastParticles)
                    Debug.Log($"FanReview step={step} t={step*dt:F3} active={active} particles={count} activations={ships[0].Keystone.Activations} leftPos={ships[0].Body.position} rightPos={ships[1].Body.position} heading={ships[0].transform.eulerAngles.z:F2} travel={Vector2.SignedAngle(Vector2.up, ships[0].Body.linearVelocity):F2}");
                lastActive = active;
                lastParticles = count;
            }
            foreach (var ship in ships) Assert.AreEqual(2, ship.Keystone.Activations);
            Assert.AreEqual(0, lastParticles);
        }
    }
}
#endif
