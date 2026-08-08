using AI.Context;
using System;
using Game.Services;
using Movement;
using Movement.MPC;
using Ships.Command;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scout))]
    [RequireComponent(typeof(Brain))]

    // After the ML-Agents Academy stepper (0) — reads the RL boundary action same-tick; before MovementController (50).
    [DefaultExecutionOrder(10)]
    public class AICommander : Commander
    {
        private const uint NavStream = 1;

        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        [Header("Combat")]
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        protected ShipControl control;
        protected ArenaContext arena;
        protected bool systemsInitialized;

        // Per-slot trigger authority, latched from the last decision; the press edge derives from prevPrimaryHeld (PlayerCommander precedent).
        private FireControl primary;
        private FireControl secondary;
        private bool prevPrimaryHeld;

        internal AIContext context;

        public Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        // Gunner is optional: an unarmed (peaceful) ship has no Gunner component.
        public Gunner Gunner { get; private set; }
        public Brain Brain { get; private set; }

        protected virtual void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Scout = GetComponent<Scout>();
            Gunner = GetComponent<Gunner>();
            Brain = GetComponent<Brain>();
        }

        public void SetArena(ArenaContext arenaContext)
        {
            arena = arenaContext;
            TryInitializeSystems();
        }

        public override void Initialize(in ShipControl control)
        {
            this.control = control;
            TryInitializeSystems();
        }

        private void TryInitializeSystems()
        {
            if (systemsInitialized || control.Ship == null || arena == null)  return;
            var self = control.Ship;
            Func<Kinematics> pose = () => self.Kinematics;
            var seed = control.DecisionSeed;

            Scout.Initialize(self.Transform, self.Id, self.Dynamics, self, arena);
            Navigator.Initialize(self, self.Dynamics, Scout, seed.Derive(NavStream),
                control.Weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f);
            if (Gunner && control.IsArmed)
                Gunner.Initialize(control.Weapons, control.WeaponActuator, pose);

            context = new AIContext(self, Scout, combatExitDelay);

            systemsInitialized = true;
        }

        /// <summary>Resets the parts this commander composed, mirroring the Initialize cascade; RNG streams re-derive from the spawn seed.</summary>
        public override void ResetState()
        {
            if (!systemsInitialized) return;
            Scout.ResetState();
            Navigator.ResetState();
            if (Gunner) Gunner.ResetState();
            primary = default;
            secondary = default;
            prevPrimaryHeld = false;
            context = new AIContext(control.Ship, Scout, combatExitDelay);
            Brain.ResetState();
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;

            if (Brain && Brain.isActiveAndEnabled)
            {
                var dt = Time.fixedDeltaTime;
                context.Update(dt);
                Route(Brain.Decide(context, dt));
            }

            control.Pilot.Drive(Navigator.ComputeCommand());
            if (primary.IsCommanded) FireCommandedPrimary();
            if (Gunner) Gunner.Fire(primary, secondary);
        }

        /// <summary>Opens one decision and hands each lane to its own consumer; nothing consumes the decision whole.</summary>
        private void Route(BrainDecision? decision)
        {
            if (!decision.HasValue)
            {
                Navigator.ResetNavigation();
                // No decision hands the triggers back to the gunner, which keeps its last aim.
                primary = FireControl.Auto;
                secondary = FireControl.Auto;
                return;
            }

            var decided = decision.Value;
            Navigator.ApplyObjective(decided.nav);
            Navigator.CommandBoost(decided.boost);
            primary = decided.primary;
            secondary = decided.secondary;

            if (!Gunner) return;
            // A commanded trigger leaves the gunner's aim dormant rather than clearing it — nothing reads it while we own the slot.
            if (primary.IsCommanded || secondary.IsCommanded) return;
            if (primary.IsAuto || secondary.IsAuto)
            {
                if (decided.nav.TryGetAnchor(out var anchor)) Gunner.Aim(anchor);
                return;
            }
            Gunner.HoldFire();
        }

        // Raw trigger facts, PlayerCommander.FireSlot precedent: the weapon interprets its own firing semantics.
        private void FireCommandedPrimary()
        {
            var held = primary.Held;
            control.WeaponActuator.Fire(WeaponSlot.Primary,
                new WeaponCommand { held = held, pressed = held && !prevPrimaryHeld });
            prevPrimaryHeld = held;
        }
    }
}
