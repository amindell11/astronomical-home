using System;
using AI.Planning;
using Unity.Properties;
using TargetingUtils = Combat.TargetingUtils;

namespace AI.Context
{
    /// <summary>
    /// The world model the AI reasons over for a single tick. Composes the live
    /// views (<see cref="Self"/>, <see cref="Combat"/>) over the sensing tier
    /// (<see cref="Scout"/>) and the per-tick normalized <see cref="Assessment"/>.
    ///
    /// Boundary: states read the live views to build navigation intents; utility
    /// factors score off the read-only <see cref="Assessment"/> snapshot. This
    /// container holds no actuators (navigator/gunner) — those are owned by the
    /// states that act on the context.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public partial class AIContext
    {
        public SelfStatus Self { get; private set; }
        public CombatStatus Combat { get; private set; }
        public TargetingUtils TargetingUtils { get; private set; }
        public Scanning.Scout Scout { get; private set; }
        public SituationAssessment Assessment { get; private set; }
        /// <summary>Active high-level routing planner, if any. Lazily resolved from
        /// <see cref="AsteroidNavField.Active"/> so AI init order vs. planner spawn order
        /// is irrelevant — returns null only when no planner has been spawned yet.</summary>
        public AsteroidNavField NavPlanner => AsteroidNavField.Active;

        public AIContext(SelfStatus self, Scanning.Scout scout, TargetingUtils targetingUtils,
            float combatExitDelay = 3f)
        {
            if (self == null || scout == null) return;

            Self = self;
            Scout = scout;
            TargetingUtils = targetingUtils;
            Combat = new CombatStatus(scout, combatExitDelay);
            Assessment = SituationAssessment.None;
        }

        public void UpdateAssessment()
        {
            Combat.Update();
            Assessment = SituationAssessment.Evaluate(Self, Combat, Scout, TargetingUtils);
        }

        public override string ToString()
        {
            var a = Assessment;
            return $"AIContext[HP:{a.HealthPct:F2} Shield:{a.ShieldPct:F2} " +
                   $"EnemyDist:{a.EnemyDistance:F1} LOS:{a.HasLineOfSight} " +
                   $"Enemies:{a.NearbyEnemyCount} Friends:{a.NearbyFriendCount}]";
        }
    }
}
