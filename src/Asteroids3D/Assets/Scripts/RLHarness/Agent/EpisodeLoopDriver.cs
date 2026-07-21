using System.Collections;
using Unity.MLAgents;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The step & reset ordering contract, in one place. Runner-owned manual stepping on the WaitForFixedUpdate phase (the same phase the PlayMode tests use): prime the first decision after Begin so no step runs under a default action; at each paid boundary AddReward → RequestDecision → EnvironmentStep (zero decision latency — observe s_t, act on [t, t+K)); on episode end AddReward → EndEpisode/EpisodeInterrupted BEFORE the next pair-reset so the terminal observation reflects the end state.</summary>
    public sealed class EpisodeLoopDriver
    {
        private readonly EpisodePair pair;
        private readonly ShipAgent agent;
        private readonly ShipAgent opponentAgent;
        private readonly Vector2 arenaCenter;
        private readonly HarnessField field;
        private readonly OpponentRoster roster;
        private readonly WaitForFixedUpdate waitFixed = new();

        public EpisodeRunner Runner { get; private set; }
        /// <summary>The agent-side cumulative reward captured just before EndEpisode cleared it — must equal the runner's totalReward.</summary>
        public float LastEpisodeCumulativeReward { get; private set; }
        /// <summary>Self-play only: the team-1 agent's mirror runner and its cumulative reward, captured before its EndEpisode (null/0 single-agent).</summary>
        public EpisodeRunner OpponentRunner { get; private set; }
        public float LastOpponentEpisodeCumulativeReward { get; private set; }

        public EpisodeLoopDriver(EpisodePair pair, ShipAgent agent, Vector2 arenaCenter, HarnessField field = null,
            OpponentRoster roster = null, ShipAgent opponentAgent = null)
        {
            this.pair = pair;
            this.agent = agent;
            this.opponentAgent = opponentAgent;
            this.arenaCenter = arenaCenter;
            this.field = field;
            this.roster = roster;
            Academy.Instance.AutomaticSteppingEnabled = false;
        }

        /// <summary><paramref name="onBegin"/> fires once after Begin (spawn pose settled) and <paramref name="onFixedStep"/> once per fixed step after Tick — the hooks a per-step behavioral sampler (eval scorecard) rides, matching the archetype gate's construct-then-sample ordering.</summary>
        public IEnumerator RunEpisode(RewardSpec spec, int episodeIndex, bool tracePerDecision = false,
            System.Action onBegin = null, System.Action onFixedStep = null)
        {
            if (spec.useAsteroidField && field == null)
                throw new System.InvalidOperationException(
                    "spec.useAsteroidField requires a HarnessField — the JSONL would claim asteroid episodes that ran in an empty arena.");
            // Field first: the episode's poses become generation-time clearings, so ships respawn onto carved ground.
            field?.Reset(in spec, episodeIndex, EpisodePoses.Derive(in spec, episodeIndex, arenaCenter));
            // Install before the pair-reset: the respawn re-inits the installed chooser (traversal-probe ordering).
            var draw = roster?.Install(in spec, episodeIndex, arenaCenter);
            pair.Reset(in spec, episodeIndex);
            Runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, episodeIndex, arenaCenter, tracePerDecision);
            if (draw.HasValue) Runner.RecordOpponent(draw.Value);
            Runner.Begin();
            onBegin?.Invoke();
            agent.BindEpisode(Runner);

            EpisodeRunner opponentRunner = null;
            if (opponentAgent != null)
            {
                opponentRunner = new EpisodeRunner(pair.Baseline, pair.Agent, spec, episodeIndex, arenaCenter, tracePerDecision);
                opponentRunner.Begin();
                opponentAgent.BindEpisode(opponentRunner);
            }
            OpponentRunner = opponentRunner;

            // self_play: trainer serves the team-1 ghost; both agents request, one EnvironmentStep drives both.
            agent.RequestDecision();
            opponentAgent?.RequestDecision();
            Academy.Instance.EnvironmentStep();

            while (!Runner.IsDone)
            {
                yield return waitFixed;
                var boundaryReached = Runner.Tick();
                opponentRunner?.Tick();
                onFixedStep?.Invoke();
                if (!boundaryReached) continue;

                var boundary = Runner.LastBoundary;
                agent.AddReward(boundary.Total);
                if (opponentRunner != null) opponentAgent.AddReward(opponentRunner.LastBoundary.Total);
                switch (boundary.endKind)
                {
                    case EndKind.Terminal:
                        LastEpisodeCumulativeReward = agent.GetCumulativeReward();
                        agent.EndEpisode();
                        if (opponentAgent != null)
                        {
                            LastOpponentEpisodeCumulativeReward = opponentAgent.GetCumulativeReward();
                            opponentAgent.EndEpisode();
                        }
                        break;
                    case EndKind.Truncation:
                        LastEpisodeCumulativeReward = agent.GetCumulativeReward();
                        agent.EpisodeInterrupted();
                        if (opponentAgent != null)
                        {
                            LastOpponentEpisodeCumulativeReward = opponentAgent.GetCumulativeReward();
                            opponentAgent.EpisodeInterrupted();
                        }
                        break;
                    default:
                        agent.RequestDecision();
                        opponentAgent?.RequestDecision();
                        Academy.Instance.EnvironmentStep();
                        break;
                }
            }
        }
    }
}
