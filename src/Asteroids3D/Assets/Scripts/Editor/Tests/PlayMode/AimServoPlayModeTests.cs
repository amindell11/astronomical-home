#if UNITY_EDITOR
using System.Collections;
using AI.Strategy;
using NUnit.Framework;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>The aim servo through the real commander path and physics: a full-authority AIM brings the nose onto a still target and holds it there.</summary>
    [TestFixture]
    [Category("AI")]
    public class AimServoPlayModeTests : AIIntegrationFixture
    {
        private const int SettleSteps = 50;
        private const int HoldSteps = 25;
        private const float HoldToleranceDeg = 3f;

        protected override bool AccelerateTime => true;

        [UnityTest]
        public IEnumerator FullAuthorityAim_HoldsTheNoseOnAStillTarget_WithinASecond()
        {
            // The servo is under test, so the Gunner sees the true target.
            var (shooter, cmdr) = CreateAIShip(Vector3.zero, team: 0, aimSettings: TestAimSettings.Perfect);
            var (target, _) = CreateAIShip(new Vector3(12f, 4f, 0f), team: 1);

            cmdr.InstallBrain<ScriptedBrain>().decision = new BrainDecision(
                NavObjective.Anchored(target.Id).Planar(Vector2.zero).Facing(0f, 1f));

            for (var i = 0; i < SettleSteps; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(cmdr.Gunner.HasTarget, "the shooter's Gunner must hold the target for an aim reference");

            var worstDeg = 0f;
            for (var i = 0; i < HoldSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                var self = shooter.Kinematics;
                var bearing = target.Kinematics.pos - self.pos;
                worstDeg = Mathf.Max(worstDeg, Vector2.Angle(self.Forward, bearing));
            }

            Assert.Less(worstDeg, HoldToleranceDeg,
                $"after {SettleSteps} steps the nose must stay within {HoldToleranceDeg}° of the target");
        }
    }
}
#endif
