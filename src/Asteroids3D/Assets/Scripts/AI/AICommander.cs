using AI.Context;
using System;
using Game.Services;
using Movement;
using Movement.MPC;
using Ships.Command;
using Unity.Mathematics;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scout))]

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
        protected WorldHandle world;
        protected bool systemsInitialized;

        // Per-slot engage gates, latched from the last decision; the Gunner owns trigger timing.
        private bool engagePrimary;
        private bool engageSecondary;
        // The ability lane, latched beside the triggers.
        private bool boost;

        internal AIContext context;

        public Scout Scout { get; private set; }
        public Navigator Navigator { get; private set; }
        // Gunner is optional: an unarmed (peaceful) ship has no Gunner component.
        public Gunner Gunner { get; private set; }
        // Brain is optional too: the harness pilots author none and install one per episode.
        public Brain Brain { get; private set; }

        protected virtual void Awake()
        {
            Navigator = GetComponent<Navigator>();
            Scout = GetComponent<Scout>();
            Gunner = GetComponent<Gunner>();
            Brain = GetComponent<Brain>();
        }

        /// <summary>Swaps the decision component, retiring whatever was installed before. The sole install path: an AddComponent that went around it would leave a ship whose decisions nothing reads.</summary>
        internal T InstallBrain<T>() where T : Brain => (T)InstallBrain(host => host.AddComponent<T>());

        /// <summary>Install overload for callers whose brain type is only known at runtime — the archetype switch hands its own attach step in rather than reproducing the swap.</summary>
        internal Brain InstallBrain(Func<GameObject, Brain> attach)
        {
            if (Brain) DestroyImmediate(Brain);
            Brain = attach(gameObject);
            return Brain;
        }

        public void SetWorld(WorldHandle worldHandle)
        {
            world = worldHandle;
            TryInitializeSystems();
        }

        public override void Initialize(in ShipControl control)
        {
            this.control = control;
            TryInitializeSystems();
        }

        private void TryInitializeSystems()
        {
            if (systemsInitialized || control.Ship == null || world == null) return;
            var self = control.Ship;
            Func<Kinematics> pose = () => self.Kinematics;
            var seed = control.DecisionSeed;

            // Dynamics is captured by value: an AI ship cannot live-re-equip.
            Scout.Initialize(self.Transform, self.Id, self.Dynamics, self, world);
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
            engagePrimary = false;
            engageSecondary = false;
            boost = false;
            context = new AIContext(control.Ship, Scout, combatExitDelay);
            if (Brain) Brain.ResetState();
        }

        protected virtual void FixedUpdate()
        {
            if (!systemsInitialized) return;

            if (Brain && Brain.isActiveAndEnabled)
            {
                context.Update(Time.fixedDeltaTime);
                Route(Brain.Decide(context));
            }

            var command = Navigator.ComputeCommand();
            command.boost = boost ? 1f : 0f;
            control.Pilot.Drive(command);
            if (Gunner) Gunner.Fire(engagePrimary, engageSecondary);
        }

        /// <summary>Opens one decision and hands each lane to its own consumer; nothing consumes the decision whole. Every referent resolves once here — anchor and rock seats alike — so the nav and fire lanes cannot aim at different moments of the same tick, and a held decision re-resolves fresh each tick (extrapolation only ever bridges one tick).</summary>
        private void Route(BrainDecision? decision)
        {
            if (!decision.HasValue || !TryResolveAnchor(decision.Value.nav, out var anchor))
            {
                Navigator.ResetNavigation();
                // No decision leaves the gunner engaged on its own judgment, keeping its last aim.
                engagePrimary = true;
                engageSecondary = true;
                boost = false;
                return;
            }

            var decided = decision.Value;
            var nav = decided.nav;
            Navigator.ApplyObjective(nav, anchor,
                ResolveRock(nav.rockSeat1), ResolveRock(nav.rockSeat2), ResolveRock(nav.rockSeat3));
            engagePrimary = decided.engagePrimary;
            engageSecondary = decided.engageSecondary;
            boost = decided.boost;

            if (!Gunner) return;
            var aim = nav.sentence.aim;
            if (aim.armed && aim.referent != 0)
            {
                // A dead rock holds fire until the next decision; its nav slot is already weight 0.
                if (nav.RockSeat(aim.referent).TryResolve(out var rockPos, out var rockVel))
                    Gunner.Aim(rockPos, rockVel);
                else
                    Gunner.HoldFire();
            }
            else if (nav.hasAnchor) Gunner.Aim(anchor);
            else Gunner.HoldFire();
        }

        /// <summary>A registry miss is "target gone" observed a tick early — callers take the no-decision path.</summary>
        private bool TryResolveAnchor(in NavObjective nav, out EnemyTarget anchor)
        {
            anchor = default;
            if (!nav.TryGetAnchorId(out var id)) return true;
            if (!Scout.Registry.TryGetShip(id, out var ship) || !ship) return false;

            anchor = new EnemyTarget { kinematics = ship.Kinematics, dynamics = ship.Dynamics };
            return true;
        }

        /// <summary>A rock seat as the solver will see it this tick; a dead or unbound rock is an invalid snapshot, dropping its slots to weight 0 until the next decision. Rocks have no facing, so yaw stays 0.</summary>
        private static ReferentSnapshot ResolveRock(in AsteroidRef rock)
        {
            if (!rock.TryResolve(out var pos, out var vel)) return default;
            return new ReferentSnapshot
            {
                valid = true,
                pos = new float2(pos.x, pos.y),
                vel = new float2(vel.x, vel.y),
            };
        }
    }
}
