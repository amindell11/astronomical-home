using System;
using System.Collections.Generic;
using AI.Utility;
using Movement.MPC;
using UnityEngine;
using AIContext = AI.Context.AIContext;
using SituationAssessment = AI.Context.SituationAssessment;

namespace AI.States
{
    // ── Input Enums ──

    public enum ContinuousInput
    {
        [Tooltip("Self HP fraction (0 = dead, 1 = full). Curve above 1 rewards, below 1 penalizes.")]
        Health,
        [Tooltip("Self shield fraction (0 = depleted, 1 = full).")]
        Shield,
        [Tooltip("Enemy combined health+shield (0 = near death, 1 = full). Low = finishing blow opportunity.")]
        EnemyDurability,
        [Tooltip("How outnumbered we are (0 = alone or outnumber them, 1 = heavily outnumbered).")]
        Outnumbered,
        [Tooltip("Closing speed (0 = separating fast, 0.5 = stationary, 1 = closing fast).")]
        ClosingRate,
        [Tooltip("How directly the enemy aims at us (0 = facing away, 1 = aiming right at us).")]
        EnemyFacing,
        [Tooltip("Our angle to the enemy (0 = facing them, 1 = facing away).")]
        SelfAngle,
        [Tooltip("Self speed as fraction of max (0 = stopped, 1 = full speed).")]
        Speed,
    }

    public enum BinarySignal
    {
        [Tooltip("True when we have clear line of sight to the enemy.")]
        Los,
        [Tooltip("True when not in combat (no enemies detected recently).")]
        NoCombat,
        [Tooltip("True when a missile is heading toward us.")]
        IncomingMissile,
        [Tooltip("True when cover is nearby.")]
        NearCover,
    }

    // ── UtilityFactor hierarchy ──

    [Serializable]
    public abstract class UtilityFactor
    {
        [Tooltip("Exponent for weighted geometric mean. 1 = normal, 2 = double influence, 0.5 = half influence.")]
        public float weight = 1f;

        public abstract string Name { get; }
        public abstract float Evaluate(SituationAssessment a);
    }

    [Serializable]
    [Tooltip("Maps a continuous input [0-1] through a response curve to produce a utility multiplier. Curve Y=1 is neutral, >1 boosts, <1 penalizes.")]
    public class CurveFactor : UtilityFactor
    {
        public ContinuousInput input;
        public AnimationCurve response = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public override string Name => input.ToString();

        public override float Evaluate(SituationAssessment a)
        {
            return response.Evaluate(ResolveContinuous(input, a));
        }

        internal static float ResolveContinuous(ContinuousInput input, SituationAssessment a)
        {
            return input switch
            {
                ContinuousInput.Health => a.HealthPct,
                ContinuousInput.Shield => a.ShieldPct,
                ContinuousInput.EnemyDurability => a.EnemyCombinedDurability,
                ContinuousInput.Outnumbered => a.Outnumbered,
                ContinuousInput.ClosingRate => a.ClosingRate,
                ContinuousInput.EnemyFacing => a.EnemyFacingThreat,
                ContinuousInput.SelfAngle => a.SelfAngleNorm,
                ContinuousInput.Speed => a.SpeedPct,
                _ => 0f
            };
        }
    }

    [Serializable]
    [Tooltip("Applies one of two multiplier values based on a boolean signal.")]
    public class BinaryFactor : UtilityFactor
    {
        public BinarySignal signal;
        public float whenTrue = 1.2f;
        public float whenFalse = 0.8f;

        public override string Name => signal.ToString();

        public override float Evaluate(SituationAssessment a)
        {
            return ResolveBinary(signal, a) ? whenTrue : whenFalse;
        }

        internal static bool ResolveBinary(BinarySignal signal, SituationAssessment a)
        {
            return signal switch
            {
                BinarySignal.Los => a.HasLineOfSight,
                BinarySignal.NoCombat => !a.InCombat,
                BinarySignal.IncomingMissile => a.IncomingMissile,
                BinarySignal.NearCover => a.NearCover,
                _ => false
            };
        }
    }

    [Serializable]
    [Tooltip("Scores 1.0 when enemy is within the optimal range band. Falls off linearly outside, using the band width as falloff distance.")]
    public class RangeBandFactor : UtilityFactor
    {
        [Tooltip("Inner edge of the optimal engagement range (meters).")]
        public float optimalMin;
        [Tooltip("Outer edge of the optimal engagement range (meters).")]
        public float optimalMax;

        public override string Name => "Range";

        public override float Evaluate(SituationAssessment a)
        {
            var dist = a.EnemyDistance;
            if (dist >= optimalMin && dist <= optimalMax) return 1f;

            var bandWidth = Mathf.Max(optimalMax - optimalMin, 1f);
            return dist < optimalMin ? Mathf.Clamp01(1f - (optimalMin - dist) / bandWidth) : Mathf.Clamp01(1f - (dist - optimalMax) / bandWidth);
        }
    }

    [Serializable]
    [Tooltip("Maps enemy distance (normalized by engage distance) through a curve. Used for pursuit states to score chasing distant enemies.")]
    public class DistanceFactor : UtilityFactor
    {
        [Tooltip("Reference distance for normalizing enemy distance.")]
        public float engageDistance = 50f;
        public AnimationCurve response = AnimationCurve.Linear(0f, 0.3f, 1f, 1.2f);

        public override string Name => "Distance";

        public override float Evaluate(SituationAssessment a)
        {
            var t = Mathf.Clamp01(a.EnemyDistance / Mathf.Max(engageDistance, 1f));
            return response.Evaluate(t);
        }
    }

    [Serializable]
    [Tooltip("Applies a flat boost when the enemy is closer than the danger distance AND has line of sight. Useful for evade urgency at close range.")]
    public class ProximityThreatFactor : UtilityFactor
    {
        [Tooltip("Distance threshold below which the boost activates (meters).")]
        public float dangerDistance = 10f;
        [Tooltip("Multiplier applied when triggered. 1.0 = neutral.")]
        public float boost = 1.3f;

        public override string Name => "ProximityThreat";

        public override float Evaluate(SituationAssessment a)
        {
            return (a.EnemyDistance < dangerDistance && a.HasLineOfSight) ? boost : 1f;
        }
    }

    [Serializable]
    [Tooltip("Applies a flat boost when hull is below the health threshold AND shields are above the shield threshold. Models the 'shields holding but hull critical' disengage trigger.")]
    public class FightingRetreatFactor : UtilityFactor
    {
        [Tooltip("Health fraction below which retreat activates.")]
        public float healthThreshold = 0.5f;
        [Tooltip("Shield fraction above which retreat activates (shields still holding).")]
        public float shieldThreshold = 0.5f;
        [Tooltip("Multiplier applied when triggered. 1.0 = neutral.")]
        public float boost = 1.25f;

        public override string Name => "FightingRetreat";

        public override float Evaluate(SituationAssessment a)
        {
            return (a.HealthPct < healthThreshold && a.ShieldPct > shieldThreshold) ? boost : 1f;
        }
    }

    // ── GoalStrategy hierarchy ──

    [Serializable]
    public abstract class GoalStrategy
    {
        public abstract GoalMode GoalMode { get; }
    }

    [Serializable]
    [Tooltip("Track and engage the enemy at a desired range.")]
    public class TrackEnemyGoal : GoalStrategy
    {
        public float desiredRange = 20f;
        public float rangeTolerance = 10f;

        public override GoalMode GoalMode => GoalMode.MaintainRange;
    }

    [Serializable]
    [Tooltip("Flee from the enemy.")]
    public class FleeEnemyGoal : GoalStrategy
    {
        public override GoalMode GoalMode => GoalMode.Flee;
    }

    [Serializable]
    [Tooltip("Patrol randomly within a radius.")]
    public class RandomWaypointGoal : GoalStrategy
    {
        public float patrolRadius = 50f;
        public float minDistanceFactor = 0.3f;
        public float arriveRadius = 3f;
        public float stuckTimeout = 3f;
        public float stuckProgressThreshold = 1f;

        public override GoalMode GoalMode => GoalMode.Waypoint;
    }

    // ── StateProfile ──

    /// <summary>
    /// Complete data profile for a single AI state. Bundles navigation style,
    /// utility scoring factors, and availability conditions into one asset.
    /// </summary>
    [CreateAssetMenu(fileName = "StateProfile", menuName = "AI/State Profile")]
    public class StateProfile : ScriptableObject
    {
        [Header("Identity")]

        [Header("Goal")]
        [SerializeReference] public GoalStrategy goal;

        [Header("Tactical")]
        [Tooltip("Enable tactical MPC costs (facing, exposure, LOS, tangential).")]
        public bool enableTacticalCosts;
        [Tooltip("Enable weapon firing in this state.")]
        public bool enableFiring;

        [Header("MPC Weight Overrides")]
        [Tooltip("Sparse per-state multipliers for MPC weights. List only the weights this " +
                 "state changes; an absent weight uses the base value (×1). 0 = disable, 2 = double.")]
        public WeightOverride[] weightOverrides = System.Array.Empty<WeightOverride>();

        [Header("Availability")]
        [Tooltip("State requires an enemy to be available.")]
        public bool requiresEnemy;
        [Tooltip("State requires NO enemy (e.g. Patrol).")]
        public bool requiresNoEnemy;
        [Tooltip("Minimum distance to enemy for availability. 0 = no minimum.")]
        public float minRange;
        [Tooltip("Maximum distance to enemy for availability. 0 = no maximum.")]
        public float maxRange;

        [Header("Utility Factors")]
        [SerializeReference] public List<UtilityFactor> utilityFactors = new List<UtilityFactor>();

        /// <summary>
        /// Availability gate for selection: whether this state may be considered given the
        /// current situation. A pure predicate over this profile's own availability fields.
        /// </summary>
        public bool IsAvailable(AIContext ctx)
        {
            var hasEnemy = ctx.Combat.HasEnemy;

            if (requiresEnemy && !hasEnemy) return false;
            if (requiresNoEnemy && hasEnemy) return false;

            // Range gates only apply when there is an enemy to measure against.
            if (hasEnemy)
            {
                var dist = ctx.Assessment.EnemyDistance;
                if (minRange > 0f && dist <= minRange) return false;
                if (maxRange > 0f && dist > maxRange) return false;
            }

            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var brain in FindObjectsByType<Brain>(
                         UnityEngine.FindObjectsSortMode.None))
                brain.RefreshStates();
        }
#endif
    }
}
