using System;
using Combat;
using Combat.Targeting;
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

        /// <summary>The ship's lock-on sensor, or null if it carries no weapons.</summary>
        public LockOnSensor Targeting { get; private set; }

        public Rigidbody Rigidbody { get; private set; }
        public ShipId Id { get; private set; }
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
            Targeting = GetComponentInChildren<LockOnSensor>();
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

        // Live inspector tuning: re-resolve and re-apply movement when any source SO changes. This
        // preserves the old ShipSettings.onSettingsChanged behaviour (which only re-applied movement).
        private void OnSettingsChanged()
        {
            Resolve();
            Movement?.PopulateSettings(Stats);
        }

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

        public virtual void Initialize(int team)
        {
            if (isInitialized) return;
            teamNumber = team;
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
                ? new(this, Movement, Weapons.Context, Weapons)
                : new(this, Movement);

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
