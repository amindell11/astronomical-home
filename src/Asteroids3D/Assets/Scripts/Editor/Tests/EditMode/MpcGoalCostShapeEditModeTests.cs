#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI.States;
using Combat.Weapons;
using Movement.MPC;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the shape of the MPC goal costs against their stated intent — a range-hold objective's argmin must be its band (never contact) and flee must strictly prefer distance — sweeping every shipped StateProfile so a profile that contradicts its own goal fails loudly.</summary>
    [Category("MPC")]
    public class MpcGoalCostShapeEditModeTests
    {
        private const string ProfilesDir = "Assets/Settings/AI/StateProfiles";
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const float GridMax = 80f;
        private const float GridStep = 0.25f;

        private static readonly float2 Goal = float2.zero;

        private static float BandCostAt(float dist, float desiredRange, float tolerance) =>
            Cost.RangeBandCost(new float2(dist, 0f), Goal, desiredRange, tolerance);

        // Fixed representative shapes incl. degenerate corners (tol 0); the asset sweep below covers whatever actually ships.
        private static readonly (float range, float tol)[] BandShapes =
        {
            (10f, 5f), (15f, 5f), (15f, 2f), (6f, 3f), (15.5f, 9.5f), (20f, 0f),
        };

        [Test]
        public void RangeBand_ZeroExactlyAcrossTheBand()
        {
            foreach (var (range, tol) in BandShapes)
            {
                foreach (var dist in new[] { range - tol, range, range + tol })
                    Assert.AreEqual(0f, BandCostAt(dist, range, tol),
                        $"Band [{range - tol}, {range + tol}] must be free at dist {dist}");
            }
        }

        [Test]
        public void RangeBand_NeverNegative_AnywhereOnTheApproach()
        {
            foreach (var (range, tol) in BandShapes)
                for (var dist = 0f; dist <= GridMax; dist += GridStep)
                    Assert.GreaterOrEqual(BandCostAt(dist, range, tol), 0f,
                        $"Range-hold must never REWARD leaving the band (dist {dist}, band {range}±{tol}) — " +
                        "a negative branch here makes every MaintainRange state dive to hull contact");
        }

        [Test]
        public void RangeBand_CostRisesMonotonicallyAwayFromTheBand()
        {
            foreach (var (range, tol) in BandShapes)
            {
                var inner = range - tol;
                var outer = range + tol;

                var prev = 0f;
                for (var dist = inner; dist >= 0f; dist -= GridStep)
                {
                    var cost = BandCostAt(dist, range, tol);
                    Assert.GreaterOrEqual(cost, prev,
                        $"Cost must not relax while closing inside the band (dist {dist}, band {range}±{tol})");
                    prev = cost;
                }

                prev = 0f;
                for (var dist = outer; dist <= GridMax; dist += GridStep)
                {
                    var cost = BandCostAt(dist, range, tol);
                    Assert.GreaterOrEqual(cost, prev,
                        $"Cost must not relax while drifting away (dist {dist}, band {range}±{tol})");
                    prev = cost;
                }
            }
        }

        [Test]
        public void RangeBand_ContactCostsAsMuchAsBeingLostAtRange()
        {
            foreach (var (range, tol) in BandShapes)
            {
                if (range - tol <= 0f) continue;
                var atContact = BandCostAt(0f, range, tol);
                var farOut = BandCostAt(range + tol + (range - tol), range, tol);
                Assert.AreEqual(farOut, atContact, 1e-4f,
                    $"Same error distance must cost the same on both sides of band {range}±{tol}");
            }
        }

        [Test]
        public void Flee_StrictlyPrefersDistance()
        {
            var prev = float.MaxValue;
            for (var dist = 0f; dist <= GridMax; dist += GridStep)
            {
                var cost = Cost.FleeCost(new float2(dist, 0f), Goal, 2f);
                Assert.Less(cost, prev, $"Flee cost must strictly decrease with distance (dist {dist})");
                prev = cost;
            }
        }

        private static List<(StateProfile profile, TrackEnemyGoal goal)> LoadRangeHoldProfiles()
        {
            var found = AssetDatabase.FindAssets("t:StateProfile", new[] { ProfilesDir })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StateProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null)
                .Select(p => (profile: p, goal: p.goal as TrackEnemyGoal))
                .Where(t => t.goal != null)
                .ToList();
            Assert.IsNotEmpty(found, $"No TrackEnemyGoal profiles under {ProfilesDir} — path or type drift?");
            return found;
        }

        [Test]
        public void ShippedProfiles_BandIsAboveHullContact()
        {
            foreach (var (profile, goal) in LoadRangeHoldProfiles())
                Assert.Greater(goal.desiredRange - goal.rangeTolerance, 0f,
                    $"{profile.name}: tolerance {goal.rangeTolerance} swallows desiredRange {goal.desiredRange} — " +
                    "the band reaches dist 0, so nothing penalizes sitting on the target");
        }

        [Test]
        public void ShippedProfiles_GoalCostArgminIsTheBand()
        {
            foreach (var (profile, goal) in LoadRangeHoldProfiles())
            {
                var bestDist = -1f;
                var bestCost = float.MaxValue;
                for (var dist = 0f; dist <= GridMax; dist += GridStep)
                {
                    var cost = BandCostAt(dist, goal.desiredRange, goal.rangeTolerance);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestDist = dist;
                    }
                }

                var inner = goal.desiredRange - goal.rangeTolerance;
                var outer = goal.desiredRange + goal.rangeTolerance;
                Assert.That(bestDist, Is.InRange(inner, outer),
                    $"{profile.name}: the optimizer's preferred distance {bestDist} sits outside the profile's " +
                    $"own band [{inner}, {outer}] — the objective contradicts the state's intent");
                Assert.Greater(BandCostAt(0f, goal.desiredRange, goal.rangeTolerance), 0f,
                    $"{profile.name}: hull contact must cost more than holding the band");
            }
        }

        [Test]
        public void ShippedFiringProfiles_HoldInsideTheLaserEnvelope()
        {
            var lasers = AssetDatabase.LoadAssetAtPath<GameObject>(LasersPrefabPath);
            Assert.IsNotNull(lasers, $"Missing {LasersPrefabPath}");
            var fireDistance = new SerializedObject(lasers.GetComponent<Lasers>())
                .FindProperty("fireDistance").floatValue;
            Assert.Greater(fireDistance, 0f);

            foreach (var (profile, goal) in LoadRangeHoldProfiles())
            {
                if (!profile.enableFiring) continue;
                Assert.LessOrEqual(goal.desiredRange, fireDistance,
                    $"{profile.name}: holds at {goal.desiredRange} but the default laser only opens fire " +
                    $"inside {fireDistance} — the state parks itself where it cannot shoot");
            }
        }
    }
}
#endif
