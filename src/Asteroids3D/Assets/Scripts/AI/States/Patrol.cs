using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Patrol : State
    {
        public override StateType Type => StateType.Patrol;

        private Vector2 currentTarget;
        private bool hasTarget = false;
        private readonly bool enableAvoidance = true;

        private float stuckTimer;
        private float bestDistToWaypoint;

        public Patrol(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) => !ctx.Combat.HasEnemy;

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);

            gunner.SetTarget((Transform)null);

            ChooseNewPatrolPoint(ctx);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            if (!navigator.CurrentWaypoint.isValid || ctx.Nav.VectorToWaypoint.magnitude < utilityTuning.patrol.arriveRadius)
            {
                ChooseNewPatrolPoint(ctx);
                return;
            }

            var dist = ctx.Nav.VectorToWaypoint.magnitude;
            var t = utilityTuning.patrol;

            if (dist < bestDistToWaypoint - t.stuckProgressThreshold)
            {
                bestDistToWaypoint = dist;
                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += deltaTime;
                if (stuckTimer >= t.stuckTimeout)
                {
                    ChooseNewPatrolPoint(ctx);
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            hasTarget = false;
        }

        public override float ComputeUtility(Info ctx)
        {
            return NewBuilder()
                .FactorBinary(!ctx.Assessment.InCombat, "noCombat", new FactorRange(0.01f, 2.0f))
                .Build();
        }

        private void ChooseNewPatrolPoint(Info ctx)
        {
            var t = utilityTuning.patrol;
            var currentPos = ctx.ShipInfo.Pos;
            var randomDistance = Random.Range(t.radius * t.minDistanceFactor, t.radius);
            var randomDirection = Random.insideUnitCircle.normalized;
            currentTarget = currentPos + randomDirection * randomDistance;

            hasTarget = true;
            stuckTimer = 0f;
            bestDistToWaypoint = float.MaxValue;

            navigator.SetNavigationPoint(currentTarget, enableAvoidance);
        }
    }
}
