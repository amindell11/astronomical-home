#if UNITY_EDITOR
using System.Collections;
using Movement.MPC.Field;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Track B3: NavFieldService rebuild/staleness policy — async double-buffered solves
    /// become available within frames, follow the target when it moves more than a cell,
    /// and never block the caller (TryGetData is non-blocking; hook contributes 0 until
    /// the first bake lands).
    /// </summary>
    [TestFixture]
    [Category("Planning")]
    public class NavFieldServicePlayModeTests : PlayModeWorldFixture
    {
        private Transform target;
        private NavFieldService service;

        public override void SetUp()
        {
            base.SetUp();
            target = new GameObject("ChaseTarget").transform;
            service = NavFieldService.Instance;
        }

        public override void TearDown()
        {
            if (service) Object.DestroyImmediate(service.gameObject);
            if (target) Object.DestroyImmediate(target.gameObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TryGetData_NonBlocking_BecomesValidWithinFrames()
        {
            var goal = new float2(10f, 10f);

            // First call kicks the bake; may or may not have data yet, but must not throw.
            service.TryGetData(target, goal, 10f, out _);

            var valid = false;
            TerminalFieldData data = default;
            for (var i = 0; i < 30 && !valid; i++)
            {
                yield return null;
                valid = service.TryGetData(target, goal, 10f, out data);
            }

            Assert.IsTrue(valid, "Field must become available within a few frames");
            Assert.AreEqual(1, data.isValid);
            var atGoal = TerminalFieldData.Sample(goal, data);
            var far = TerminalFieldData.Sample(goal + new float2(30f, 0f), data);
            Assert.Less(atGoal, far, "Cost-to-go must increase away from the target");
        }

        [UnityTest]
        public IEnumerator Rebuild_FollowsTargetWhenItMovesBeyondACell()
        {
            var goalA = new float2(0f, 0f);
            service.TryGetData(target, goalA, 10f, out _);
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                if (service.TryGetData(target, goalA, 10f, out _)) break;
            }
            Assert.IsTrue(service.TryGetData(target, goalA, 10f, out var dataA));

            // Move the goal far past one cell; keep querying until the solved goal follows.
            var goalB = new float2(40f, 0f);
            var followed = false;
            TerminalFieldData dataB = default;
            // Fixed-update waits advance game time deterministically past the min rebuild
            // interval regardless of how fast batch-mode renders frames.
            for (var i = 0; i < 300 && !followed; i++)
            {
                service.TryGetData(target, goalB, 10f, out dataB);
                yield return new WaitForFixedUpdate();
                followed = service.TryGetData(target, goalB, 10f, out dataB)
                           && math.distance(dataB.goal, goalB) < 1f;
            }

            Assert.IsTrue(followed, "Field must re-solve from the target's new position");
            Assert.Greater(math.distance(dataA.goal, dataB.goal), 30f);
        }
    }
}
#endif
