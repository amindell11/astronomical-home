using System;
using Ships;

namespace Objectives.States
{
    /// <summary>
    /// Commits extraction on the hero's jump request while the zone reports it eligible, before another simulation step can damage it.
    /// A request outside the zone is dropped, so flying in never commits on its own; with no hero nothing commits.
    /// The sector module owns disposal because closing an objective does not exit its current state.
    /// </summary>
    public sealed class ExtractionJumpState : ObjectiveState, IDisposable
    {
        private readonly IExtractionZone zone;
        private readonly Ship hero;
        private readonly Action onCommit;
        private bool committed;

        public ExtractionJumpState(IExtractionZone zone, Ship hero, Action onCommit, Action onEnter = null) : base(onEnter)
        {
            this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
            this.hero = hero;
            this.onCommit = onCommit ?? throw new ArgumentNullException(nameof(onCommit));
        }

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
        public override bool IsComplete => committed;
        public override string StatusLine => zone.IsPlayerInZone ? "PRESS F TO JUMP" : "REACH THE JUMP GATE";

        public override void Enter()
        {
            committed = false;
            base.Enter();
            if (hero) hero.JumpRequested += Commit;
        }

        public override void Tick(float deltaTime) { }
        public override void Exit() => StopWatching();
        public void Dispose() => StopWatching();

        private void StopWatching()
        {
            if (hero) hero.JumpRequested -= Commit;
        }

        private void Commit()
        {
            if (!zone.IsPlayerInZone) return;
            committed = true;
            StopWatching();
            onCommit();
        }
    }
}
