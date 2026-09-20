using Unity.Mathematics;

namespace AI.Navigation.MPC
{
    public static partial class Cost
    {
        public static float EvaluateTerminal(State endpoint, in CostInput input, in Config config)
        {
            var scale = config.wTerminalField * (input.sentence.field.armed ? input.sentence.field.weight : 1f);
            if (scale == 0f) return 0f;
            var remaining = math.max(0f, math.distance(endpoint.pos, input.terminalField.goal) - input.sentence.pos.setpoint);
            var reach = math.sqrt(config.maxSpeedSq) * config.horizon * config.dt;
            var fraction = remaining > 0f ? remaining / math.max(remaining, reach) : 0f;
            var authority = fraction * fraction * (3f - 2f * fraction);
            return scale * authority * input.terminalField.Sample(endpoint.pos);
        }
    }
}
