using System;

namespace Game.RLHarness
{
    /// <summary>Pure per-episode overlay of ML-Agents environment parameters onto a <see cref="RewardSpec"/>: the trainer's curriculum reaches every spec consumer (field, roster, JSONL self-description) through the one value the harness already threads. The getter is injected (TrainingHost passes <c>Academy.Instance.EnvironmentParameters.GetWithDefault</c>; tests pass a dictionary) so this stays EditMode-testable and Academy-free.</summary>
    public static class EnvParamOverlay
    {
        public const string UseAsteroidField = "use_asteroid_field";
        public const string FieldDensityScale = "field_density_scale";
        public const string CollisionLethality = "collision_lethality";
        public const string OpponentWeightAggressor = "opponent_weight_aggressor";
        public const string OpponentWeightEvader = "opponent_weight_evader";
        public const string OpponentWeightOrbiter = "opponent_weight_orbiter";
        public const string OpponentWeightKiter = "opponent_weight_kiter";
        public const string OpponentWeightDummy = "opponent_weight_dummy";

        internal static readonly string[] ParamNames =
        {
            UseAsteroidField,
            FieldDensityScale,
            CollisionLethality,
            OpponentWeightAggressor,
            OpponentWeightEvader,
            OpponentWeightOrbiter,
            OpponentWeightKiter,
            OpponentWeightDummy,
        };

        /// <summary>Applies the current environment-parameter values onto <paramref name="spec"/>; a parameter the trainer does not send leaves its spec field untouched (the getter's default).</summary>
        public static RewardSpec Apply(RewardSpec spec, Func<string, float, float> getWithDefault)
        {
            spec.useAsteroidField = getWithDefault(UseAsteroidField, spec.useAsteroidField ? 1f : 0f) > 0.5f;
            spec.fieldDensityScale = getWithDefault(FieldDensityScale, spec.fieldDensityScale);
            spec.collisionLethality = getWithDefault(CollisionLethality, spec.collisionLethality);
            spec.weightAggressor = getWithDefault(OpponentWeightAggressor, spec.weightAggressor);
            spec.weightEvader = getWithDefault(OpponentWeightEvader, spec.weightEvader);
            spec.weightOrbiter = getWithDefault(OpponentWeightOrbiter, spec.weightOrbiter);
            spec.weightKiter = getWithDefault(OpponentWeightKiter, spec.weightKiter);
            spec.weightDummy = getWithDefault(OpponentWeightDummy, spec.weightDummy);

            var weightSum = spec.weightAggressor + spec.weightEvader + spec.weightOrbiter
                + spec.weightKiter + spec.weightDummy;
            if (!(weightSum > 0f))
                throw new InvalidOperationException(
                    $"Opponent mixture weights from environment parameters sum to {weightSum} — the trainer YAML must keep the mixture pickable (sum > 0).");
            return spec;
        }
    }
}
