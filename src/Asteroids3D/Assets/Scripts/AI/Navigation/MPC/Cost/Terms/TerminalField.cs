namespace AI.Navigation.MPC
{
    public static partial class Cost
    {
        public static float EvaluateTerminal(State endpoint, in CostInput input, in Config config)
        {
            var scale = config.wTerminalField * (input.sentence.field.armed ? input.sentence.field.weight : 1f);
            return scale == 0f ? 0f : scale * input.terminalField.Sample(endpoint.pos);
        }
    }
}
