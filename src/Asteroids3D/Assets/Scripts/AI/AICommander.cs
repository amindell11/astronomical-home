using AI.Context;
using AI.Utility;
using Combat;
using Movement.MPC;
using Ships;
using Ships.Command;
using UnityEngine;
using AI.States;
using State = Ships.Command.State;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scanning.Scout))]
    [RequireComponent(typeof(Utility.UtilitySelector))]

    [DefaultExecutionOrder(-40)]
    public partial class AICommander : Commander
    {
        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        [Header("State Profiles")]
        [Tooltip("Data-driven state profiles that define available AI states.")]
        [SerializeField] private StateProfile[] stateProfiles;

        [Header("Combat")]
        [Tooltip("Optional. Leave empty (or omit the Gunner component) for a peaceful, unarmed AI.")]
        [SerializeField] private CombatTuning combatTuning;
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        protected Ship ship;
        protected IShipRegistry registry;
        protected bool systemsInitialized;

        private AIContext context;

        public Scanning.Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        // Gunner is optional: an unarmed (peaceful) ship has no Gunner component.
        public Gunner Gunner { get; private set; }
        public UtilitySelector UtilitySelector { get; private set; }
        public string CurrentStateName => UtilitySelector ? UtilitySelector.CurrentStateName : "None";
        public bool HasRegistryConfigured => registry != null;

        protected virtual void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Scout = GetComponent<Scanning.Scout>();
            Gunner = GetComponent<Gunner>();
            UtilitySelector = GetComponent<UtilitySelector>();
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

            var self = new AI.Context.SelfStatus(ship);
            var targeting = new TargetingUtils(self, combatTuning);

            System.Func<State> stateProvider = () => ship.CurrentState;

            Scout.Initialize(ship.transform, ship.Id, ship.Dynamics, stateProvider, registry);
            Navigator.Initialize(stateProvider, ship.Dynamics, Scout);

            // Weapons are optional: only a CombatShip carrying a Gunner gets armed.
            if (Gunner && ship is CombatShip combatShip)
            {
                Gunner.Initialize(combatShip.Weapons.Primary, combatShip.Weapons.Secondary, targeting, stateProvider);
            }

            context = new AIContext(self, Scout, targeting, combatExitDelay);

            var states = new AI.States.AIState[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
                states[i] = new AIState(stateProfiles[i], Navigator, Gunner);
            UtilitySelector.Initialize(states);

            systemsInitialized = true;
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;
            if (UtilitySelector && UtilitySelector.isActiveAndEnabled)
            {
                context.UpdateAssessment();
                UtilitySelector.Tick(context, Time.fixedDeltaTime);
            }
            GetSubCommands(ref cachedCommand);
        }

        protected virtual void GetSubCommands(ref Command command)
        {
            cachedCommand = Navigator.CurrentCommand;

            if (!Gunner) return;
            var gunCmd = Gunner.CurrentCommand;
            cachedCommand.primaryFire = gunCmd.primaryFire;
            cachedCommand.secondaryFire = gunCmd.secondaryFire;
        }
    }
}
