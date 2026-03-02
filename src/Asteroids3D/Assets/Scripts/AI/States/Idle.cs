using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Idle : State
    {
        public override StateType Type => StateType.Idle;

        public Idle(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);
            
            navigator.ClearNavigationPoint();
            gunner.SetTarget((Transform)null);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
        }

        public override void Exit()
        {
            base.Exit();
        }

        public override float ComputeUtility(Info ctx)
        {
            if(ctx.InCombat)
            {
                return 0f;
            }
            return 0;
        }
    }
} 