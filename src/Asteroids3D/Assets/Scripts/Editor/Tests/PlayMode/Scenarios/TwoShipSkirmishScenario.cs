#if UNITY_EDITOR
using System.Collections;
using Game.Capture;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Committed sample scenario (runner + render smoke, and the living doc for authoring new ones): two policy-pilot ships skirmish for a few seconds under the standard ship diagnostics overlay.</summary>
    public sealed class TwoShipSkirmishScenario : CaptureScenario
    {
        private const float SimSeconds = 8f;

        public override IEnumerator Run(CaptureRecorder recorder)
        {
            var (a, _) = SpawnCombatShip(new Vector2(-12f, 0f), rotDeg: -90f, team: 0);
            var (b, _) = SpawnCombatShip(new Vector2(12f, 0f), rotDeg: 90f, team: 1);

            var subjects = new Vector2[2];
            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var i = 0; i < steps && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
                subjects[0] = a.Kinematics.pos;
                subjects[1] = b.Kinematics.pos;
                recorder.Step(subjects, ctx => ShipDiagnosticsOverlay.Draw(ctx, a, b, Session.Services.Projectiles));
            }
        }
    }
}
#endif
