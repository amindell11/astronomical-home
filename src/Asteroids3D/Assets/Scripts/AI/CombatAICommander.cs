using Movement;
using AI.Utility;
using Combat;
using Ships;
using Ships.Command;
using UnityEngine;
using AI.States;
using Attack = AI.States.Attack;
using Info = AI.Context.Info;
using State = Ships.Command.State;

namespace AI
{
    [RequireComponent(typeof(Gunner))]
    [RequireComponent(typeof(UtilitySelector))]
    public class CombatAICommander : AICommander
    {
        [Header("AI Configuration")]
        [Tooltip("AI tuning parameters (distances, bonuses, thresholds, etc.)")]
        [SerializeField] private UtilityTuning utilityTuning;

        [Header("Combat")]
        [SerializeField] private CombatTuning combatTuning;

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
            var maneuvers = new Maneuvers(shipInfo);

            System.Func<State> stateProvider = () => ship.CurrentState;

            if (!utilityTuning)
                utilityTuning = ScriptableObject.CreateInstance<UtilityTuning>();

            Scout.Initialize(ship.transform, ship.Id, ship.Dynamics, stateProvider, registry);
            Navigator.Initialize(stateProvider, ship.Dynamics, Scout);

            var combatShip = ship as CombatShip;
            if (combatShip)
            {
                gunner.Initialize(combatShip.Weapons.Primary, combatShip.Weapons.Secondary, targeting, stateProvider);
            }

            context = new Info(ship, Navigator, gunner, Scout, targeting, maneuvers,
                utilityTuning.combatExitDelay);

            utilitySelector.Initialize(utilityTuning,
                new Attack(Navigator, gunner, utilityTuning),
                new Evade(Navigator, gunner, utilityTuning),
                new Patrol(Navigator, gunner, utilityTuning)
            );

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
