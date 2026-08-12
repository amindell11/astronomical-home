using AI;
using AI.Context;
using Ships;

namespace Game.RLHarness
{
    /// <summary>Plays one fixed hand-authored intent sentence (Stage A3): the row's <see cref="SentenceRows"/> vector re-issued every tick, so strategy never varies and the 50 Hz solver supplies all the feedback. The anchor is re-bound by id each decision, so respawns and registry churn ride the commander's standard resolution path.</summary>
    public sealed class SentenceBrain : Brain
    {
        private Ship target;
        private SentenceRow row;

        public void Configure(Ship target, SentenceRow row)
        {
            this.target = target;
            this.row = row;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (!target || !target.gameObject.activeInHierarchy) return null;
            var fire = SentenceRows.Engages(row) ? FireControl.Auto : FireControl.Hold;
            return new BrainDecision(SentenceRows.Author(row, target.Id), fire, fire);
        }
    }
}
