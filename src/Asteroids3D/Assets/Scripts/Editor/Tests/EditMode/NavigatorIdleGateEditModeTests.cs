#if UNITY_EDITOR
using Movement;
using Movement.MPC;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Guards <see cref="Navigator.ShouldIdle"/> dispatching on the objective: waypoint/patrol idle once stopped at the destination, but the continuous MaintainRange/Flee objectives keep running while their target exists. ShouldIdle reads only the control surface, so no solver/context wiring is needed.</summary>
    [Category("MPC")]
    public class NavigatorIdleGateEditModeTests
    {
        private Navigator nav;

        [SetUp]
        public void SetUp() => nav = new GameObject("NavigatorIdleGate").AddComponent<Navigator>();

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
        }

        private static Kinematics AtRest(Vector2 pos) => new(pos, Vector2.zero, 0f, 0f, 0f);

        [Test]
        public void MaintainRange_NearAndStopped_DoesNotIdle()
        {
            var enemy = new Vector2(10f, 10f);
            nav.SetNavigationPoint(enemy);
            nav.SetGoalMaintainRange(20f, 5f);

            Assert.That(nav.ShouldIdle(AtRest(enemy)), Is.False,
                "MaintainRange is a continuous hold objective — sitting on top of the enemy and " +
                "stopped must NOT idle (the MPC still needs to open the range band).");
        }

        [Test]
        public void Waypoint_NearAndStopped_Idles()
        {
            var goal = new Vector2(10f, 10f);
            nav.SetNavigationPoint(goal);
            nav.ClearGoalMode(); // Waypoint

            Assert.That(nav.ShouldIdle(AtRest(goal)), Is.True,
                "A reached-and-stopped waypoint should idle — arrival semantics are correct here.");
        }

        [Test]
        public void Waypoint_FarFromGoal_DoesNotIdle()
        {
            nav.SetNavigationPoint(new Vector2(100f, 0f));
            nav.ClearGoalMode();

            Assert.That(nav.ShouldIdle(AtRest(Vector2.zero)), Is.False,
                "A distant waypoint must keep the MPC running.");
        }

        [Test]
        public void NoValidWaypoint_Idles()
        {
            // Fresh navigator: no destination in any mode → idle.
            nav.SetGoalMaintainRange(20f, 5f);
            Assert.That(nav.ShouldIdle(AtRest(Vector2.zero)), Is.True,
                "With no valid waypoint there is nothing to act on, even in a continuous mode.");
        }
    }
}
#endif
