#if UNITY_EDITOR
using AI.States;
using Movement.MPC;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the boost command seam: a VelocityReference intent's boost ORs into the drive command over the solver's own boost, other modes and resets never leak it. ApplyIntent/ApplyControl read only the control surface, so no solver wiring is needed.</summary>
    [Category("MPC")]
    public class NavigatorBoostPassThroughEditModeTests
    {
        private Navigator nav;

        [SetUp]
        public void SetUp() => nav = new GameObject("NavigatorBoost").AddComponent<Navigator>();

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
        }

        private static NavigationIntent VelocityIntent(bool boost) => new()
        {
            isValid = true,
            goalMode = GoalMode.VelocityReference,
            velocityReference = new Vector2(3f, 0f),
            boost = boost,
        };

        [Test]
        public void VelocityReference_BoostCommanded_OrsIntoCommand()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void VelocityReference_NoBoost_SolverBoostPassesThrough()
        {
            nav.ApplyIntent(VelocityIntent(boost: false));
            nav.ApplyControl(new MpcResult { boost = 1f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void PositionGoalMode_BoostFlag_IsIgnored()
        {
            var intent = new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.MaintainRange,
                desiredRange = 20f,
                rangeTolerance = 5f,
                boost = true,
            };
            nav.ApplyIntent(intent);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }

        [Test]
        public void InvalidIntent_ClearsCommandedBoost()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyIntent(NavigationIntent.None);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }

        [Test]
        public void NextIntentWithoutBoost_DropsThePriorCommand()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyIntent(VelocityIntent(boost: false));
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }
    }
}
#endif
