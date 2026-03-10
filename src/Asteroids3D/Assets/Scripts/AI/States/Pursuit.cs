using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Pursuit : State
    {
        public override StateType Type => StateType.Pursuit;

        public Pursuit(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) =>
            ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance > utilityTuning.attack.optimalRangeMax;

        public override void Enter(Info context)
        {
            // Waypoint mode — pure distance minimization, no facing override.
            // Heading cost drives the nose toward the enemy for efficient forward-thrust approach.
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            navigator.SetNavigationPoint(combat.EnemyPos, true, combat.EnemyVel);
        }

        public override void Exit()
        {
            navigator.ClearNavigationPoint();
        }

        public override float ComputeUtility(Info ctx)
        {
            var a = ctx.Assessment;
            var t = utilityTuning.pursuit;

            // Only score high when outside attack range
            var distNorm = Mathf.Clamp01(a.EnemyDistance / t.engageDistance);

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("distance", distNorm, t.distanceFactor)
                .Factor("threat", a.Outnumbered, t.threatFactor)
                .Build();
        }
    }
}
