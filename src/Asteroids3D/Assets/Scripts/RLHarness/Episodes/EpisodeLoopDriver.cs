using System.Collections;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Read access to the combat snapshot the episode loop captured this fixed step, so a per-step observer never re-derives it.</summary>
    public interface IStepSnapshotSource
    {
        CombatSnapshot StepSnapshot { get; }
    }

    /// <summary>The decision & reset ordering contract, in one place. The Academy auto-steps every FixedUpdate (this driver no longer owns the step); it only paces decisions: prime the first RequestDecision after Begin so the opening step runs under a real action, and RequestDecision again at each paid boundary. Zero decision latency holds because AICommander (which reads the action) is ordered after the AcademyFixedUpdateStepper, so a boundary's action is applied the same FixedUpdate it is produced. On episode end AddReward → EndEpisode/EpisodeInterrupted BEFORE the next pair-reset so the terminal observation reflects the end state.</summary>
    public sealed class EpisodeLoopDriver : IStepSnapshotSource
    {
        private readonly EpisodePair pair;
        private readonly ShipAgent agent;
        private readonly ShipAgent opponentAgent;
        private readonly Vector2 arenaCenter;
        private readonly HarnessField field;
        private readonly OpponentRoster roster;
        private readonly WaitForFixedUpdate waitFixed = new();

        public EpisodeRunner Runner { get; private set; }
        /// <summary>Forwarded, never cached: the runner is rebuilt per episode, so an observer bound before RunEpisode must read through the driver.</summary>
        public CombatSnapshot StepSnapshot => Runner.StepSnapshot;
        /// <summary>The agent-side cumulative reward captured just before EndEpisode cleared it — must equal the runner's totalReward.</summary>
        public float LastEpisodeCumulativeReward { get; private set; }
        /// <summary>Self-play only: the team-1 agent's mirror runner and its cumulative reward, captured before its EndEpisode (null/0 single-agent).</summary>
        public EpisodeRunner OpponentRunner { get; private set; }
        public float LastOpponentEpisodeCumulativeReward { get; private set; }

        /// <summary>A null <paramref name="agent"/> is the scripted-vs-scripted measurement composition (open-loop lane): the runner still owns boundaries and termination, but no decision requests or rewards flow anywhere.</summary>
        public EpisodeLoopDriver(EpisodePair pair, ShipAgent agent, Vector2 arenaCenter, HarnessField field = null,
            OpponentRoster roster = null, ShipAgent opponentAgent = null)
        {
            this.pair = pair;
            this.agent = agent;
            this.opponentAgent = opponentAgent;
            this.arenaCenter = arenaCenter;
            this.field = field;
            this.roster = roster;
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
            // Install before the pair-reset: the respawn re-inits the installed brain (traversal-probe ordering).
            var draw = roster?.Install(in spec, episodeIndex, arenaCenter);
            pair.Reset(in spec, episodeIndex);
            Runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, episodeIndex, arenaCenter, tracePerDecision);
            if (draw.HasValue) Runner.RecordOpponent(draw.Value);
            Runner.Begin();
            onBegin?.Invoke();
            agent?.BindEpisode(Runner);

            EpisodeRunner opponentRunner = null;
            if (opponentAgent != null)
            {
                opponentRunner = new EpisodeRunner(pair.Baseline, pair.Agent, spec, episodeIndex, arenaCenter, tracePerDecision);
                opponentRunner.Begin();
                opponentAgent.BindEpisode(opponentRunner);
            }
            OpponentRunner = opponentRunner;

            // self_play: trainer serves the team-1 ghost; both agents request, the Academy auto-steps both.
            agent?.RequestDecision();
            opponentAgent?.RequestDecision();

            while (!Runner.IsDone)
            {
                yield return waitFixed;
                var boundaryReached = Runner.Tick();
                opponentRunner?.Tick();
                onFixedStep?.Invoke();
                if (!boundaryReached) continue;

                var boundary = Runner.LastBoundary;
                agent?.AddReward(boundary.Total);
                if (opponentRunner != null) opponentAgent.AddReward(opponentRunner.LastBoundary.Total);
                switch (boundary.endKind)
                {
                    case EndKind.Terminal:
                        if (agent != null)
                        {
                            LastEpisodeCumulativeReward = agent.GetCumulativeReward();
                            agent.EndEpisode();
                        }
                        if (opponentAgent != null)
                        {
                            LastOpponentEpisodeCumulativeReward = opponentAgent.GetCumulativeReward();
                            opponentAgent.EndEpisode();
                        }
                        break;
                    case EndKind.Truncation:
                        if (agent != null)
                        {
                            LastEpisodeCumulativeReward = agent.GetCumulativeReward();
                            agent.EpisodeInterrupted();
                        }
                        if (opponentAgent != null)
                        {
                            LastOpponentEpisodeCumulativeReward = opponentAgent.GetCumulativeReward();
                            opponentAgent.EpisodeInterrupted();
                        }
                        break;
                    default:
                        agent?.RequestDecision();
                        opponentAgent?.RequestDecision();
                        break;
                }
            }
        }
    }
}
