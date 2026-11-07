#if UNITY_EDITOR
using System.Collections;
using AI;
using Capture;
using Combat.Weapons;
using Ships;
using Ships.Loadout;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class LaserImpactCapture : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
        public override CaptureConfig Config => new()
        {
            clipName = "LaserImpact", width = 1280, height = 720,
            everyFixedSteps = 1, minHalfHeight = 8f, padding = 3f
        };

        public override IEnumerator Run()
        {
            var a = Spawn(new Vector2(-6f, 0f), -90f, 0);
            var b = Spawn(new Vector2(6f, 0f), 90f, 1);
            Film(a, b);
            for (var i = 0; i < 250 && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
        }

        private Ship Spawn(Vector2 position, float angle, int team)
        {
            var ship = TestAssets.LoadCrimsonPrefab();
            var laser = TestAssets.Load<WeaponComponent>("Assets/Prefabs/Weapons/Lasers.prefab");
            return Session.Units.SpawnShip(ship,
                TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AgentPilot.prefab"), team,
                Session.Frame.Place(position), GamePlane.Rotation * Quaternion.AngleAxis(angle, Vector3.forward),
                field: null, loadout: new ShipLoadout(ship, ship.Shield, laser, null));
        }
    }
}
#endif
