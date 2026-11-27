#if UNITY_EDITOR
using AI.Pilots;
using AI.Plays;
using Movement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Plays
{
    [Category("AI")]
    public class ShieldWallEditModeTests
    {
        private ShieldWall wall;

        [SetUp]
        public void SetUp() => wall = ScriptableObject.CreateInstance<ShieldWall>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(wall);

        [Test]
        public void FlatWall_PlacesRankTwo_AlongTheMirroredPerpendicular()
        {
            var rivalPos = new Vector2(4f, -2f);
            var targetPos = rivalPos + new Vector2(30f, 0f);
            var rival = Pose(rivalPos);
            var view = new PlayView(rival, Pose(targetPos), PlayStage.Imposing, 0f, 0f, -1, 4, PlayRead.None, 0f);

            var order = wall.MinionOrder(view, rival, rank: 2, speed: 100f);

            var axis = Vector2.Perpendicular((targetPos - rivalPos).normalized) * -1f;
            Assert.AreEqual(axis * 2f * wall.spacing, order.velocity);
        }

        [Test]
        public void Read_IsNoneOutOfRange_AAtTheFreeEnd_BAtTheHinge()
        {
            wall.spacing = 16f;
            wall.swingRate = 90f;
            wall.maxSwing = 90f;
            wall.readRange = 14f;
            var rival = Pose(Vector2.zero);

            var unseen = View(rival, Pose(new Vector2(40f, 0f)), PlayStage.Imposing, 0f, minions: 2);
            Assert.AreEqual(PlayRead.None, wall.Read(unseen));

            var free = View(rival, Pose(new Vector2(16f, 0f)), PlayStage.Imposing, imposed: 1f, minions: 1);
            Assert.AreEqual(PlayRead.A, wall.Read(free));

            var hinge = View(rival, Pose(new Vector2(3f, 0f)), PlayStage.Imposing, 0f, minions: 2);
            Assert.AreEqual(PlayRead.B, wall.Read(hinge));

            var empty = View(rival, Pose(Vector2.zero), PlayStage.Imposing, 0f, minions: 0);
            Assert.AreEqual(PlayRead.None, wall.Read(empty));
        }

        [Test]
        public void AnswerB_OrdersTheRivalTowardTheTarget_AndEngages()
        {
            var rival = Pose(Vector2.zero);
            var target = Pose(new Vector2(12f, 0f));
            const float speed = 8f;
            var answer = new PlayView(rival, target, PlayStage.AnswerB, 1f, 0f, 1, 2, PlayRead.B, 0f);

            var order = wall.RivalOrder(answer, speed);

            Assert.IsTrue(order.HasValue);
            Assert.AreEqual(RangerBrain.HoldRangeVelocity(rival, target, 0f, speed), order.Value.velocity);
            Assert.Greater(Vector2.Dot(order.Value.velocity, Vector2.right), 0f);
            Assert.IsTrue(order.Value.engages);
            var imposing = new PlayView(rival, target, PlayStage.Imposing, 0f, 0f, 1, 2, PlayRead.None, 0f);
            Assert.IsFalse(wall.RivalOrder(imposing, speed).HasValue);
        }

        [Test]
        public void Ripple_LetsOneRankEngageAtATime_WhileImposing()
        {
            wall.rippleSlot = 1f;
            Assert.AreEqual(1, EngagingRank(0f, 3));
            Assert.AreEqual(2, EngagingRank(1f, 3));
            Assert.AreEqual(3, EngagingRank(2f, 3));
        }

        private int EngagingRank(float imposed, int minions)
        {
            var view = new PlayView(Pose(Vector2.zero), Pose(Vector2.right), PlayStage.Imposing, 0f, imposed, 1,
                minions, PlayRead.None, 0f);
            var engaging = 0;
            var rankEngaged = 0;
            for (var rank = 1; rank <= minions; rank++)
            {
                if (!wall.MinionOrder(view, Pose(Vector2.zero), rank, 10f).engages) continue;
                engaging++;
                rankEngaged = rank;
            }

            Assert.AreEqual(1, engaging);
            return rankEngaged;
        }

        private static Kinematics Pose(Vector2 pos) => new(pos, Vector2.zero, 0f, 0f, 0f);

        private static PlayView View(Kinematics rival, Kinematics target, PlayStage stage, float imposed, int minions) =>
            new(rival, target, stage, 0f, imposed, 1, minions, PlayRead.None, 0f);
    }
}
#endif
