using Game;
using Ships;
using Ships.Command;
using UnityEngine;
using State = Ships.Command.State;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scanning.Scout))]

    [DefaultExecutionOrder(-40)]
    public partial class AICommander : Commander
    {
        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        [Header("Debug")]
        [SerializeField] private Debug.AIDebugSettings debugSettings;
        public Debug.AIDebugSettings DebugSettings => debugSettings;

        protected Ship ship;
        protected IShipRegistry registry;
        protected bool systemsInitialized;

        public Scanning.Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        public virtual Gunner Gunner => null;
        public virtual Utility.UtilitySelector UtilitySelector => null;
        public virtual string CurrentStateName => "None";
        public bool HasRegistryConfigured => registry != null;

        protected virtual void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Scout = GetComponent<Scanning.Scout>();
        }

        public void SetRegistry(IShipRegistry shipRegistry)
        {
            registry = shipRegistry;
            TryInitializeSystems();
        }

        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
            TryInitializeSystems();
        }

        protected virtual void TryInitializeSystems()
        {
            if (systemsInitialized || !ship || registry == null)
                return;

            System.Func<State> stateProvider = () => ship.CurrentState;

            Scout.Initialize(ship.transform, ship.Id, ship.settings.Dynamics, stateProvider, registry);
            Navigator.Initialize(stateProvider, ship.settings.Dynamics, Scout);

            systemsInitialized = true;
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;
            GetSubCommands(ref cachedCommand);
        }

        protected virtual void GetSubCommands(ref Command command)
        {
            cachedCommand = Navigator.CurrentCommand;
        }
    }
}
