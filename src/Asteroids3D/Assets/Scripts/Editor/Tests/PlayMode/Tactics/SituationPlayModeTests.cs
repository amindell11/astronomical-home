#if UNITY_EDITOR
using System;
using System.Collections;
using AI;
using AI.Tactics;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Tactics
{
    /// <summary>The situation read off real ships placed along one target's nose and beam. PlayMode because a ship's kinematics and weapons exist only after its Awake.</summary>
    [Category("AI")]
    public class SituationPlayModeTests : AIIntegrationFixture
    {
        private const int AcquireSteps = 200;

        private TacticsSettings settings;
        private Ship target;
        private Vector2 nose;
        private Vector2 beam;
        private float range;

        public override void SetUp()
        {
            base.SetUp();
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            settings.exposureCone = 60f;
            settings.laneWidth = 4f;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(settings);
            base.TearDown();
        }

        private IEnumerator SpawnTarget()
        {
            (target, _) = CreateAIShip(Vector3.zero, team: 0);
            yield return new WaitForFixedUpdate();
            nose = target.Kinematics.Forward;
            beam = new Vector2(nose.y, -nose.x);
            range = target.Weapons.Primary.FireRange;
            Assert.Greater(range, 0f, "test premise: the target's primary declares a gun range");
        }

        private (Ship ship, AICommander pilot) Enemy(float ahead, float aside) =>
            CreateAIShip(GamePlane.PlanePointToWorld(target.Kinematics.pos + nose * ahead + beam * aside), team: 1);

        private IEnumerator UntilTargeted(params AICommander[] enemies)
        {
            for (var step = 0; Array.Exists(enemies, enemy => enemy.context?.Combat.Enemy != target); step++)
            {
                Assert.Less(step, AcquireSteps, "every enemy acquires the target");
                yield return new WaitForFixedUpdate();
            }
        }

        private Situation Read(AICommander enemy) => Situation.Read(enemy.context, settings);

        [UnityTest]
        public IEnumerator Exposed_InsideTheTargetsGunRangeAndNoseCone_ClearOutsideEither()
        {
            yield return SpawnTarget();
            var offNose = 50f * Mathf.Deg2Rad;
            var (_, inGuns) = Enemy(0.6f * range, 0f);
            var (_, aside) = Enemy(0.6f * range * Mathf.Cos(offNose), 0.6f * range * Mathf.Sin(offNose));
            var (_, outOfRange) = Enemy(range + 4f, 0f);
            Assert.Less(range + 4f, outOfRange.Scout.nearbyShipRadius, "test premise: the far ship still sees the target");
            yield return UntilTargeted(inGuns, aside, outOfRange);

            Assert.IsTrue(Read(inGuns).exposed, "inside the gun range on the nose");
            Assert.IsFalse(Read(aside).exposed, "50° off a 60° cone's nose");
            Assert.IsFalse(Read(outOfRange).exposed, "past the target's gun range");
            Assert.IsTrue(Read(inGuns).inMyRange, "the same primary reaches back");
            Assert.IsFalse(Read(outOfRange).inMyRange);
            Assert.AreEqual(range + 4f, Read(outOfRange).distance, 0.5f);

            settings.exposureCone = 120f;
            Assert.IsTrue(Read(aside).exposed, "a 120° cone takes in a ship 50° off the nose");
        }

        [UnityTest]
        public IEnumerator Lane_CountsAlliesBetweenMeAndTheTarget_WithinTheLaneWidth()
        {
            yield return SpawnTarget();
            var (_, me) = Enemy(16f, 0f);
            var (_, inLane) = Enemy(8f, 1f);
            var (_, wide) = Enemy(8f, 6f);
            var (_, behindMe) = Enemy(24f, 0f);
            yield return UntilTargeted(me, inLane, wide, behindMe);

            Assert.AreEqual(1, Read(me).alliesInLane, "only the ally inside a 4-unit lane, between me and the target");
            settings.laneWidth = 14f;
            Assert.AreEqual(2, Read(me).alliesInLane, "a 14-unit lane takes in the ally 6 units off the line");
        }

        [UnityTest]
        public IEnumerator OpenSide_HasAClearLaneAndSitsOutOfTheNose_TheTailSideFirst()
        {
            yield return SpawnTarget();
            var (_, onNose) = Enemy(14f, 0f);
            var (_, swung) = Enemy(14f * Mathf.Cos(Mathf.PI / 4f), 14f * Mathf.Sin(Mathf.PI / 4f));
            var (_, blocker) = Enemy(0f, -7f);
            yield return UntilTargeted(onNose, swung, blocker);

            Assert.Greater(Vector2.Dot(Read(onNose).openSide, beam), 0.95f, "an ally blocks the other side's lane");
            var tailSide = (beam - nose).normalized;
            Assert.Greater(Vector2.Dot(Read(swung).openSide, tailSide), 0.95f,
                "both sides are clear and out of the nose, so the one toward the tail");

            settings.exposureCone = 200f;
            Assert.AreEqual(Vector2.zero, Read(onNose).openSide, "a side inside the nose cone is never open");
        }

        [UnityTest]
        public IEnumerator Myself_ReadsMyShieldAndHull_AndTheSecondsSinceIWasHit()
        {
            yield return SpawnTarget();
            var (ship, me) = Enemy(14f, 0f);
            yield return UntilTargeted(me);
            Assert.IsTrue(float.IsPositiveInfinity(Read(me).secondsSinceHit), "never hit yet");

            DealDamage(ship, 5f);
            yield return new WaitForFixedUpdate();
            var hit = Read(me);
            Assert.Less(hit.secondsSinceHit, 2f * Time.fixedDeltaTime);
            Assert.Less(hit.shieldPct, 1f, "test premise: the hit took shield");
            Assert.AreEqual(ship.ShieldPct, hit.shieldPct);
            Assert.AreEqual(ship.HealthPct, hit.hullPct);
        }
    }
}
#endif
