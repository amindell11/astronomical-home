#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.Strategy;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>Pins sight-limited tracking. An enemy in range with a clear line is kept. One beyond sight range, or behind an asteroid, is lost after the scout's lose-sight seconds, and an unseen contact is not acquired. Recall hands a live lost enemy back and forgets a dead one. An unlimited tracker keeps its enemy far past sight range. A decision whose recall lane is set hands the lost enemy back.</summary>
    [TestFixture]
    [Category("AI")]
    public class EnemyTrackerSightPlayModeTests : AIIntegrationFixture
    {
        private readonly List<GameObject> rocks = new();

        public override void TearDown()
        {
            foreach (var rock in rocks)
                if (rock) Object.DestroyImmediate(rock);
            rocks.Clear();
            base.TearDown();
        }

        [Test]
        public void SightLimited_KeepsAnEnemyInRangeWithAClearLine()
        {
            var (self, cmdr, enemy) = Pair(20f);
            Assert.AreEqual(70f, cmdr.Scout.sightRange);
            Assert.AreEqual(3f, cmdr.Scout.loseSightAfter);
            Assert.LessOrEqual(Gap(self, enemy), cmdr.Scout.sightRange);
            Assert.IsTrue(LineOfSight.IsClear(cmdr.Scout.Origin, enemy.transform.position,
                enemy.transform, cmdr.Scout.asteroidMask));

            cmdr.HuntBySight(enemy);
            cmdr.context.Update(cmdr.Scout.loseSightAfter + 2f);

            Assert.IsTrue(cmdr.context.Combat.SightLimited);
            Assert.AreSame(enemy, cmdr.context.Combat.Enemy);
            Assert.IsTrue(cmdr.context.Combat.HasEnemy);
        }

        [Test]
        public void SightLimited_LosesAnEnemyBeyondSightRange_AfterLoseSight()
        {
            var (self, cmdr, enemy) = Pair(100f);
            var lose = cmdr.Scout.loseSightAfter;
            Assert.Greater(Gap(self, enemy), cmdr.Scout.sightRange);

            cmdr.HuntBySight(enemy);
            cmdr.context.Update(lose - 0.25f);
            Assert.AreSame(enemy, cmdr.context.Combat.Enemy, "inside the lose-sight window the enemy is still held");

            cmdr.context.Update(0.25f);
            Assert.IsNull(cmdr.context.Combat.Enemy, "past the lose-sight window a far enemy is lost");
            Assert.IsFalse(cmdr.context.Combat.HasEnemy);
        }

        [UnityTest]
        public IEnumerator SightLimited_LosesAnEnemyBehindAnAsteroid_AfterLoseSight()
        {
            var (self, cmdr, enemy) = Pair(20f);
            var rock = Block(self, enemy);
            yield return null;
            var lose = cmdr.Scout.loseSightAfter;
            Assert.LessOrEqual(Gap(self, enemy), cmdr.Scout.sightRange);
            Assert.AreNotEqual(0, cmdr.Scout.asteroidMask.value & (1 << rock.layer),
                "the scout's asteroid mask includes the rock");
            Assert.IsFalse(LineOfSight.IsClear(cmdr.Scout.Origin, enemy.transform.position,
                enemy.transform, cmdr.Scout.asteroidMask), "the rock blocks the line");

            cmdr.HuntBySight(enemy);
            cmdr.context.Update(lose - 0.25f);
            Assert.AreSame(enemy, cmdr.context.Combat.Enemy, "inside the lose-sight window the occluded enemy is still held");

            cmdr.context.Update(0.25f);
            Assert.IsNull(cmdr.context.Combat.Enemy, "past the lose-sight window an occluded enemy is lost");
        }

        [UnityTest]
        public IEnumerator SightLimited_DoesNotAcquireAnUnseenContact()
        {
            var (self, cmdr, enemy) = Pair(20f);
            Block(self, enemy);
            cmdr.HuntBySight(enemy);
            for (var i = 0; i < 5 && cmdr.Scout.Contacts.nearestEnemyId != enemy.Id; i++)
                yield return null;

            Assert.AreEqual(enemy.Id, cmdr.Scout.Contacts.nearestEnemyId, "the occluded ship is a contact");
            cmdr.context.Update(cmdr.Scout.loseSightAfter);
            Assert.IsNull(cmdr.context.Combat.Enemy);

            cmdr.context.Update(0.05f);
            Assert.AreEqual(enemy.Id, cmdr.Scout.Contacts.nearestEnemyId, "the unseen ship is still the contact");
            Assert.IsNull(cmdr.context.Combat.Enemy, "a sight-limited tracker does not acquire an unseen contact");
        }

        [Test]
        public void Recall_HandsBackALiveLostEnemy_AndForgetsADeadOne()
        {
            var (_, cmdr, enemy) = Pair(100f);
            var combat = cmdr.context.Combat;
            cmdr.HuntBySight(enemy);
            cmdr.context.Update(cmdr.Scout.loseSightAfter);
            Assert.IsNull(combat.Enemy);

            combat.Recall();
            Assert.AreSame(enemy, combat.Enemy, "Recall hands a live lost enemy back");
            Assert.IsTrue(combat.HasEnemy);

            cmdr.context.Update(cmdr.Scout.loseSightAfter);
            Assert.IsNull(combat.Enemy);
            Object.DestroyImmediate(enemy.gameObject);
            combat.Recall();
            Assert.IsNull(combat.Enemy, "Recall forgets a dead enemy");
        }

        [UnityTest]
        public IEnumerator Unlimited_KeepsAnEnemyFarBeyondSightRange()
        {
            var (self, cmdr, enemy) = Pair(12f);
            Assert.IsFalse(cmdr.context.Combat.SightLimited);

            for (var i = 0; i < 5 && !cmdr.Scout.Contacts.nearestEnemyId.IsValid; i++)
                yield return null;
            cmdr.context.Update(0.05f);
            Assert.AreSame(enemy, cmdr.context.Combat.Enemy, "the unlimited tracker acquires the contact");

            Move(enemy, new Vector3(200f, 0f, 0f));
            Assert.Greater(Gap(self, enemy), cmdr.Scout.sightRange);
            cmdr.context.Update(cmdr.Scout.loseSightAfter + 5f);

            Assert.AreSame(enemy, cmdr.context.Combat.Enemy, "an unlimited tracker keeps its enemy far beyond sight range");
            Assert.IsTrue(cmdr.context.Combat.HasEnemy);
        }

        [UnityTest]
        public IEnumerator RecallDecision_HandsTheLostEnemyBack()
        {
            var (_, cmdr, enemy) = Pair(100f);
            cmdr.HuntBySight(enemy);
            cmdr.context.Update(cmdr.Scout.loseSightAfter);
            Assert.IsNull(cmdr.context.Combat.Enemy);

            cmdr.InstallBrain<ScriptedBrain>().decision =
                new BrainDecision(NavObjective.Planar(Vector2.zero), recall: true);
            cmdr.enabled = true;
            yield return new WaitForFixedUpdate();

            Assert.AreSame(enemy, cmdr.context.Combat.Enemy, "routing a recall decision hands the lost enemy back");
        }

        private (Ship self, AICommander cmdr, Ship enemy) Pair(float planeX)
        {
            var (self, cmdr) = CreateAIShip(Vector3.zero, team: 0);
            var (enemy, enemyCommander) = CreateAIShip(new Vector3(planeX, 0f, 0f), team: 1);
            cmdr.enabled = false;
            enemyCommander.enabled = false;
            Freeze(self);
            Freeze(enemy);
            return (self, cmdr, enemy);
        }

        private GameObject Block(Ship from, Ship to)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rocks.Add(rock);
            rock.layer = LayerMask.NameToLayer("Asteroid");
            rock.transform.position = (from.transform.position + to.transform.position) * 0.5f;
            rock.transform.localScale = new Vector3(8f, 8f, 8f);
            Physics.SyncTransforms();
            return rock;
        }

        private static void Freeze(Ship ship)
        {
            ship.Body.isKinematic = true;
            ship.Body.linearVelocity = Vector3.zero;
            ship.Body.angularVelocity = Vector3.zero;
        }

        private static void Move(Ship ship, Vector3 position)
        {
            ship.transform.position = position;
            ship.Body.position = position;
            Physics.SyncTransforms();
        }

        private static float Gap(Ship a, Ship b) => Vector2.Distance(
            GamePlane.WorldPointToPlane(a.transform.position),
            GamePlane.WorldPointToPlane(b.transform.position));
    }
}
#endif
