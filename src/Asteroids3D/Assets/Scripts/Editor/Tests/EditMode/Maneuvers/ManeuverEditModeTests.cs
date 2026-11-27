#if UNITY_EDITOR
using AI.Maneuvers;
using AI.Pilots;
using AI.Tactics;
using Movement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Maneuvers
{
    [Category("AI")]
    public class ManeuverEditModeTests
    {
        private const float Speed = 12f;
        private const float GunRange = 10f;

        private TacticsSettings temper;
        private Kinematics self;
        private Kinematics them;

        [SetUp]
        public void SetUp()
        {
            temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.attackRunMargin = 4f;
            self = new Kinematics(new Vector2(3f, -4f), new Vector2(1f, 2f), 0f, 0f, 0f);
            them = new Kinematics(new Vector2(40f, 0f), Vector2.zero, 0f, 0f, 0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(temper);

        private static Situation Fight(bool inTargetsCone) =>
            new(1f, 1f, float.PositiveInfinity, hasTarget: true, distance: 15f, targetRange: 20f,
                inTargetsCone: inTargetsCone, alliesInLane: 0);

        private ManeuverFrame Frame(Situation situation) =>
            new(self, them, situation, temper, Speed, maxSpeed: 20f, speedFraction: 0.8f, gunRange: GunRange,
                flankSide: 1, place: 0, wounded: false);

        [Test]
        public void AttackRun_FliesTheHoldRangePastItsGunRange_AndEngages()
        {
            var frame = Frame(Fight(inTargetsCone: false));
            var step = new AttackRun().Fly(in frame);

            Assert.AreEqual(RangerBrain.HoldRangeVelocity(self, them, GunRange + temper.attackRunMargin, Speed),
                step.velocity);
            Assert.IsTrue(step.engages);
        }

        [Test]
        public void StandOff_DoesNotEngage_InTheTargetsCone()
        {
            var frame = Frame(Fight(inTargetsCone: true));
            var step = new StandOff().Fly(in frame);

            Assert.IsFalse(step.engages);
        }

        [Test]
        public void BreakOff_CallsFleeOnce_AndDoesNotEngage()
        {
            var calls = 0;
            var frame = Frame(Fight(inTargetsCone: false));
            var step = new BreakOff((_, _, _) =>
            {
                calls++;
                return Vector2.left;
            }).Fly(in frame);

            Assert.AreEqual(1, calls);
            Assert.IsFalse(step.engages);
        }
    }
}
#endif
