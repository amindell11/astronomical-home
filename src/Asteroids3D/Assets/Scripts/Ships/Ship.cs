using System;
using Combat;
using Combat.Targeting;
using Ships.Command;
using Ships.Damage;
using Ships.Movement;
using Ships.Weapons;
using Movement;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ships
{
    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(DamageController))]
    [DefaultExecutionOrder(-90)]
    public class Ship : MonoBehaviour, ITargetable, IShipStatus, IShooter
    {
        [Header("Frame & Modules")]
        [Tooltip("Frame archetype: chassis stats + references to the default engine/shield modules.")]
        [FormerlySerializedAs("settings")]
        public FrameSettings frame;

        [Tooltip("Optional engine override. Falls back to the frame's default engine when unset.")]
        public EngineModule engine;

        [Tooltip("Optional shield override. Falls back to the frame's default shield when unset.")]
        public ShieldModule shield;

        /// <summary>The flattened stats resolved from frame + modules. Null until settings populate.</summary>
        public ResolvedShipStats Stats { get; private set; }

        private EngineModule ResolvedEngine => engine ? engine : (frame ? frame.defaultEngine : null);
        private ShieldModule ResolvedShield => shield ? shield : (frame ? frame.defaultShield : null);

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

        private void Resolve()
        {
            Stats = frame ? ResolvedShipStats.Resolve(frame, ResolvedEngine, ResolvedShield) : null;
        }

        // Live inspector tuning: re-resolve and re-apply movement when any source SO changes. This
        // preserves the old ShipSettings.onSettingsChanged behaviour (which only re-applied movement).
        private void OnSettingsChanged()
        {
            Resolve();
            Movement?.PopulateSettings(Stats);
        }

        private FrameSettings subFrame;
        private EngineModule subEngine;
        private ShieldModule subShield;

        private void Subscribe()
        {
            Unsubscribe();
            subFrame = frame;
            subEngine = ResolvedEngine;
            subShield = ResolvedShield;
            if (subFrame) subFrame.onChanged.AddListener(OnSettingsChanged);
            if (subEngine) subEngine.onChanged.AddListener(OnSettingsChanged);
            if (subShield) subShield.onChanged.AddListener(OnSettingsChanged);
        }

        private void Unsubscribe()
        {
            if (subFrame) subFrame.onChanged.RemoveListener(OnSettingsChanged);
            if (subEngine) subEngine.onChanged.RemoveListener(OnSettingsChanged);
            if (subShield) subShield.onChanged.RemoveListener(OnSettingsChanged);
            subFrame = null;
            subEngine = null;
            subShield = null;
        }

        public virtual void Initialize(FrameSettings frameSettings, int team)
        {
            if (isInitialized) return;
            frame = frameSettings;
            teamNumber = team;
            Resolve();
            Movement.Initialize(Stats, ()=>KinematicsPoller.Kinematics);
            Damage?.PopulateSettings(Stats);
            Subscribe();

            if (Rigidbody) Rigidbody.ResetInertiaTensor();
            Dynamics = Stats.BuildDynamics(Rigidbody ? Rigidbody.inertiaTensor.z : 0f);

            if (Damage)
                Damage.OnDeath += (_, _) => HandleShipDeath();

            isInitialized = true;
            Commander?.Initialize(BuildShipControl());
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
