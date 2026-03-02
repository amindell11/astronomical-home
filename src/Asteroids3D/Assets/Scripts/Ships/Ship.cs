using System;
using Combat;
using Combat.Targeting;
using Game;
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
    [RequireComponent(typeof(WeaponsController))]
    [DefaultExecutionOrder(-90)]
    public class Ship : MonoBehaviour, ITargetable, IShooter
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
        public WeaponsController Weapons { get; private set; }
        public DamageController Damage { get; private set; }
        public TargetingComputer Targeting { get; private set; }
        public ShipId Id { get; private set; }
        public Collider[] Colliders {get; private set;}
        public IGamePlane Plane { get; private set; } = StaticGamePlaneAdapter.Instance;
        private bool isInitialized;

        public State CurrentState { get; private set; }
        public Command.Command CurrentCommand { get; private set; }

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        private void Awake()
        {
            Id = new ShipId(GetInstanceID());
            KinematicsPoller = GetComponent<KinematicsPoller>();
            Movement = GetComponent<MovementController>();
            Damage   = GetComponent<DamageController>();
            Weapons = GetComponent<WeaponsController>();
            Targeting = GetComponentInChildren<TargetingComputer>();
            Colliders = GetComponentsInChildren<Collider>();
        }
        
        private void OnEnable() => PopulateSettings();
        
        private void PopulateSettings()
        {            
            Movement?.PopulateSettings(settings);
            Damage?.PopulateSettings(settings);
        }
        
        public void SetPlane(IGamePlane plane)
        {
            Plane = plane ?? throw new ArgumentNullException(nameof(plane));
        }

        public void Initialize(ShipSettings shipSettings, int team)
        {
            if (isInitialized) return;
            settings = shipSettings;
            teamNumber = team;

            KinematicsPoller?.SetPlane(Plane);
            Movement.Initialize(settings, ()=>KinematicsPoller.Kinematics, Plane);
            Commander?.InitializeCommander(this);
            Damage?.PopulateSettings(settings);

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

     
        private void HandleShipDeath()
        {
            Lock.RaiseReleased();
            Weapons?.OnShipDeath();
            gameObject.SetActive(false);
        }

        public void ResetShip()
        {
            Movement.ResetMovement();
            Weapons?.ResetSystem();
            Damage.ResetDamageState();
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            UpdateState();
            TryGetCommand();
            ExecuteCommand();
        }

        private void ExecuteCommand()
        {
            if (CurrentCommand.primaryFire)
                Weapons?.FirePrimary();
            if (CurrentCommand.secondaryFire)
                Weapons?.FireSecondary();
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

        private void UpdateState()
        {
            CurrentState = new State
            {
                kinematics = Movement.Kinematics,
                isPrimaryReady = Weapons?.Primary?.CanFire() ?? false,
                isSecondaryReady = Weapons?.Secondary?.CanFire() ?? false,
                healthPct = Damage.Health.Pct,
                shieldPct = Damage.Shield.Pct,
            };
        }
    }
}
