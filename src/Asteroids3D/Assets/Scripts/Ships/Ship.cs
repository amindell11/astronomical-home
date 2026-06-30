using System;
using Combat.Targeting;
using Ships.Command;
using Ships.Damage;
using Ships.Movement;
using Movement;
using UnityEngine;

namespace Ships
{
    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(DamageController))]
    [DefaultExecutionOrder(-90)]
    public class Ship : MonoBehaviour, ITargetable, IShipStatus
    {
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public ShipSettings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber;

        public KinematicsPoller KinematicsPoller { get; private set; }
        public Commander Commander { get; private set; }
        public MovementController Movement { get; private set; }
        public DamageController Damage { get; private set; }
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
        public float MaxSpeed => settings ? settings.maxSpeed : 0f;
        public float MaxYawRate => settings ? settings.maxYawRate : 0f;

        private bool isInitialized = false;

        protected virtual void Awake()
        {
            Id = new ShipId(GetInstanceID());
            KinematicsPoller = GetComponent<KinematicsPoller>();
            Movement         = GetComponent<MovementController>();
            Damage           = GetComponent<DamageController>();
            Colliders        = GetComponentsInChildren<Collider>();
            Rigidbody        = GetComponent<Rigidbody>();
        }

        private void OnEnable() => PopulateSettings();

        private void PopulateSettings()
        {
            Movement?.PopulateSettings(settings);
            Damage?.PopulateSettings(settings);
        }

        public virtual void Initialize(ShipSettings shipSettings, int team)
        {
            if (isInitialized) return;
            settings = shipSettings;
            teamNumber = team;
            Movement.Initialize(settings, ()=>KinematicsPoller.Kinematics);
            Damage?.PopulateSettings(settings);

            if (Rigidbody) Rigidbody.ResetInertiaTensor();
            Dynamics = settings.BuildDynamics(Rigidbody ? Rigidbody.inertiaTensor.z : 0f);

            if (Damage)
                Damage.OnDeath += (_, _) => HandleShipDeath();

            isInitialized = true;
            Commander?.Initialize(BuildShipControl());
        }

        /// <summary>
        /// Assembles the narrow control surface handed to the commander. The base ship is unarmed;
        /// <see cref="CombatShip"/> overrides this to add the weapon context and actuator.
        /// </summary>
        protected virtual ShipControl BuildShipControl() => new(this, Movement);

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
            Lock.RaiseReleased();
            gameObject.SetActive(false);
        }

        public virtual void ResetShip()
        {
            Movement.ResetMovement();
            Damage.ResetDamageState();
            gameObject.SetActive(true);
        }
    }
}
