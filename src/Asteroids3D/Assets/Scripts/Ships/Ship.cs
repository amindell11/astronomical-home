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
    public class Ship : MonoBehaviour, ITargetable
    {
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public ShipSettings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber;

        public KinematicsPoller KinematicsPoller { get; private set; }
        public ICommandSource Commander { get; private set; }
        public MovementController Movement { get; private set; }
        public DamageController Damage { get; private set; }
        public ShipId Id { get; private set; }
        public Collider[] Colliders {get; private set;}
        public Dynamics Dynamics { get; private set; }
        private bool isInitialized;

        public State CurrentState { get; protected set; }
        public Command.Command CurrentCommand { get; private set; }

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        protected virtual void Awake()
        {
            Id = new ShipId(GetInstanceID());
            KinematicsPoller = GetComponent<KinematicsPoller>();
            Movement = GetComponent<MovementController>();
            Damage   = GetComponent<DamageController>();
            Colliders = GetComponentsInChildren<Collider>();
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
            Commander?.InitializeCommander(this);
            Movement.Initialize(settings, ()=>KinematicsPoller.Kinematics);
            Damage?.PopulateSettings(settings);

            var rb = GetComponent<Rigidbody>();
            if (rb) rb.ResetInertiaTensor();
            Dynamics = settings.BuildDynamics(rb ? rb.inertiaTensor.z : 0f);

            if (Damage)
                Damage.OnDeath += (_, _) => HandleShipDeath();

            isInitialized = true;
        }

        private void SetCommander(ICommandSource commander)
        {
            Commander = commander;
            if (isInitialized && Commander !=null)
                Commander.InitializeCommander(this);
        }

        public void AddCommander(Commander commanderPrefab)
        {
            if (!commanderPrefab) return;
            if (Commander !=null ) throw new Exception("Commander already set");
            var instance = Instantiate(commanderPrefab, transform);
            SetCommander(instance);
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

        private void FixedUpdate()
        {
            UpdateState();
            TryGetCommand();
            ExecuteCommand();
        }

        protected virtual void ExecuteCommand()
        {
        }

        private void Update()
        {
            TryGetCommand();
        }

        private void TryGetCommand()
        {
            if (Commander != null && Commander.TryGetCommand(CurrentState, out var cmd))
                CurrentCommand = cmd;
        }

        protected virtual void UpdateState()
        {
            CurrentState = new State
            {
                kinematics = Movement.Kinematics,
                healthPct = Damage.Health.Pct,
                shieldPct = Damage.Shield.Pct,
            };
        }
    }
}
