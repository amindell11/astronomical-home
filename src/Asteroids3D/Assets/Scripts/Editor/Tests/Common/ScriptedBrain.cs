using AI;
using AI.Context;
using AI.Strategy;

namespace Tests.Common
{
    public sealed class ScriptedBrain : Brain
    {
        public BrainDecision? decision;
        public override BrainDecision? Decide(AIContext ctx) => decision;
    }
}
