namespace Game.RLHarness
{
    /// <summary>The trained ShipCombat policy contract shared by training, eval, and gameplay hosts.</summary>
    public static class ShipCombatPolicy
    {
        public const string BehaviorName = "ShipCombat";
        public const int DecisionIntervalSteps = 10;
    }
}
