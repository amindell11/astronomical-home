using AI.Context;
using AI.Gunnery;
using AI.Scanning;
using System;
using Movement;
using AI.Navigation.MPC;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Unity.Mathematics;
using UnityEngine;
using Ships.Registry;
using AI.Strategy;

namespace AI
{
    [RequireComponent(typeof(Navigator))]
    [RequireComponent(typeof(Scout))]

    // After the ML-Agents Academy stepper (0) — reads the RL boundary action same-tick; before MovementController (50).
    [DefaultExecutionOrder(10)]
    public class AICommander : Commander
    {
        private const uint NavStream = 1;
        private const uint GunnerStream = 2;

        [Header("Difficulty")]
        [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
        [Range(0f, 1f)] public float difficulty = 1.0f;

        [Header("Combat")]
        [Tooltip("Seconds after losing an enemy before exiting combat state.")]
        [SerializeField] private float combatExitDelay = 3f;

        [Header("Gunnery")]
        [Tooltip("How well this pilot's Gunner sees its target. An armed pilot without one fails at spawn.")]
        [SerializeField] internal AimSettings aimSettings;

        protected ShipControl control;
        protected IShipRegistry registry;
        protected IObstacleField field;
        private bool sensingWired;
        protected bool systemsInitialized;

        // Per-slot engage gates, latched from the last decision; the Gunner owns trigger timing.
        private bool engagePrimary;
        private bool engageSecondary;
        // The ability lane, latched beside the triggers.
        private bool boost;
        private bool hasAnchor;
        private ShipId anchorId;
        private EnemyTarget anchorTarget;

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

        /// <summary>What this ship senses: the registry it scans for ships and the obstacle field it scans for rocks (null for none). Set by the unit service at wiring, before the systems initialise.</summary>
        public void SetSensing(IShipRegistry shipRegistry, IObstacleField obstacleField)
        {
            registry = shipRegistry ?? throw new ArgumentNullException(nameof(shipRegistry));
            field = obstacleField;
            sensingWired = true;
            TryInitializeSystems();
        }

        public override void Initialize(in ShipControl control)
        {
            this.control = control;
            TryInitializeSystems();
        }

        private void TryInitializeSystems()
        {
            if (systemsInitialized || control.Ship == null || !sensingWired) return;
            var self = control.Ship;
            Func<Kinematics> pose = () => self.Kinematics;
            var seed = control.DecisionSeed;

            // Dynamics is captured by value: an AI ship cannot live-re-equip.
            Scout.Initialize(self.Transform, self.Id, PeakDynamics(self), registry, field, control.Weapons);
            Navigator.Initialize(self, self.Dynamics, Scout, seed.Derive(NavStream),
                control.Weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f);
            if (Gunner && control.IsArmed)
                Gunner.Initialize(control.Weapons, control.WeaponActuator, pose, aimSettings,
                    seed.Derive(GunnerStream));

            context = new AIContext(self, Scout, PrimaryHoldRange(), combatExitDelay);

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
            hasAnchor = false;
            context = new AIContext(control.Ship, Scout, PrimaryHoldRange(), combatExitDelay);
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

            var keystone = control.Keystone;
            var seeking = keystone && hasAnchor && keystone.SeeksContact;
            Navigator.ExemptFromAvoidance(seeking ? anchorId : ShipId.Invalid);
            // A ram flies the navigator's heading; the aim servo would yaw the dash off the sight.
            var command = Navigator.ComputeCommand(keystone && keystone.FliesNavigatorHeading || !Gunner
                ? AimReference.None
                : Gunner.PrimaryAimReference());
            if (seeking) command.thrust = 1f;
            command.abilityTarget = Navigator.PlanEnd;
            command.boost = boost || SightFires(keystone) ? 1f : 0f;
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
                hasAnchor = false;
                return;
            }

            var decided = decision.Value;
            var nav = decided.nav;
            var seat1 = ResolveRock(nav.rockSeat1);
            var seat2 = ResolveRock(nav.rockSeat2);
            var seat3 = ResolveRock(nav.rockSeat3);
            Navigator.ApplyObjective(nav, anchor, seat1, seat2, seat3);
            engagePrimary = decided.engagePrimary;
            engageSecondary = decided.engageSecondary;
            boost = decided.boost;
            hasAnchor = nav.TryGetAnchorId(out anchorId);
            anchorTarget = anchor;

            if (!Gunner) return;
            var aim = nav.sentence.aim;
            if (aim.armed && aim.referent != 0)
            {
                // A dead rock holds fire until the next decision; its nav slot is already weight 0.
                var snapshot = aim.referent switch
                {
                    1 => seat1,
                    2 => seat2,
                    3 => seat3,
                    _ => default,
                };
                if (snapshot.valid)
                    Gunner.Aim(new Vector2(snapshot.pos.x, snapshot.pos.y), new Vector2(snapshot.vel.x, snapshot.vel.y));
                else
                    Gunner.HoldFire();
            }
            else if (nav.hasAnchor) Gunner.Aim(anchor);
            else Gunner.HoldFire();
        }

        // The sight only adds an activation for an engaged commander; it never vetoes the brain's.
        private bool SightFires(Keystone keystone) =>
            keystone && hasAnchor && (engagePrimary || engageSecondary)
            && keystone.ShouldActivate(new KeystoneContext(control.Ship.Kinematics, control.Ship.Dynamics.mass, control.Ship.Dynamics.maxSpeed, anchorTarget.kinematics,
                Desired(Navigator.CostVelocityReference), Navigator.PlanEnd));

        private float? PrimaryHoldRange() => control.Weapons?.HoldRange(WeaponSlot.Primary);

        // The scout sizes its look-ahead once, so it must cover the keystone's raised cap.
        private Dynamics PeakDynamics(IShipStatus self) =>
            control.Keystone ? self.Dynamics.Scaled(control.Keystone.PeakSpeedScale, control.Keystone.PeakThrustScale) : self.Dynamics;

        private static Vector2 Desired(float2 velocity) => new(velocity.x, velocity.y);

        /// <summary>A registry miss is "target gone" observed a tick early — callers take the no-decision path.</summary>
        private bool TryResolveAnchor(in NavObjective nav, out EnemyTarget anchor)
        {
            anchor = default;
            if (!nav.TryGetAnchorId(out var id)) return true;
            if (!Scout.Registry.TryGetShip(id, out var ship) || !ship) return false;

            anchor = new EnemyTarget
            {
                kinematics = ship.Kinematics,
                dynamics = ship.Dynamics,
                id = id,
                boostCooldownRemaining = ship.BoostCooldownRemaining,
            };
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
