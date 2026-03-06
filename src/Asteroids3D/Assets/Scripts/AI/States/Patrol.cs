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

        public Patrol(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);
            
            gunner.SetTarget((Transform)null);
            
            ChooseNewPatrolPoint(ctx);
        }

        public override void Tick(Info context, float deltaTime)
        {
            if (!navigator.CurrentWaypoint.isValid || context.VectorToWaypoint.magnitude < navigator.arriveRadius)
            {
                ChooseNewPatrolPoint(context);
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
                .FactorBinary(!ctx.InCombat, "noCombat", new FactorRange(0.01f, 2.0f))
                .Build();
        }

        private void ChooseNewPatrolPoint(Info ctx)
        {
            var currentPos = ctx.SelfPosition;
            var randomDistance = Random.Range(utilityTuning.patrolRadius * utilityTuning.patrolMinDistanceFactor, utilityTuning.patrolRadius);
            var randomDirection = Random.insideUnitCircle.normalized;
            currentTarget = currentPos + randomDirection * randomDistance;

            hasTarget = true;

            navigator.SetNavigationPoint(currentTarget, enableAvoidance);
        }
    }
} 