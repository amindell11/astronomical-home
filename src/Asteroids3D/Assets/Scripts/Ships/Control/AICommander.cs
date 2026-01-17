using AI.Steering;
using Game;
using UnityEngine;
using Attack = AI.States.Attack;
using Info = AI.Context.Info;
using Evade = AI.States.Evade;
using Idle = AI.States.Idle;
using JinkEvade = AI.States.JinkEvade;
using Kite = AI.States.Kite;
using Orbit = AI.States.Orbit;
using Patrol = AI.States.Patrol;
using AI;
using AI.Computers;
using AI.Utility;
using UtilitySelector = AI.Utility.UtilitySelector;

// Commander modules are now standalone; ShipMovement lives on the parent Ship object.
namespace Ships.Control
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Gunner))]
    [RequireComponent(typeof(Sensors))]
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
        public  Sensors Sensors { get; private set; }
        public State CurrentState { get; private set; }
        public Navigator Navigator { get; private set; }
        public Gunner Gunner { get; private set; }
        public UtilitySelector UtilitySelector { get; private set; }
        public string CurrentStateName => UtilitySelector?.CurrentStateName ?? "None";

        public void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Gunner = GetComponent<Gunner>();
            Sensors = GetComponent<Sensors>();
            UtilitySelector = GetComponent<UtilitySelector>();
        }
        
        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
            
            // Create context dependencies
            var shipInfo = new AI.Context.ShipInfo(ship);
            var targeting = new AI.Computers.Targeting(ship, shipInfo);
            var maneuvers = new AI.Computers.Maneuvers(shipInfo);
            
            // Initialize components
            Sensors.Initialize(ship, shipInfo);
            Navigator.Initialize(ship, Sensors);
            Gunner.Initialize(ship, targeting);
            
            // Create Info with all dependencies
            context = new Info(ship, Navigator, Gunner, Sensors, targeting, maneuvers);
        
            if (!utilityTuning)
            {
                utilityTuning = ScriptableObject.CreateInstance<UtilityTuning>();
            }
        
            // Initialize the state machine with all states
            UtilitySelector.Initialize(
                new Idle(Navigator, Gunner, utilityTuning),
                new Patrol(Navigator, Gunner, utilityTuning),
                new Evade(Navigator, Gunner, utilityTuning),
                new JinkEvade(Navigator, Gunner, utilityTuning),
                new Attack(Navigator, Gunner, utilityTuning),
                new Orbit(Navigator, Gunner, utilityTuning),
                new Kite(Navigator, Gunner, utilityTuning)
            );
        }

        private void FixedUpdate()
        {
            if (!ship || !UtilitySelector) return;   
            CurrentState = ship.CurrentState;
            UtilitySelector.Tick(context, Time.fixedDeltaTime);
            cachedCommand = GenerateCommand(CurrentState);
        }

        private Command GenerateCommand(State state)
        {
            var cmd = new Command();

            // --- Difficulty Level 1 (< 0.25): Stationary, no actions. ---
            if (difficulty < 0.25f) return cmd; // cmd defaults to zeros/false.
    

            Navigator.GenerateNavCommands(state, ref cmd);

            if (difficulty < 0.5f) return cmd;

            Gunner.GenerateGunnerCommands(state, ref cmd);

            // Level 3 (< 0.75): Lasers only, no missiles.
            if (!(difficulty < 0.75f)) return cmd;
            if (cmd.SecondaryFire) // Only log if we are actually disabling it
            {
                cmd.SecondaryFire = false;
            }
            return cmd;
        }
    }
}
