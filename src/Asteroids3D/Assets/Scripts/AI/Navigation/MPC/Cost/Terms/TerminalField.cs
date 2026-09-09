namespace Movement.MPC
{
    // Solver-owned terminal term: beyond-horizon route knowledge from the ship's baked field,
    // charged once at the rollout's end state — outside the terminal ramp, never saturated.
    public static partial class Cost
    {
        /// <summary>Detour-excess cost at the rollout's end state, 0 without a valid field; shared by the rollout job and the trajectory breakdown so the two cannot drift.</summary>
        public static float EvaluateTerminal(State end, in CostInput input, in Config cfg)
        {
            if (cfg.wTerminalField <= 0f || !input.terminalField.IsValid) return 0f;
            return cfg.wTerminalField * FieldScale(in input.sentence) * input.terminalField.DetourExcess(end.pos);
        }

        /// <summary>The FIELD slot's authority over both shaping terms, turn-away and terminal field; unarmed = ×1.</summary>
        internal static float FieldScale(in IntentSentence sentence)
            => sentence.field.armed ? sentence.field.weight : 1f;
    }
}
