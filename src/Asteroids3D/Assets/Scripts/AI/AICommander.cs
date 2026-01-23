using AI.Steering;
using AI.Utility;
using Ships;
using Ships.Control;
using UnityEngine;
using Attack = AI.States.Attack;
using Info = AI.Context.Info;
using Evade = AI.States.Evade;
using Idle = AI.States.Idle;
using JinkEvade = AI.States.JinkEvade;
using Kite = AI.States.Kite;
using Orbit = AI.States.Orbit;
using Patrol = AI.States.Patrol;
using UtilitySelector = AI.Utility.UtilitySelector;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Gunner))]
    [RequireComponent(typeof(Scout))]
    [RequireComponent(typeof(UtilitySelector))]
    public partial class AICommander : Commander
    {
        [Header("AI Configuration")]
        [Tooltip("AI tuning parameters (distances, bonuses, thresholds, etc.)")]
        [SerializeField] private UtilityTuning utilityTuning;
        
        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        private Ship ship;
        private Info context;
        public  Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        public Gunner Gunner { get; private set; }
        public UtilitySelector UtilitySelector { get; private set; }
        public string CurrentStateName => UtilitySelector?.CurrentStateName ?? "None";

        public void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Gunner = GetComponent<Gunner>();
            Scout = GetComponent<Scout>();
            UtilitySelector = GetComponent<UtilitySelector>();
        }
        
        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
            
            var shipInfo = new AI.Context.ShipInfo(ship);
            var targeting = new AI.Computers.Targeting(ship, shipInfo);
            var maneuvers = new Maneuvers(shipInfo);

            System.Func<State> stateProvider = () => ship.CurrentState;
            
            Scout.Initialize(ship);
            Navigator.Initialize(stateProvider, ship.settings.Dynamics, Scout);
            Gunner.Initialize(ship.Weapons.Primary, ship.Weapons.Secondary, targeting, stateProvider);
            
            context = new Info(ship, Navigator, Gunner, Scout, targeting, maneuvers);
        
            if (!utilityTuning)
                utilityTuning = ScriptableObject.CreateInstance<UtilityTuning>();
        
            UtilitySelector.Initialize(
                //new Idle(Navigator, Gunner, utilityTuning),
                //new Patrol(Navigator, Gunner, utilityTuning),
                //new Evade(Navigator, Gunner, utilityTuning),
               // new JinkEvade(Navigator, Gunner, utilityTuning),
                new Attack(Navigator, Gunner, utilityTuning)
               // new Orbit(Navigator, Gunner, utilityTuning),
               // new Kite(Navigator, Gunner, utilityTuning)
            );
        }

        private void FixedUpdate()
        {
            if (!ship || !UtilitySelector) return;   
            UtilitySelector.Tick(context, Time.fixedDeltaTime);
            GetSubCommands(ref cachedCommand);
        }

        private void GetSubCommands(ref Command command)
        {
            cachedCommand = Navigator.CurrentCommand;
            
            var gunCmd = Gunner.CurrentCommand;
            cachedCommand.PrimaryFire = gunCmd.PrimaryFire; 
            cachedCommand.SecondaryFire = gunCmd.SecondaryFire;
        }
    }
}
