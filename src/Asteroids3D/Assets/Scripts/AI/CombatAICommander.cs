using Movement;
using AI.Utility;
using Combat;
using Ships;
using Ships.Command;
using UnityEngine;
using AI.States;
using Info = AI.Context.Info;
using State = Ships.Command.State;

namespace AI
{
    [RequireComponent(typeof(Gunner))]
    [RequireComponent(typeof(UtilitySelector))]
    public partial class CombatAICommander : AICommander
    {
        [Header("State Profiles")]
        [Tooltip("Data-driven state profiles that define available AI states.")]
        [SerializeField] private StateProfile[] stateProfiles;

        [Header("Combat")]
        [SerializeField] private CombatTuning combatTuning;
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        private Gunner gunner;
        private UtilitySelector utilitySelector;
        private Info context;

        public override Gunner Gunner => gunner;
        public override UtilitySelector UtilitySelector => utilitySelector;
        public override string CurrentStateName => utilitySelector?.CurrentStateName ?? "None";

        protected override void Awake()
        {
            base.Awake();
            gunner = GetComponent<Gunner>();
            utilitySelector = GetComponent<UtilitySelector>();
        }

        protected override void TryInitializeSystems()
        {
            if (systemsInitialized || !ship || registry == null)
                return;

            var shipInfo = new AI.Context.ShipInfo(ship);
            var targeting = new TargetingUtils(shipInfo, combatTuning);

            System.Func<State> stateProvider = () => ship.CurrentState;

            Scout.Initialize(ship.transform, ship.Id, ship.Dynamics, stateProvider, registry);
            Navigator.Initialize(stateProvider, ship.Dynamics, Scout);

            var combatShip = ship as CombatShip;
            if (combatShip)
            {
                gunner.Initialize(combatShip.Weapons.Primary, combatShip.Weapons.Secondary, targeting, stateProvider);
            }

            context = new Info(ship, Navigator, gunner, Scout, targeting,
                combatExitDelay);

            var states = new AI.States.State[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
                states[i] = new AIState(stateProfiles[i], Navigator, gunner);
            utilitySelector.Initialize(states);

            systemsInitialized = true;
        }

        protected override void FixedUpdate()
        {
            if (!systemsInitialized) return;
            if (utilitySelector && utilitySelector.isActiveAndEnabled)
            {
                context.UpdateAssessment();
                utilitySelector.Tick(context, Time.fixedDeltaTime);
            }
            GetSubCommands(ref cachedCommand);
        }

        protected override void GetSubCommands(ref Command command)
        {
            cachedCommand = Navigator.CurrentCommand;

            var gunCmd = gunner.CurrentCommand;
            cachedCommand.primaryFire = gunCmd.primaryFire;
            cachedCommand.secondaryFire = gunCmd.secondaryFire;
        }
    }
}
