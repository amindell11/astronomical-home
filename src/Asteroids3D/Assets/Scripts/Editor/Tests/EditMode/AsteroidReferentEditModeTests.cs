#if UNITY_EDITOR
using System;
using AI;
using Asteroids;
using Game;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the interim asteroid identity (component-ref + spawn epoch, #423) and the builder's rock-seat allocation: dedup by identity, seats 1–3 in binding order, loud failure past three distinct rocks, and referent 0 untouched for anchor-bound slots.</summary>
    [Category("AI")]
    public class AsteroidReferentEditModeTests
    {
        private static readonly Ships.ShipId AnchorId = new(7);

        private AsteroidController rockA;
        private AsteroidController rockB;
        private AsteroidController rockC;
        private AsteroidController rockD;

        [SetUp]
        public void SetUp()
        {
            rockA = TestRocks.Spawn(new Vector2(1f, 0f));
            rockB = TestRocks.Spawn(new Vector2(2f, 0f));
            rockC = TestRocks.Spawn(new Vector2(3f, 0f));
            rockD = TestRocks.Spawn(new Vector2(4f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var rock in new[] { rockA, rockB, rockC, rockD })
                if (rock)
                    UnityEngine.Object.DestroyImmediate(rock.gameObject);
        }

        [Test]
        public void Builder_SameRockInTwoSlots_SharesOneSeat()
        {
            NavObjective nav = NavObjective.Anchored(AnchorId)
                .Facing(AsteroidRef.Of(rockA), 0f, 1f)
                .Position(AsteroidRef.Of(rockA), 0f, 0f, 5f, 1f);

            Assert.That(nav.sentence.aim.referent, Is.EqualTo(1));
            Assert.That(nav.sentence.pos.referent, Is.EqualTo(1), "one rock, one seat — dedup is by identity");
            Assert.That(nav.rockSeat1.Equals(AsteroidRef.Of(rockA)));
            Assert.That(nav.rockSeat2.IsBound, Is.False);
        }

        [Test]
        public void Builder_DistinctRocks_ClaimSeatsInBindingOrder()
        {
            NavObjective nav = NavObjective.Anchored(AnchorId)
                .Facing(AsteroidRef.Of(rockA), 0f, 1f)
                .Position(AsteroidRef.Of(rockB), 0f, 0f, 5f, 1f)
                .Velocity(AsteroidRef.Of(rockC), 0f, 4f, 1f);

            Assert.That(nav.sentence.aim.referent, Is.EqualTo(1));
            Assert.That(nav.sentence.pos.referent, Is.EqualTo(2));
            Assert.That(nav.sentence.vel.referent, Is.EqualTo(3));
            Assert.That(nav.rockSeat3.Equals(AsteroidRef.Of(rockC)));
        }

        [Test]
        public void Builder_AnchorSlotsStayOnReferentZero()
        {
            NavObjective nav = NavObjective.Anchored(AnchorId)
                .Facing(0f, 1f)
                .Position(AsteroidRef.Of(rockA), 0f, 0f, 5f, 1f);

            Assert.That(nav.sentence.aim.referent, Is.EqualTo(0), "the anchor path is the equivalence pin");
            Assert.That(nav.sentence.pos.referent, Is.EqualTo(1));
        }

        [Test]
        public void Builder_FourthDistinctRock_Throws()
        {
            var builder = NavObjective.Anchored(AnchorId)
                .Facing(AsteroidRef.Of(rockA), 0f, 1f)
                .Position(AsteroidRef.Of(rockB), 0f, 0f, 5f, 1f)
                .Velocity(AsteroidRef.Of(rockC), 0f, 4f, 1f);

            Assert.Throws<InvalidOperationException>(() => builder.Facing(AsteroidRef.Of(rockD), 0f, 1f),
                "re-binding never frees a seat, so the misuse fails at authoring, not silently downstream");
        }

        [Test]
        public void Builder_UnboundRef_Throws()
        {
            Assert.Throws<ArgumentException>(() => NavObjective.Anchored(AnchorId).Facing(default, 0f, 1f));
        }

        [Test]
        public void AsteroidRef_Resolves_ToThePlanePosition()
        {
            Assert.That(AsteroidRef.Of(rockA).TryResolve(out var pos, out _));
            Assert.That(Vector2.Distance(pos, new Vector2(1f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AsteroidRef_DespawnedOrReused_StopsResolving()
        {
            var held = AsteroidRef.Of(rockA);

            rockA.gameObject.SetActive(false);
            Assert.That(held.IsLive, Is.False, "pool release deactivates: the ref must go dead with it");

            rockA.gameObject.SetActive(true);
            TestRocks.BumpEpoch(rockA); // the pool handed the component to a new rock
            Assert.That(held.IsLive, Is.False, "same component, new spawn — a stale ref must not resolve to it");
            Assert.That(held.TryResolve(out _, out _), Is.False);
            Assert.That(AsteroidRef.Of(rockA).IsLive, "the new spawn's own ref is live");
        }
    }
}
#endif
