using System;

namespace Game.RLHarness
{
    public sealed class DecisionTransitionRecorder
    {
        private readonly DecisionTransitionJsonl output;
        private readonly RewardSpec spec;
        private readonly int episodeIndex;
        private readonly int teamId;

        private DecisionObservation state;
        private ExecutedDecisionAction action;
        private bool hasState;
        private bool hasAction;
        private bool ended;

        internal DecisionTransitionRecorder(DecisionTransitionJsonl output, in RewardSpec spec,
            int episodeIndex, int teamId)
        {
            if (episodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(episodeIndex), episodeIndex,
                    "Transition episode indices must be non-negative.");
            if (teamId is not (0 or 1))
                throw new ArgumentOutOfRangeException(nameof(teamId), teamId,
                    "Transition team ids must be 0 or 1.");

            this.output = output;
            this.spec = spec;
            this.episodeIndex = episodeIndex;
            this.teamId = teamId;
        }

        public void BeginDecision(in DecisionObservation observation)
        {
            if (ended)
                throw new InvalidOperationException("A completed episode cannot begin another transition.");
            if (hasState)
                throw new InvalidOperationException("The current transition already has its decision observation.");

            state = observation;
            hasState = true;
        }

        public void RecordAction(in ExecutedDecisionAction executedAction)
        {
            if (!hasState)
                throw new InvalidOperationException("An executed action requires its preceding decision observation.");
            if (hasAction)
                throw new InvalidOperationException("The current transition already has an executed action.");

            action = executedAction;
            hasAction = true;
        }

        public void Complete(in BoundaryResult boundary, in DecisionObservation nextState)
        {
            if (!hasState || !hasAction)
                throw new InvalidOperationException(
                    "A paid decision boundary requires an aligned observation and executed action.");

            var transition = DecisionTransition.Create(output.RunId, output.WorkerIndex,
                output.ArenaIndex, in spec, episodeIndex, teamId, in state, in action,
                in boundary, in nextState);
            output.Append(in transition);

            state = default;
            action = default;
            hasState = false;
            hasAction = false;
            ended = boundary.endKind != EndKind.None;
        }
    }
}
