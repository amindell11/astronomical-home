#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Scanning;
using Asteroids;
using Game.RLHarness;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;
using AI.Strategy;

namespace Tests.EditMode
{
    /// <summary>Pins the sticky rock-slot roster (Intent_Grammar §Stage C brief, fork 1): union-with-backfill membership, margin hysteresis, the bound-occupant no-evict rule with its despawn/range-exit exceptions, stable slot indices, and pooled reuse reading as a new identity.</summary>
    [Category("AI")]
    public class RockSlotRosterEditModeTests
    {
        private const float Margin = 2f;
        private static readonly Vector2 SelfPos = Vector2.zero;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go)
                    Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private AsteroidController Rock(float x, float y = 0f)
        {
            var rock = TestRocks.Spawn(new Vector2(x, y));
            spawned.Add(rock.gameObject);
            return rock;
        }

        private static ObstacleScan Scan(params AsteroidController[] rocks)
        {
            var buffer = new DetectedObstacle[rocks.Length];
            for (var i = 0; i < rocks.Length; i++)
                buffer[i] = new DetectedObstacle(rocks[i].transform.position, 2f, rocks[i].SimpleCollider,
                    default, 1f, rocks[i]);
            return new ObstacleScan(buffer, buffer.Length);
        }

        private static bool Rostered(RockSlotRoster roster, AsteroidController rock) =>
            SlotOf(roster, rock) >= 0;

        private static int SlotOf(RockSlotRoster roster, AsteroidController rock)
        {
            for (var s = 0; s < RockSlotRoster.SlotCount; s++)
                if (roster.TryGetSlot(s, out var occupant) && occupant.Equals(AsteroidRef.Of(rock)))
                    return s;
            return -1;
        }

        [Test]
        public void Roster_IsTheUnionOfBothSides()
        {
            var enemyPos = new Vector2(100f, 0f);
            var nearSelf = new[] { Rock(5f), Rock(6f), Rock(7f) };
            var extra = Rock(8f);
            var nearEnemy = new[] { Rock(95f), Rock(94f), Rock(93f) };

            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(nearSelf[0], nearSelf[1], nearSelf[2], extra,
                nearEnemy[0], nearEnemy[1], nearEnemy[2]), default);

            foreach (var rock in nearSelf) Assert.That(Rostered(roster, rock), "nearest-3-to-self belongs");
            foreach (var rock in nearEnemy) Assert.That(Rostered(roster, rock), "nearest-3-to-enemy belongs");
            Assert.That(Rostered(roster, extra), Is.False, "4th-from-self loses to the enemy side");
        }

        [Test]
        public void Roster_BackfillsBySelfDistance_WhenTheSidesOverlap()
        {
            // Enemy sits on the self-side cluster, so both sides pick A/B/C — backfill takes D/E/F, not G.
            var enemyPos = new Vector2(5f, 0f);
            var a = Rock(5f);
            var b = Rock(6f);
            var c = Rock(7f);
            var d = Rock(8f);
            var e = Rock(9f);
            var f = Rock(10f);
            var g = Rock(11f);

            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(a, b, c, d, e, f, g), default);

            foreach (var rock in new[] { a, b, c, d, e, f })
                Assert.That(Rostered(roster, rock), $"expected backfilled roster to hold the rock at {rock.transform.position}");
            Assert.That(Rostered(roster, g), Is.False);
        }

        // Hysteresis scenarios colocate the enemy with self so both sides pick the same rocks and
        // membership reduces to nearest-6-by-self-distance — a far enemy would make the far rocks
        // ITS nearest three and change which occupant is challengeable.
        [Test]
        public void Challenger_InsideTheMargin_DoesNotEvict()
        {
            var enemyPos = SelfPos;
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            var marginal = Rock(15.5f);
            var all = new List<AsteroidController>(occupants) { marginal };
            roster.Update(SelfPos, enemyPos, Scan(all.ToArray()), default);

            Assert.That(Rostered(roster, occupants[5]), "a 0.5 m better challenger is churn, not a takeover");
            Assert.That(Rostered(roster, marginal), Is.False);
        }

        [Test]
        public void Challenger_BeyondTheMargin_Evicts()
        {
            var enemyPos = SelfPos;
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            var strong = Rock(10f);
            var all = new List<AsteroidController>(occupants) { strong };
            roster.Update(SelfPos, enemyPos, Scan(all.ToArray()), default);

            Assert.That(Rostered(roster, strong), "a decisively closer rock takes the slot");
            Assert.That(Rostered(roster, occupants[5]), Is.False, "the worst unbound occupant pays");
        }

        [Test]
        public void BoundOccupant_IsNeverChallengedOut()
        {
            var enemyPos = SelfPos;
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            var strong = Rock(5f);
            var all = new List<AsteroidController>(occupants) { strong };
            var bound = new[] { AsteroidRef.Of(occupants[5]) };
            roster.Update(SelfPos, enemyPos, Scan(all.ToArray()), bound);

            Assert.That(Rostered(roster, occupants[5]), "the held sentence's rock keeps its slot");
            Assert.That(Rostered(roster, strong), Is.False, "no other occupant was challengeable");
        }

        [Test]
        public void BoundOccupant_DespawnStillEvicts()
        {
            var enemyPos = new Vector2(1000f, 0f);
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            var bound = new[] { AsteroidRef.Of(occupants[5]) };
            occupants[5].gameObject.SetActive(false);
            roster.Update(SelfPos, enemyPos,
                Scan(occupants[0], occupants[1], occupants[2], occupants[3], occupants[4]), bound);

            Assert.That(Rostered(roster, occupants[5]), Is.False, "binding protects against challengers, not death");
        }

        [Test]
        public void BoundOccupant_RangeExitStillEvicts()
        {
            var enemyPos = new Vector2(1000f, 0f);
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            // Still alive, but outside the scan box this tick.
            var bound = new[] { AsteroidRef.Of(occupants[5]) };
            roster.Update(SelfPos, enemyPos,
                Scan(occupants[0], occupants[1], occupants[2], occupants[3], occupants[4]), bound);

            Assert.That(Rostered(roster, occupants[5]), Is.False, "leaving the scan is the other legitimate exit");
        }

        [Test]
        public void SlotIndices_AreStableAcrossUpdates()
        {
            var enemyPos = new Vector2(1000f, 0f);
            var occupants = new[] { Rock(11f), Rock(12f), Rock(13f), Rock(14f), Rock(15f), Rock(16f) };
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            var before = SlotOf(roster, occupants[2]);
            roster.Update(SelfPos, enemyPos, Scan(occupants), default);

            Assert.That(SlotOf(roster, occupants[2]), Is.EqualTo(before),
                "the policy indexes slots — occupants must not shuffle while they stay rostered");
        }

        [Test]
        public void PooledReuse_ReadsAsANewIdentity()
        {
            var enemyPos = new Vector2(1000f, 0f);
            var rock = Rock(11f);
            var roster = new RockSlotRoster(Margin);
            roster.Update(SelfPos, enemyPos, Scan(rock), default);
            roster.TryGetSlot(SlotOf(roster, rock), out var staleRef);

            TestRocks.BumpEpoch(rock); // the pool handed the component to a new rock
            roster.Update(SelfPos, enemyPos, Scan(rock), default);

            Assert.That(Rostered(roster, rock), "the new spawn is admissible on its own merits");
            roster.TryGetSlot(SlotOf(roster, rock), out var freshRef);
            Assert.That(freshRef.Equals(staleRef), Is.False, "same component, different rock — identity must not carry over");
        }
    }
}
#endif
