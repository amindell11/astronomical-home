using AI.Context;
using AI.Utility;
using System;
using Movement;
using Movement.MPC;
using Ships;
using Ships.Command;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scout))]
    [RequireComponent(typeof(Brain))]

    [DefaultExecutionOrder(-40)]
    public partial class AICommander : Commander
    {
        private const uint NavStream = 1;
        private const uint StrategyStream = 2;

        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        [Header("Combat")]
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        protected ShipControl control;
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

        public override void Initialize(in ShipControl control)
        {
            this.control = control;
            TryInitializeSystems();
        }

        private void TryInitializeSystems()
        {
            if (systemsInitialized || control.Ship == null || registry == null)  return;
            var self = control.Ship;
            Func<Kinematics> pose = () => self.Kinematics;
            var seed = control.DecisionSeed;

            Scout.Initialize(self.Transform, self.Id, self.Dynamics, self, registry);
            Navigator.Initialize(self, self.Dynamics, Scout, seed.Derive(NavStream));
            if (Gunner && control.IsArmed)
                Gunner.Initialize(control.Weapons, control.WeaponActuator, pose);

            context = new AIContext(self, Scout, combatExitDelay);

            Brain.Initialize(Navigator, Gunner, seed.Derive(StrategyStream));

            systemsInitialized = true;
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;

            if (Brain && Brain.isActiveAndEnabled)
            {
                var dt = Time.fixedDeltaTime;
                context.UpdateAssessment(dt);
                var intent = Brain.Decide(context, dt);
                Navigator.ApplyIntent(intent);
                if (Gunner) Gunner.ApplyIntent(intent);
            }

            control.Pilot.Drive(Navigator.ComputeCommand());
            if (Gunner) Gunner.Fire();
        }
    }
}
