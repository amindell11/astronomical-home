using System.Collections;
using Game.Services;
using Movement.MPC.Field;
using Ships.Command;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for RL runs: composes the arena + episode pair + ShipAgent, applies and continuously asserts the pacing contract, and drives the episode loop from the WaitForFixedUpdate phase while mlagents-learn attaches to editor play mode.</summary>
    public sealed class TrainingHost : MonoBehaviour
    {
        [Tooltip("Episodes to run; 0 = until play mode stops.")]
        [SerializeField] private int episodes;
        [SerializeField] private int runSeed = 1;
        [Tooltip("Default = trainer when connected, else heuristic. HeuristicOnly for a Python-free loop check.")]
        [SerializeField] private BehaviorType behaviorType = BehaviorType.Default;

        private IEnumerator Start()
        {
            PacingContract.Apply();
            StartCoroutine(PacingWatchdog());

            var spec = RewardSpec.Default;
            spec.runSeed = runSeed;

            var units = gameObject.AddComponent<UnitService>();
            var navField = gameObject.AddComponent<NavFieldService>();
            var arena = new ArenaContext(Vector2.zero, units.ActiveRegistry, navField);
            units.SetArena(arena);

            AgentChooser chooser = null;
            var pair = EpisodePair.Spawn(units, arena, in spec, (agentShip, baselineShip) =>
            {
                chooser = new AgentChooser();
                chooser.Configure(baselineShip,
                    agentShip.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
                return chooser;
            });

            var agent = behaviorType == BehaviorType.HeuristicOnly
                ? ShipAgentFactory.ComposeHeuristicOnly(pair, chooser, in spec, arena.Offset, transform)
                : ShipAgentFactory.ComposeForTraining(pair, chooser, in spec, arena.Offset, transform);

            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);
            var jsonlPath = EpisodeJsonl.NewRunPath("training");

            for (var i = 0; episodes <= 0 || i < episodes; i++)
            {
                yield return driver.RunEpisode(spec, i);
                var result = driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                Debug.Log($"[TrainingHost] episode {i}: {result.outcome}/{result.endKind} "
                    + $"decisions={result.decisions} reward={result.totalReward:F3}");
            }
        }

        private IEnumerator PacingWatchdog()
        {
            while (enabled)
            {
                PacingContract.AssertHolds();
                yield return null;
            }
        }
    }
}
