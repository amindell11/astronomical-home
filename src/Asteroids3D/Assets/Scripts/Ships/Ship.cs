using System;
using Combat;
using Combat.Targeting;
using Combat.Weapons;
using Ships.Command;
using Ships.Damage;
using Ships.Movement;
using Ships.Weapons;
using Movement;
using UnityEngine;

namespace Ships
{
    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(DamageController))]
    [DefaultExecutionOrder(-90)]
    public class Ship : MonoBehaviour, ITargetable, IShipStatus, IShooter
    {
        [Header("Chassis")]
        [Tooltip("Ship mass (kg). Drives momentum and the physics inertia tensor.")]
        public float mass = 800f;

        [Tooltip("Lives before the ship is permanently destroyed.")]
        public int startingLives = 1;

        [Tooltip("Hull hit points.")]
        public float maxHealth = 100f;

        [Tooltip("Maximum roll (bank) angle, in degrees, entered while turning.")]
        public float maxBankAngle = 35f;

        [Header("Modules")]
        [Tooltip("Engine module: movement/handling stats. Required for a mobile ship.")]
        public EngineModule engine;

        [Tooltip("Shield module: shield capacity/regen. Null → the ship carries no shield.")]
        public ShieldModule shield;

        /// <summary>The currently-installed engine module. Change it via <see cref="Reequip"/>.</summary>
        public EngineModule Engine => engine;

        /// <summary>The currently-installed shield module, or null if the ship carries no shield.</summary>
        public ShieldModule Shield => shield;

        /// <summary>The flattened stats resolved from this ship's chassis + modules.</summary>
        public ResolvedShipStats Stats { get; private set; }

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber;

        public KinematicsPoller KinematicsPoller { get; private set; }
        public Commander Commander { get; private set; }
        public MovementController Movement { get; private set; }
        public DamageController Damage { get; private set; }

        /// <summary>The ship's weapons, or null if it carries none (peaceful ship).</summary>
        public WeaponsController Weapons { get; private set; }

        /// <summary>
        /// The ship's lock-on sensor, or null if no mounted weapon carries one. Reads through to
        /// the weapons controller, which owns the mounts and keeps this current across reequips.
        /// </summary>
        public LockOnSensor Targeting => Weapons ? Weapons.Sensor : null;

        public Rigidbody Rigidbody { get; private set; }
        public ShipId Id { get; private set; }
        public int DecisionSeed { get; private set; }
        public Collider[] Colliders {get; private set;}
        public Dynamics Dynamics { get; private set; }
        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        // ── IShipStatus: the narrow read view handed to commanders ──
        Transform IShipStatus.Transform => transform;
        public Kinematics Kinematics => Movement ? Movement.Kinematics : default;
        public float HealthPct => Damage ? Damage.Health.Pct : 1f;
        public float ShieldPct => Damage ? Damage.Shield.Pct : 1f;
        public bool BoostAvailable => Movement && Movement.BoostAvailable;
        public float BoostCooldownRemaining => Movement ? Movement.BoostCooldownRemaining : 0f;
        public float MaxSpeed => Stats != null ? Stats.maxSpeed : 0f;
        public float MaxYawRate => Stats != null ? Stats.maxYawRate : 0f;

        /// <summary>
        /// Collision radius derived at spawn from the ship's scaled collider bounds (see
        /// <see cref="Initialize"/>). 1 until the ship has resolved. Consumed by MPC obstacle
        /// inflation and the scanner.
        /// </summary>
        public float ShipRadius => Stats?.shipRadius ?? 1f;

        private bool isInitialized = false;

        protected virtual void Awake()
        {
            Id = new ShipId(GetInstanceID());
            KinematicsPoller = GetComponent<KinematicsPoller>();
            Movement         = GetComponent<MovementController>();
            Damage           = GetComponent<DamageController>();
            Colliders        = GetComponentsInChildren<Collider>();
            Rigidbody        = GetComponent<Rigidbody>();

            // Weapons are optional: a ship without a WeaponsController is simply unarmed.
            Weapons  = GetComponent<WeaponsController>();
            Weapons?.Initialize(() => Kinematics);
        }

        private void OnEnable() => PopulateSettings();

        private void OnDisable() => Unsubscribe();

        private void PopulateSettings()
        {
            Resolve();
            Movement?.PopulateSettings(Stats);
            Damage?.PopulateSettings(Stats);
            Subscribe();
        }

        private void Resolve() => Stats = ResolvedShipStats.Resolve(this, engine, shield);

        /// <summary>
        /// Resolve this ship's stats from its own chassis + modules without instantiating it. The
        /// collision radius stays at its default until <see cref="Initialize"/> derives it from the
        /// live colliders. Handy for editor tooling and tests that only need the stat block.
        /// </summary>
        public ResolvedShipStats ResolveStats() => ResolvedShipStats.Resolve(this, engine, shield);

        /// <summary>
        /// Swap this ship's full build — every first-class slot in one atomic apply — and re-resolve
        /// so every subsystem picks up the new modules. Engine/Shield are data modules (stat
        /// re-resolve); weapons are prefab modules (unchanged slots keep their mounts; see
        /// <see cref="Weapons.WeaponsController.Reequip"/>). A between-run operation, not a
        /// live-while-flying swap. Null modules are valid builds (no shield / empty weapon slot);
        /// an unarmed chassis (no WeaponsController) ignores the weapon slots. A weapon swap leaves
        /// world-scoped wiring (lock sensor registry) to the caller — re-run
        /// <c>IUnitService.WireShipDependencies</c> after applying.
        /// </summary>
        public void Reequip(EngineModule newEngine, ShieldModule newShield,
            WeaponComponent newPrimaryWeapon, WeaponComponent newSecondaryWeapon)
        {
            engine = newEngine;
            shield = newShield;

            if (Weapons)
                Weapons.Reequip(newPrimaryWeapon, newSecondaryWeapon);

            // Before Initialize there is nothing live to update — Initialize will resolve from these
            // pointers. Re-subscribe so onChanged tracks the new SOs even in that case.
            Subscribe();
            if (!isInitialized) return;

            ReResolve(resetVitals: true);
        }

        // Re-resolve stats from the current chassis + modules and push them to the live subsystems.
        // Geometry is unchanged by a module swap, so the derived collision radius carries forward.
        // resetVitals re-applies the damage settings (which refills health/shield) — true on an equip
        // swap (a between-run action), false on live inspector tuning so it doesn't heal on every tweak.
        private void ReResolve(bool resetVitals)
        {
            var radius = Stats?.shipRadius ?? 1f;
            Resolve();
            Stats.shipRadius = radius;
            Movement?.PopulateSettings(Stats);
            if (resetVitals)
                Damage?.PopulateSettings(Stats);
            if (isInitialized)
                Dynamics = Stats.BuildDynamics(Rigidbody ? Rigidbody.inertiaTensor.z : 0f);
        }

        // Live inspector tuning: re-resolve and re-apply when any source SO changes. Preserves the old
        // ShipSettings.onSettingsChanged behaviour (re-apply movement without disturbing vitals).
        private void OnSettingsChanged() => ReResolve(resetVitals: false);

        private EngineModule subEngine;
        private ShieldModule subShield;

        private void Subscribe()
        {
            Unsubscribe();
            subEngine = engine;
            subShield = shield;
            if (subEngine) subEngine.onChanged.AddListener(OnSettingsChanged);
            if (subShield) subShield.onChanged.AddListener(OnSettingsChanged);
        }

        private void Unsubscribe()
        {
            if (subEngine) subEngine.onChanged.RemoveListener(OnSettingsChanged);
            if (subShield) subShield.onChanged.RemoveListener(OnSettingsChanged);
            subEngine = null;
            subShield = null;
        }

        public virtual void Initialize(int team, int decisionSeed)
        {
            if (isInitialized) return;
            teamNumber = team;
            DecisionSeed = decisionSeed;
            Resolve();
            Movement.Initialize(Stats, ()=>KinematicsPoller.Kinematics);
            Damage?.PopulateSettings(Stats);
            Subscribe();

            // Physical size is the ship prefab's authored root scale — the colliders and the embedded
            // rig inherit it, so there is no runtime scaling to apply. Flush transforms so the collider
            // bounds reflect that scale before deriving the radius from them.
            Physics.SyncTransforms();

            if (Rigidbody) Rigidbody.ResetInertiaTensor();

            // shipRadius is DERIVED from the authored-scale collider bounds — a single source of truth
            // with no authored scalar to drift.
            Stats.shipRadius = DeriveShipRadius();

            Dynamics = Stats.BuildDynamics(Rigidbody ? Rigidbody.inertiaTensor.z : 0f);

            if (Damage)
                Damage.OnDeath += (_, _) => HandleShipDeath();

            isInitialized = true;
            Commander?.Initialize(BuildShipControl());
        }

        /// <summary>
        /// Derive the collision radius from the ship's own colliders' combined world bounds, evaluated
        /// at the prefab's authored root scale. Mirrors the single-collider precedent in
        /// <see cref="AI.Scout"/> (extents magnitude × 0.5). Falls back to the resolved default when the
        /// ship carries no colliders.
        /// </summary>
        private float DeriveShipRadius()
        {
            if (Colliders == null || Colliders.Length == 0)
                return Stats?.shipRadius ?? 1f;

            var bounds = Colliders[0].bounds;
            for (var i = 1; i < Colliders.Length; i++)
                bounds.Encapsulate(Colliders[i].bounds);
            return bounds.extents.magnitude * 0.5f;
        }

        /// <summary>
        /// Assembles the narrow control surface handed to the commander. Ships with a
        /// <see cref="WeaponsController"/> hand over the weapon context and actuator; unarmed
        /// ships hand over only movement, so a peaceful commander never sees a weapons surface.
        /// </summary>
        private ShipControl BuildShipControl() =>
            Weapons
                ? new(this, Movement, new SeedScope(DecisionSeed), Weapons.Context, Weapons)
                : new(this, Movement, new SeedScope(DecisionSeed));

        private void SetCommander(Commander commander)
        {
            Commander = commander;
            if (isInitialized && Commander != null)
                Commander.Initialize(BuildShipControl());
        }

        public void AddCommander(Commander commanderPrefab)
        {
            if (!commanderPrefab) return;
            if (Commander != null) throw new Exception("Commander already set");
            var instance = Instantiate(commanderPrefab, transform);
            SetCommander(instance);
        }

        /// <summary>
        /// Wire an already-existing commander instance (e.g. a pilot authored as a child of this
        /// ship in a sector prefab) without instantiating a copy. Used by the adopt pipeline.
        /// </summary>
        public void AdoptCommander(Commander commander)
        {
            if (!commander) return;
            if (Commander != null) throw new Exception("Commander already set");
            SetCommander(commander);
        }


        protected virtual void HandleShipDeath()
        {
            Weapons?.OnShipDeath();
            Lock.RaiseReleased();
            gameObject.SetActive(false);
        }

        public virtual void ResetShip()
        {
            Weapons?.ResetSystem();
            Movement.ResetMovement();
            Damage.ResetDamageState();
            gameObject.SetActive(true);
        }
    }
}
