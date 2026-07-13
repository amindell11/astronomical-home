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

        /// <summary>Change via <see cref="Reequip"/>, not by writing the field.</summary>
        public EngineModule Engine => engine;

        /// <summary>Null if the ship carries no shield.</summary>
        public ShieldModule Shield => shield;

        public ResolvedShipStats Stats { get; private set; }

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber;

        public KinematicsPoller KinematicsPoller { get; private set; }
        public Commander Commander { get; private set; }
        public MovementController Movement { get; private set; }
        public DamageController Damage { get; private set; }

        /// <summary>Null if the ship is unarmed.</summary>
        public WeaponsController Weapons { get; private set; }

        /// <summary>Null if no mounted weapon carries a sensor; reads through the weapons controller so it stays current across reequips.</summary>
        public LockOnSensor Targeting => Weapons ? Weapons.Sensor : null;

        public Rigidbody Rigidbody { get; private set; }
        public ShipId Id { get; private set; }
        public int DecisionSeed { get; private set; }
        public Collider[] Colliders {get; private set;}
        public Dynamics Dynamics { get; private set; }
        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;
        public Rigidbody Body => Rigidbody;

        Transform IShipStatus.Transform => transform;
        public Kinematics Kinematics => Movement ? Movement.Kinematics : default;
        public float HealthPct => Damage ? Damage.Health.Pct : 1f;
        public float ShieldPct => Damage ? Damage.Shield.Pct : 1f;
        public bool BoostAvailable => Movement && Movement.BoostAvailable;
        public float BoostCooldownRemaining => Movement ? Movement.BoostCooldownRemaining : 0f;
        public float MaxSpeed => Stats != null ? Stats.maxSpeed : 0f;
        public float MaxYawRate => Stats != null ? Stats.maxYawRate : 0f;

        /// <summary>Collision radius derived from collider bounds in <see cref="Initialize"/>; 1 until then.</summary>
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

        /// <summary>Resolve stats without instantiating; shipRadius stays default until <see cref="Initialize"/> derives it from live colliders.</summary>
        public ResolvedShipStats ResolveStats() => ResolvedShipStats.Resolve(this, engine, shield);

        /// <summary>Between-run full-build swap (null modules are valid); after a weapon swap the caller must re-run <c>IUnitService.WireShipDependencies</c> to rewire world-scoped lock-sensor state.</summary>
        public void Reequip(EngineModule newEngine, ShieldModule newShield,
            WeaponComponent newPrimaryWeapon, WeaponComponent newSecondaryWeapon)
        {
            engine = newEngine;
            shield = newShield;

            if (Weapons)
                Weapons.Reequip(newPrimaryWeapon, newSecondaryWeapon);

            // Subscribe before the pre-Initialize early-return so onChanged tracks the new modules either way.
            Subscribe();
            if (!isInitialized) return;

            ReResolve(resetVitals: true);
        }

        // Module swaps don't change geometry, so the derived radius carries forward; resetVitals refills health/shield (equip swap yes, live inspector tuning no).
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

            // Flush transforms so collider bounds reflect the authored root scale before the radius is derived from them.
            Physics.SyncTransforms();

            if (Rigidbody) Rigidbody.ResetInertiaTensor();

            Stats.shipRadius = DeriveShipRadius();

            Dynamics = Stats.BuildDynamics(Rigidbody ? Rigidbody.inertiaTensor.z : 0f);

            if (Damage)
                Damage.OnDeath += (_, _) => HandleShipDeath();

            isInitialized = true;
            Commander?.Initialize(BuildShipControl());
        }

        // Extents-magnitude × 0.5 mirrors the single-collider radius convention in AI.Scout.
        private float DeriveShipRadius()
        {
            if (Colliders == null || Colliders.Length == 0)
                return Stats?.shipRadius ?? 1f;

            var bounds = Colliders[0].bounds;
            for (var i = 1; i < Colliders.Length; i++)
                bounds.Encapsulate(Colliders[i].bounds);
            return bounds.extents.magnitude * 0.5f;
        }

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
