#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode
{
    /// <summary>
    /// The production decision-seed source: <see cref="UnitService"/> derives a per-agent seed from
    /// the deterministic spawn order, not object identity. Seeds must therefore be reproducible
    /// across reconstructed sessions (the RL/self-play replay guarantee) and distinct per agent.
    /// </summary>
    [TestFixture]
    [Category("AI")]
    public class AgentDecisionSeedPlayModeTests : PlayModeWorldFixture
    {
        // Spawns commander-less ships (no AI init needed to read the assigned seed) through one
        // fresh UnitService and returns their decision seeds in spawn order.
        private static int[] SpawnDecisionSeeds(int count)
        {
            var host = new GameObject("TestUnitService");
            var units = host.AddComponent<UnitService>();
            var template = TestAssets.LoadShip2Prefab();
            Assert.IsNotNull(template, "Ship_2 prefab failed to load");

            var spawned = new List<Ship>(count);
            var seeds = new int[count];
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var ship = units.SpawnShip(template, null, team: 0,
                        new Vector3(i * 100f, 0f, 0f), Quaternion.identity);
                    spawned.Add(ship);
                    seeds[i] = ship.DecisionSeed;
                }
            }
            finally
            {
                foreach (var ship in spawned)
                    if (ship) UnityEngine.Object.DestroyImmediate(ship.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
            return seeds;
        }

        [Test]
        public void SpawnOrder_ReproducesDecisionSeeds_AcrossReconstructedSessions()
        {
            var first = SpawnDecisionSeeds(4);
            var second = SpawnDecisionSeeds(4);

            Assert.AreEqual(first, second,
                "A fresh session with the same spawn order must derive identical decision seeds.");
        }

        [Test]
        public void SpawnedAgents_HaveDistinctNonzeroDecisionSeeds()
        {
            var seeds = SpawnDecisionSeeds(5);

            CollectionAssert.AllItemsAreUnique(seeds,
                "Each spawned agent must get a distinct decision seed.");
            Assert.IsFalse(Array.Exists(seeds, s => s == 0),
                "Decision seeds must be nonzero (a zero seed is illegal for the MPC sampler RNG).");
        }
    }
}
#endif
