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
    /// across reconstructed sessions and across an episode reset (<see cref="UnitService.Clear"/>) —
    /// the RL/self-play replay guarantee — and distinct per agent.
    /// </summary>
    [TestFixture]
    [Category("AI")]
    public class AgentDecisionSeedPlayModeTests : PlayModeWorldFixture
    {
        // Spawns commander-less ships (no AI init needed to read the assigned seed) into the given
        // service and returns their decision seeds in spawn order.
        private static int[] SpawnDecisionSeeds(UnitService units, Ship template, int count, List<Ship> spawned)
        {
            var seeds = new int[count];
            for (var i = 0; i < count; i++)
            {
                var ship = units.SpawnShip(template, null, team: 0,
                    new Vector3(i * 100f, 0f, 0f), Quaternion.identity);
                spawned.Add(ship);
                seeds[i] = ship.DecisionSeed;
            }
            return seeds;
        }

        private static void WithService(Action<UnitService, Ship, List<Ship>> body)
        {
            var host = new GameObject("TestUnitService");
            var units = host.AddComponent<UnitService>();
            units.SetArena(Tests.Common.TestArena.On(host, units.Registry));
            var template = TestAssets.LoadShip2Prefab();
            Assert.IsNotNull(template, "Ship_2 prefab failed to load");

            var spawned = new List<Ship>();
            try
            {
                body(units, template, spawned);
            }
            finally
            {
                foreach (var ship in spawned)
                    if (ship) UnityEngine.Object.DestroyImmediate(ship.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SpawnOrder_ReproducesDecisionSeeds_AcrossReconstructedSessions()
        {
            int[] first = null, second = null;
            WithService((u, t, s) => first = SpawnDecisionSeeds(u, t, 4, s));
            WithService((u, t, s) => second = SpawnDecisionSeeds(u, t, 4, s));

            Assert.AreEqual(first, second,
                "A fresh session with the same spawn order must derive identical decision seeds.");
        }

        [Test]
        public void EpisodeReset_ViaClear_ReproducesDecisionSeeds()
        {
            WithService((units, template, spawned) =>
            {
                var firstEpisode = SpawnDecisionSeeds(units, template, 4, spawned);
                units.Clear();
                var secondEpisode = SpawnDecisionSeeds(units, template, 4, spawned);

                Assert.AreEqual(firstEpisode, secondEpisode,
                    "Clear() is the episode-reset boundary — the next episode must re-derive the same seeds.");
            });
        }

        [Test]
        public void SpawnedAgents_HaveDistinctNonzeroDecisionSeeds()
        {
            int[] seeds = null;
            WithService((u, t, s) => seeds = SpawnDecisionSeeds(u, t, 5, s));

            CollectionAssert.AllItemsAreUnique(seeds,
                "Each spawned agent must get a distinct decision seed.");
            Assert.IsFalse(Array.Exists(seeds, s => s == 0),
                "Decision seeds must be nonzero (a zero seed is illegal for the MPC sampler RNG).");
        }
    }
}
#endif
