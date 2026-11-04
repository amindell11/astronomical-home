#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AI;
using NUnit.Framework;
using RL.Opponents.Beats;
using Ships;
using Ships.Registry;
using Substrate;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Enemies on the beat brain around one target: in a crowd of six some always hang back and the ones holding at range still take their long shots, and an unaware ship beside an engaged ally takes no target from it and never fires.</summary>
    [Category("AI")]
    public class BeatBrainPlayModeTests : PlayModeWorldFixture
    {
        private const int Enemies = 6;
        private const float RingRadius = 40f;
        private const float AcquireSeconds = 20f;
        private const float FightSeconds = 20f;

        private GameObject unitsHost;
        private UnitService units;
        private Ship template;
        private AICommander pilot;

        protected override bool AccelerateTime => true;

        public override void SetUp()
        {
            base.SetUp();
            unitsHost = new GameObject("UnitService");
            units = unitsHost.AddComponent<UnitService>();
            ShipServices.Compose(units, unitsHost.transform, presentationEnabled: false);
            template = TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            pilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/EnemyPilot.prefab");
            Random.InitState(1349);
        }

        public override void TearDown()
        {
            if (units) units.Clear();
            DestroyTestObject(unitsHost);
            base.TearDown();
        }

        private Ship Target()
        {
            var target = units.SpawnShip(template, null, team: 0, Vector3.zero, Quaternion.identity, Field);
            target.Damage.SetInvulnerability(1e6f);
            return target;
        }

        // Faces the origin, where the target sits.
        private Ship Enemy(Vector2 at) =>
            units.SpawnShip(template, pilot, team: 1, GamePlane.PlanePointToWorld(at),
                GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(-at.normalized)), Field);

        private static BeatBrain Brain(Ship ship) => (BeatBrain)((AICommander)ship.Commander).Brain;

        // The scouts scan once a frame, and an accelerated frame can span many fixed steps.
        private static IEnumerator UntilTargeted(Ship target, params BeatBrain[] brains)
        {
            for (var wait = 0; brains.Any(brain => brain.TargetId != target.Id); wait++)
            {
                Assert.Less(wait * Time.fixedDeltaTime, AcquireSeconds, "every aware enemy picks a beat on the target");
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator CrowdedFight_SomeEnemiesAlwaysHangBack_AndHoldersStillFire()
        {
            var target = Target();
            var brains = new BeatBrain[Enemies];
            var holdShots = 0;
            for (var i = 0; i < Enemies; i++)
            {
                var angle = 2f * Mathf.PI * i / Enemies;
                var ship = Enemy(RingRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                var brain = Brain(ship);
                brains[i] = brain;
                ship.Weapons.Fired += () =>
                {
                    if (brain.Beat == Beat.Hold) holdShots++;
                };
            }
            yield return UntilTargeted(target, brains);

            var steps = 0;
            var attackingSteps = 0;
            var mostAttacking = 0;
            while (steps * Time.fixedDeltaTime < FightSeconds)
            {
                yield return new WaitForFixedUpdate();
                steps++;
                var attacking = brains.Count(brain => brain.Attacking && brain.TargetId == target.Id);
                attackingSteps += attacking;
                mostAttacking = Mathf.Max(mostAttacking, attacking);
            }

            Assert.Less(mostAttacking, Enemies, "at no moment did every enemy attack at once");
            var meanAttacking = (float)attackingSteps / steps;
            Assert.Less(meanAttacking, Enemies / 2f,
                $"on average {meanAttacking:F1} of {Enemies} enemies attacked; most at once {mostAttacking}");
            Assert.Greater(holdShots, 0, "enemies holding at range never fired");
        }

        [UnityTest]
        public IEnumerator UnawareShip_BesideAnEngagedAlly_TakesNoTargetAndNeverFires()
        {
            var target = Target();
            var ally = Brain(Enemy(new Vector2(12f, 0f)));
            var unawareShip = Enemy(new Vector2(0f, 12f));
            var unawareCommander = (AICommander)unawareShip.Commander;
            unawareCommander.Scout.Unaware = true;
            var unaware = Brain(unawareShip);
            var shots = 0;
            unawareShip.Weapons.Fired += () => shots++;

            yield return UntilTargeted(target, ally);
            for (var step = 0; step * Time.fixedDeltaTime < FightSeconds / 4f; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(ShipId.Invalid, unaware.TargetId, $"took a target at step {step}");
                Assert.AreEqual(Beat.Search, unaware.Beat, $"left its search at step {step}");
            }

            Assert.IsTrue(ally.Attacking, "test premise: the ally is attacking the target");
            Assert.AreEqual(0, shots, "the unaware ship fired");
            var decision = unaware.Decide(unawareCommander.context);
            Assert.IsTrue(decision.HasValue, "an unaware ship still decides to search");
            Assert.IsFalse(decision.Value.engagePrimary || decision.Value.engageSecondary, "an unaware ship engaged");
        }
    }
}
#endif
