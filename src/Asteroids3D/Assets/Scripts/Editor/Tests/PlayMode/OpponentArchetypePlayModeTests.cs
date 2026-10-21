#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate.Services;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Episodes;
using RL.Opponents;
using RL.Probes;
using RL.Reward;

namespace Tests.PlayMode
{
    /// <summary>Smoke for the per-archetype degeneracy gate: each scripted opponent archetype runs one episode against the deterministic <see cref="RangerBrain"/> stand-in on the agent side, and its <see cref="ArchetypeGateRow"/> must show the archetype's signature behaviour.</summary>
    [TestFixture]
    [Category("AI")]
    public class OpponentArchetypePlayModeTests
    {
        private const float RangerHoldRange = 15f;

        private static readonly OpponentArchetype[] Archetypes =
        {
            OpponentArchetype.Aggressor,
            OpponentArchetype.Evader,
            OpponentArchetype.Orbiter,
            OpponentArchetype.Kiter,
            OpponentArchetype.Dummy,
        };

        private GameObject arenaHost;
        private UnitService unitService;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private OpponentRoster roster;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[ArchetypeArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            projectiles = ShipServices.Compose(unitService, arenaHost.transform, presentationEnabled: true);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.timeScale = 20f;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            projectiles?.ReturnAllToPool();
            roster?.Dispose();
            roster = null;
            pair?.Dispose();
            pair = null;

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            projectiles = null;

            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_EachArchetype_ProducesIntentsAndTerminates()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 60;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            SpawnPairWithRoster(in spec);

            foreach (var archetype in Archetypes)
            {
                var draw = roster.Install(archetype, in spec, 0, Vector2.zero);
                pair.Reset(in spec, 0);
                using var probe = new ArchetypeGateSampler(pair.Baseline, pair.Agent, Vector2.zero,
                    spec.arenaRadius, in draw);
                var runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, 0, Vector2.zero);
                runner.RecordOpponent(in draw);
                yield return RunToCompletion(runner, spec, probe);

                var row = probe.ToRow(runner.Result);
                Assert.AreEqual(archetype.ToString(), row.opponent.archetype);
                Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), row.outcome,
                    $"{archetype} episode did not terminate legally");
                Assert.AreNotEqual(EndKind.None.ToString(), row.endKind);

                switch (archetype)
                {
                    case OpponentArchetype.Dummy:
                        Assert.AreEqual(0, row.shotsFired, "Dummy must never fire");
                        Assert.Less(row.maxDisplacement, 10f, "Dummy must hold station");
                        break;
                    case OpponentArchetype.Evader:
                        Assert.AreEqual(0, row.shotsFired, "Evader must never fire");
                        Assert.Greater(row.maxDisplacement, 5f, "Evader must actually flee");
                        break;
                    default:
                        Assert.Greater(row.maxDisplacement, 2f,
                            $"{archetype} produced no motion — brain install/wiring broken?");
                        break;
                }

                var gateRoundTrip = JsonUtility.FromJson<ArchetypeGateRow>(row.ToJsonLine());
                Assert.AreEqual(row.outcome, gateRoundTrip.outcome);
                Assert.AreEqual(row.opponent.archetype, gateRoundTrip.opponent.archetype);

                // The archetype draw rides the standard episode row additively.
                var episodeRoundTrip = JsonUtility.FromJson<EpisodeResult>(runner.Result.ToJsonLine());
                Assert.AreEqual(EpisodeResult.SchemaId, episodeRoundTrip.schema);
                Assert.AreEqual(archetype.ToString(), episodeRoundTrip.opponent.archetype);
            }
        }

        /// <summary>The gate composition: the canonical pair with the deterministic ranger stand-in on the agent side, and the roster bound to the opponent while its prefab-default utility brain is still installed.</summary>
        private void SpawnPairWithRoster(in RewardSpec spec)
        {
            pair = EpisodePair.Spawn(unitService, Vector2.zero, field: null, projectiles, in spec, (commander, baselineShip) =>
            {
                var ranger = commander.InstallBrain<RangerBrain>();
                ranger.Configure(baselineShip, RangerHoldRange);
                return ranger;
            }, assets);
            roster = new OpponentRoster(pair.Baseline, pair.Agent);
        }

        private IEnumerator RunToCompletion(EpisodeRunner runner, RewardSpec spec, ArchetypeGateSampler probe)
        {
            runner.Begin();
            var maxSimSeconds = spec.timeoutDecisions * spec.decisionIntervalSteps * Time.fixedDeltaTime;
            var deadline = Time.realtimeSinceStartup + 120f + maxSimSeconds * 2f;
            while (!runner.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                runner.Tick();
                probe.Sample();
            }
            Assert.IsTrue(runner.IsDone, "Episode wall-clock deadline exceeded before termination");
        }
    }
}
#endif
