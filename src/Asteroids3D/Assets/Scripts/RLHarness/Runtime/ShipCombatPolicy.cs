namespace Game.RLHarness
{
    /// <summary>The trained ShipCombat policy contract shared by training, eval, and gameplay hosts.</summary>
    public static class ShipCombatPolicy
    {
        public const string BehaviorName = "ShipCombat";
        public const int DecisionIntervalSteps = 10;
    }

    /// <summary>Which observation/action contract a checkpoint was trained against. A checkpoint runs only under the surface it was exported for — the tensor shapes and sensor names are baked into the ONNX — so this is authored next to the model on the pilot, and ML-Agents rejects a mismatched pair when the policy first loads.</summary>
    public enum PolicySurface
    {
        /// <summary>The shipped 500k checkpoint: <see cref="LegacyAgentObservations"/>'s flat 72-float vector with asteroids packed inline, 4 continuous actions, and aim/fire delegated to the Gunner.</summary>
        Legacy72,

        /// <summary>Stage-(iii) manual aim: <see cref="AgentObservations"/>'s 26-float vector plus the asteroid attention buffer, and 6 continuous actions in which the policy owns facing, facing authority, and the trigger.</summary>
        ManualAim,
    }
}
