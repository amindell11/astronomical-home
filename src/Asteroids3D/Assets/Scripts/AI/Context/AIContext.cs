using System;
using Ships.Command;

namespace AI.Context
{
    /// <summary>
    /// The world model the AI reasons over for a single tick: the live views
    /// (<see cref="Self"/>, <see cref="Combat"/>) over the sensing tier
    /// (<see cref="Scout"/>). Holds no actuators (navigator/gunner) — those are
    /// owned by the host that acts on the context.
    /// </summary>
    [Serializable]
    public class AIContext
    {
        public IShipStatus Self { get; private set; }
        public EnemyTracker Combat { get; private set; }
        public Scout Scout { get; private set; }

        public AIContext(IShipStatus self, Scout scout,
            float combatExitDelay = 3f)
        {
            if (self == null || scout == null) return;

            Self = self;
            Scout = scout;
            Combat = new EnemyTracker(scout, combatExitDelay);
        }

        public void Update(float deltaTime) => Combat.Update(deltaTime);
    }
}
