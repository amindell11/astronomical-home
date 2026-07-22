#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using AI;
using Game;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>M=2 fan-out leak checks for the in-process multi-arena harness: per-arena provider isolation (each arena's ships sense their own field handle), seed decorrelation (arena-1 episode-0 poses differ from arena-0's), spatial confinement (ships never cross into the neighbor's half of the line), and liveness (both async loops complete episodes). Never mirror-determinism — twin-arena identical evolution is a postmortem-rejected gate.</summary>
    [TestFixture]
    [Category("AI")]
    public class RLMultiArenaPlayModeTests
    {
        private const int EpisodesPerArena = 2;

        private GameObject hostA;
        private GameObject hostB;
        private ArenaContext arenaA;
        private ArenaContext arenaB;
        private HarnessField fieldA;
        private HarnessField fieldB;
        private EpisodePair pairA;
        private EpisodePair pairB;
        private ShipAgent agentA;
        private ShipAgent agentB;
        private EpisodeLoopDriver driverA;
        private EpisodeLoopDriver driverB;
        private ProjectileService projectilesA;
        private ProjectileService projectilesB;
        private HarnessAssets assets;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            Assert.AreEqual(0, Object.FindObjectsByType<Ship>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked ships — fix its teardown");

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.maximumDeltaTime = 1f;
            PacingContract.Apply();

            // An InferenceChooser test earlier in the suite leaves auto-stepping off.
            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            projectilesA?.ReturnAllToPool();
            projectilesB?.ReturnAllToPool();
            projectilesA = null;
            projectilesB = null;
            if (agentA) Object.DestroyImmediate(agentA.gameObject);
            if (agentB) Object.DestroyImmediate(agentB.gameObject);
            agentA = null;
            agentB = null;
            pairA?.Dispose();
            pairB?.Dispose();
            pairA = null;
            pairB = null;
            fieldA?.Dispose();
            fieldB?.Dispose();
            fieldA = null;
            fieldB = null;
            if (hostA) Object.DestroyImmediate(hostA);
            if (hostB) Object.DestroyImmediate(hostB);
            arenaA = null;
            arenaB = null;

            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TwoArenas_IsolatedDecorrelatedConfined_BothComplete()
        {
            var specA = TightSpec(TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 0));
            var specB = TightSpec(TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 1));

            (hostA, arenaA, fieldA, pairA, agentA, driverA, projectilesA) = ComposeArena("Arena0", TrainingHost.ArenaOffset(0), in specA);
            (hostB, arenaB, fieldB, pairB, agentB, driverB, projectilesB) = ComposeArena("Arena1", TrainingHost.ArenaOffset(1), in specB);

            // (a) Provider isolation: each arena's ships were wired to their own arena handle and field instance.
            Assert.AreSame(fieldA.Field, arenaA.ObstacleField, "arena 0's handle must expose its own field");
            Assert.AreSame(fieldB.Field, arenaB.ObstacleField, "arena 1's handle must expose its own field");
            Assert.AreNotSame(arenaA.ObstacleField, arenaB.ObstacleField, "the two arenas must not share a field instance");
            foreach (var ship in new[] { pairA.Agent, pairA.Baseline })
                Assert.AreSame(arenaA, CommanderArena(ship), $"{ship.name} in arena 0 must be wired to arena 0's context");
            foreach (var ship in new[] { pairB.Agent, pairB.Baseline })
                Assert.AreSame(arenaB, CommanderArena(ship), $"{ship.name} in arena 1 must be wired to arena 1's context");

            // (b) Decorrelation: the arena-1 seed derives different episode-0 poses than arena 0's.
            var relA = GamePlane.WorldPointToPlane(pairA.Agent.transform.position) - arenaA.Offset;
            var relB = GamePlane.WorldPointToPlane(pairB.Agent.transform.position) - arenaB.Offset;
            Assert.Greater((relA - relB).magnitude, 1e-3f,
                "arena 1's episode-0 spawn pose must differ from arena 0's — identical poses mean the arena seed re-correlated");

            var completedA = new int[1];
            var completedB = new int[1];
            var loopA = ArenaEpisodes(driverA, specA, completedA);
            var loopB = ArenaEpisodes(driverB, specB, completedB);
            // Manual pump = two coroutines resumed on the same WaitForFixedUpdate, with a per-step assert hook.
            var aAlive = loopA.MoveNext();
            var bAlive = loopB.MoveNext();
            while (aAlive || bAlive)
            {
                yield return new WaitForFixedUpdate();
                // (c) Spatial confinement: no ship crosses into the neighbor arena's half of the line.
                AssertConfined(pairA, arenaA);
                AssertConfined(pairB, arenaB);
                if (aAlive) aAlive = loopA.MoveNext();
                if (bAlive) bAlive = loopB.MoveNext();
            }

            // (d) Liveness: both async per-arena loops ran their full episode budget.
            Assert.AreEqual(EpisodesPerArena, completedA[0], "arena 0 must complete its episodes");
            Assert.AreEqual(EpisodesPerArena, completedB[0], "arena 1 must complete its episodes");
        }

        private static RewardSpec TightSpec(int seed)
        {
            var spec = RewardSpec.Default;
            spec.runSeed = seed;
            spec.arenaRadius = 25f;
            spec.timeoutDecisions = 30;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;
            spec.useAsteroidField = true;
            return spec;
        }

        private (GameObject, ArenaContext, HarnessField, EpisodePair, ShipAgent, EpisodeLoopDriver, ProjectileService) ComposeArena(
            string name, Vector2 offset, in RewardSpec spec)
        {
            var host = new GameObject(name);
            var (units, arena, projectiles) = HarnessArena.Compose(host, offset);
            var field = HarnessField.Spawn(arena, assets, spec.fieldDensityScale, host.transform);
            var pair = EpisodePair.SpawnWithAgentChooser(units, arena, projectiles, in spec, assets, out var chooser);
            var agent = ShipAgentFactory.ComposeHeuristicOnly(pair, chooser, in spec, arena.Offset, host.transform);
            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset, field);
            return (host, arena, field, pair, agent, driver, projectiles);
        }

        private static IEnumerator ArenaEpisodes(EpisodeLoopDriver driver, RewardSpec spec, int[] completed)
        {
            for (var i = 0; i < EpisodesPerArena; i++)
            {
                var episode = driver.RunEpisode(spec, i);
                while (episode.MoveNext())
                    yield return episode.Current;
                completed[0]++;
            }
        }

        private static ArenaContext CommanderArena(Ship ship)
        {
            var commander = ship.GetComponentInChildren<AICommander>();
            var field = typeof(AICommander).GetField("arena", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ArenaContext)field.GetValue(commander);
        }

        private static void AssertConfined(EpisodePair pair, ArenaContext arena)
        {
            foreach (var ship in new[] { pair.Agent, pair.Baseline })
            {
                if (!ship) continue;
                var planeX = GamePlane.WorldPointToPlane(ship.transform.position).x;
                Assert.Less(Mathf.Abs(planeX - arena.Offset.x), TrainingHost.ArenaSpacing * 0.5f,
                    $"{ship.name} left its arena's half of the fan-out line — arena isolation leak");
            }
        }
    }
}
#endif
