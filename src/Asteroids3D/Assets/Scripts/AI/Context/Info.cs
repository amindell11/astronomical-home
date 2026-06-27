using System;
using AI.Planning;
using Movement;
using Unity.Properties;
using UnityEngine;
using TargetingUtils = Combat.TargetingUtils;

namespace AI.Context
{
    /// <summary>
    /// Provides AI context data for state machine consumption.
    /// Thin container composing subsystem references and a per-tick SituationAssessment.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public partial class Info
    {
        public ShipInfo ShipInfo { get; private set; }
        public CombatTracker Combat { get; private set; }
        public Navigation Nav { get; private set; }
        public TargetingUtils TargetingUtils { get; private set; }
        public Scanning.Scout Scout { get; private set; }
        public SituationAssessment Assessment { get; private set; }
        /// <summary>Optional high-level routing planner. Null in scenes without one.</summary>
        public AsteroidNavField NavPlanner { get; private set; }

        public Info(Ships.Ship ship, Navigator navigator, Gunner gunner, Scanning.Scout scout, TargetingUtils targetingUtils,
            float combatExitDelay = 3f, AsteroidNavField navPlanner = null)
        {
            if (!ship) return;

            var shipId = scout.ShipId;
            var registry = scout.Registry;

            ShipInfo = new ShipInfo(ship);
            TargetingUtils = targetingUtils;
            Scout = scout;
            Combat = new CombatTracker(scout, gunner, targetingUtils, shipId, registry, combatExitDelay);
            Nav = new Navigation(ShipInfo, navigator);
            Assessment = SituationAssessment.None;
            NavPlanner = navPlanner;
        }

        public void UpdateAssessment()
        {
            Combat.Update();
            Assessment = SituationAssessment.Evaluate(ShipInfo, Combat, Scout, TargetingUtils);
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
