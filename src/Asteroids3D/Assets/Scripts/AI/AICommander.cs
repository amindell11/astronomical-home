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
    [RequireComponent(typeof(Scout))]
    [RequireComponent(typeof(Brain))]

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
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        protected Ship ship;
        protected IShipRegistry registry;
        protected bool systemsInitialized;

        private AIContext context;

        public Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        // Gunner is optional: an unarmed (peaceful) ship has no Gunner component.
        public Gunner Gunner { get; private set; }
        public Brain Brain { get; private set; }
        // Editor/diagnostics convenience: the active policy when it is the utility chooser.
        public UtilityChooser UtilityChooser => Brain ? Brain.Chooser as UtilityChooser : null;
        public string CurrentStateName => UtilityChooser?.CurrentStateName ?? "None";
        public bool HasRegistryConfigured => registry != null;

        protected virtual void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Scout = GetComponent<Scout>();
            Gunner = GetComponent<Gunner>();
            Brain = GetComponent<Brain>();
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
            System.Func<Movement.Kinematics> pose = () => self.Kin;

            System.Func<State> stateProvider = () => ship.CurrentState;

            Scout.Initialize(ship.transform, ship.Id, ship.Dynamics, stateProvider, registry);
            Navigator.Initialize(stateProvider, ship.Dynamics, Scout);

            // Weapons are optional: only a CombatShip carrying a Gunner gets armed.
            if (Gunner && ship is CombatShip combatShip)
            {
                Gunner.Initialize(combatShip.Weapons.Primary, combatShip.Weapons.Secondary, pose, stateProvider);
            }

            context = new AIContext(self, Scout, combatExitDelay);

            var states = new AI.States.AIState[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
                states[i] = new AIState(stateProfiles[i], Navigator, Gunner);
            (Brain.Chooser as IStateChooser)?.Initialize(states);

            systemsInitialized = true;
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;

            // The brain decides; the commander actuates. A disabled Brain component
            // bypasses the loop (tests drive the Navigator directly this way).
            if (Brain && Brain.isActiveAndEnabled)
            {
                context.UpdateAssessment();
                var intent = Brain.Decide(context, Time.fixedDeltaTime);
                Navigator.ApplyIntent(intent);
                if (Gunner) Gunner.ApplyIntent(intent);
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
