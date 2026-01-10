using AI.Steering;
using AI.Utility;
using Editor;
using Info = AI.Context.Info;

namespace AI.States
{
    public enum StateType
    {
        Idle,
        Patrol,
        Attack,
        Evade,
        Kite,
        Orbit,
        JinkEvade
    }

    public abstract partial class State
    {
        protected readonly Navigator navigator;
        protected readonly Gunner gunner;
        protected readonly UtilityTuning utilityTuning;
        protected readonly string stateName;
        public abstract StateType Type { get; }

        protected State(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            this.utilityTuning = utilityTuning;
            stateName = GetType().Name;
        }

        public UtilityTuning GetTuning() => utilityTuning;


        public virtual void Enter(Info ctx)
        {
            RLog.AI($"[{stateName}] Enter");
        }

        public abstract void Tick(Info ctx, float deltaTime);

        public virtual void Exit()
        {
            RLog.AI($"[{stateName}] Exit");
        }

        public abstract float ComputeUtility(Info ctx);

    }
} 