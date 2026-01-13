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
using AI.Utility;
using UtilitySelector = AI.Utility.UtilitySelector;

// Commander modules are now standalone; ShipMovement lives on the parent Ship object.
namespace Ships.Control
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Gunner))]
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
        private Navigator navigator;
        private Gunner gunner;
        private Info context;
        private UtilitySelector utilitySelector;
        private State currentState;
        public State CurrentState => currentState;
        public Navigator Navigator => navigator;
        public Gunner Gunner => gunner;
        public UtilitySelector UtilitySelector => utilitySelector;
        public string CurrentStateName => utilitySelector?.CurrentStateName ?? "None";

        public void Awake()
        {
            navigator = GetComponent<Navigator>();
            gunner = GetComponent<Gunner>();
            context = GetComponent<Info>();
            utilitySelector = GetComponent<UtilitySelector>();
        }
        
        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
            context.Initialize(ship, this, navigator, gunner);
            navigator.Initialize(ship, context.Sensors);
            gunner.Initialize(ship, context.Targeting);
        
            if (!utilityTuning)
            {
                Debug.LogWarning($"[AI] No AITuning assigned to {gameObject.name}. Using default values.", this);
                utilityTuning = ScriptableObject.CreateInstance<UtilityTuning>();
            }
        
            // Initialize the state machine with all states
            utilitySelector.Initialize(
                new Idle(navigator, gunner, utilityTuning),
                new Patrol(navigator, gunner, utilityTuning),
                new Evade(navigator, gunner, utilityTuning),
                new JinkEvade(navigator, gunner, utilityTuning),
                new Attack(navigator, gunner, utilityTuning),
                new Orbit(navigator, gunner, utilityTuning),
                new Kite(navigator, gunner, utilityTuning)
            );
        }

        private void FixedUpdate()
        {
            if (!ship || !utilitySelector) return;   
            currentState = ship.CurrentState;
        
            if (context)
            {
                utilitySelector.Tick(context, Time.fixedDeltaTime);
            }
        
            cachedCommand = GenerateCommand(currentState);
        }

        private Command GenerateCommand(State state)
        {
            var cmd = new Command();

            // --- Difficulty Level 1 (< 0.25): Stationary, no actions. ---
            if (difficulty < 0.25f) return cmd; // cmd defaults to zeros/false.
    

            navigator.GenerateNavCommands(state, ref cmd);

            if (difficulty < 0.5f) return cmd;

            gunner.GenerateGunnerCommands(state, ref cmd);

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
