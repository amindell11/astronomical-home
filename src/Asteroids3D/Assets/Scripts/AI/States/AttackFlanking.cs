using Movement;
using Movement.MPC;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class AttackFlanking : State
    {
        public override StateType Type => StateType.AttackFlanking;

        public AttackFlanking(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning)
            : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) =>
            ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance <= utilityTuning.attackFlanking.rangeMax;

        public override void Enter(Info context)
        {
            var t = utilityTuning.attackFlanking;
            // Use Waypoint mode — the flanking point is computed each tick
            navigator.ClearGoalMode();

            navigator.SetMpcWeightOverrides(new WeightOverrides
            {
                wFacing = t.wFacing,
                wExposure = t.wExposure,
                wLos = float.NaN,
                wTangential = float.NaN,
                exposurePower = float.NaN,
                facingPower = float.NaN,
            });
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            navigator.SetObstacleExclusion(combat.Enemy.transform);

            // Compute flanking waypoint: offset perpendicular to enemy's forward, at desired range
            var t = utilityTuning.attackFlanking;
            var enemyFwd = combat.EnemyForward;
            var flankAngleRad = t.flankAngleDeg * Mathf.Deg2Rad;

            // Two candidate flank sides: pick the one closer to the ship
            var shipPos = ctx.ShipInfo.Pos;
            var toShip = shipPos - combat.EnemyPos;

            // Rotate enemy forward by +/- flank angle
            var cosA = Mathf.Cos(flankAngleRad);
            var sinA = Mathf.Sin(flankAngleRad);
            var leftFlank = new Vector2(
                enemyFwd.x * cosA - enemyFwd.y * sinA,
                enemyFwd.x * sinA + enemyFwd.y * cosA);
            var rightFlank = new Vector2(
                enemyFwd.x * cosA + enemyFwd.y * sinA,
                -enemyFwd.x * sinA + enemyFwd.y * cosA);

            // Pick side closer to current ship position
            var leftPoint = combat.EnemyPos + leftFlank * t.desiredRange;
            var rightPoint = combat.EnemyPos + rightFlank * t.desiredRange;
            var flankPoint = (shipPos - leftPoint).sqrMagnitude < (shipPos - rightPoint).sqrMagnitude
                ? leftPoint
                : rightPoint;

            navigator.SetNavigationPoint(flankPoint, true, combat.EnemyVel);

            // Still set enemy state for facing/exposure cost computation
            var enemyYawDeg = Mathf.Atan2(-enemyFwd.x, enemyFwd.y) * Mathf.Rad2Deg;
            navigator.SetEnemyState(enemyYawDeg, combat.EnemyYawRate, combat.LaserSpeed);

            // Fire while flanking
            var predictedTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos, combat.EnemyVel, combat.LaserSpeed);
            gunner.SetTarget(predictedTarget);
        }

        public override void Exit()
        {
            navigator.ClearNavigationPoint();
            navigator.ClearGoalMode();
            navigator.ClearEnemyState();
            navigator.ClearObstacleExclusion();
            navigator.ClearMpcWeightOverrides();
        }

        public override float ComputeUtility(Info ctx)
        {
            var a = ctx.Assessment;
            var t = utilityTuning.attackFlanking;
            var inRange = a.EnemyDistance <= t.rangeMax;
            var rangeScore = inRange ? 1f : 0.3f;

            // Flanking is best at mid-range self-angle: not head-on, not behind
            // Peak at 0.4-0.6 SelfAngleNorm
            var angleMidBoost = 1f - Mathf.Abs(a.SelfAngleNorm - 0.5f) * 2f;
            angleMidBoost = Mathf.Clamp01(angleMidBoost);

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("selfAngle", angleMidBoost, t.selfAngleFactor)
                .Factor("enemyFacing", a.EnemyFacingThreat, t.enemyFacingFactor)
                .Factor("range", rangeScore, t.rangeFactor)
                .FactorBinary(a.HasLineOfSight, "LOS", t.losFactor)
                .Build();
        }
    }
}
