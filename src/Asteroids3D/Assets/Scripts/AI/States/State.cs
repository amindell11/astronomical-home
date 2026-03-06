using Movement;
using AI.Utility;
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

        /// <summary>
        /// The UtilityBuilder from the most recent ComputeUtility call.
        /// Use NewBuilder() in ComputeUtility to populate this automatically.
        /// </summary>
        public UtilityBuilder LastBuilder { get; private set; }

        protected State(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            this.utilityTuning = utilityTuning;
            stateName = GetType().Name;
        }

        /// <summary>
        /// Creates a new UtilityBuilder and stores it as LastBuilder for logging access.
        /// States should use this instead of <c>new UtilityBuilder()</c>.
        /// </summary>
        protected UtilityBuilder NewBuilder()
        {
            LastBuilder = new UtilityBuilder();
            return LastBuilder;
        }

        public virtual void Enter(Info ctx)
        {
        }

        public abstract void Tick(Info ctx, float deltaTime);

        public virtual void Exit()
        {
        }

        public abstract float ComputeUtility(Info ctx);
    }
} 