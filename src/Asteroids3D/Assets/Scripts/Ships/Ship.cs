using System;
using Ships.Control;
using Ships.Damage;
using Ships.Movement;
using Ships.Visuals;
using Weapons;
using UnityEngine;
using Ships.Weapons;
namespace Ships
{
    [RequireComponent(typeof(Movement.MovementController))]
    [RequireComponent(typeof(Damage.DamageController))]
    [RequireComponent(typeof(Weapons.WeaponsController))]

    public class Ship : MonoBehaviour, ITargetable, IShooter
    {
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public Settings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber = 0;
        
        public ICommandSource Commander { get; private set; }  
        public Movement.MovementController Movement { get; internal set; }
        public Weapons.WeaponsController Weapons { get; private set; }
        public Damage.DamageController Damage { get; internal set; }

        private bool isInitialized = false;

        public State CurrentState { get; private set; }
        public Command CurrentCommand { get; internal set; }
        private bool HasValidCommand { get; set; } = false;

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        private void Awake() => FindComponents(); 
        private void OnEnable() => PopulateSettings();
        
        private void PopulateSettings()
        {            
            Movement?.PopulateSettings(settings);
            Damage?.PopulateSettings(settings);
        }
        
        public void Initialize(Settings shipSettings, int team)
        {
            if (isInitialized) return;
            settings = shipSettings;
            teamNumber = team;
            Commander?.InitializeCommander(this);
            PopulateSettings();

            if (Damage)
                Damage.OnDeath += (victim, killer) => HandleShipDeath();

            isInitialized = true;
        }
        private void FindComponents(){            
            Movement = GetComponent<Movement.MovementController>();
            Damage   = GetComponent<Damage.DamageController>();
            Weapons = GetComponent<Weapons.WeaponsController>();
        }

        private void SetCommander(ICommandSource commander)
        {
            Commander = commander;
            if (isInitialized && Commander !=null)
                Commander.InitializeCommander(this);
        }

        public Commander AddCommander(Commander commanderPrefab)
        {
            if (!commanderPrefab) return null;
            if (Commander !=null ) throw new Exception("Commander already set");
            var instance = Instantiate(commanderPrefab, transform);
            SetCommander(instance);
            return instance;
        }

     
        private void HandleShipDeath()
        {
            Lock.Released?.Invoke();
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
            if (HasValidCommand)
            {
                if (Movement)
                    Movement.CurrentCommand = CurrentCommand;
                if (CurrentCommand.PrimaryFire && Weapons)
                    Weapons.FirePrimary();
                if (CurrentCommand.SecondaryFire && Weapons)
                    Weapons.FireSecondary();
            }
            HasValidCommand = false;
        }
        private void Update()
        {
            UpdateState();
            var cmd = CurrentCommand;
            HasValidCommand = Commander?.TryGetCommand(CurrentState, out cmd) ?? false;
            if(HasValidCommand) CurrentCommand = cmd;
        }

        private void UpdateState()
        {
            CurrentState = new State
            {
                Kinematics = Movement.Kinematics,
                IsPrimaryReady = Weapons?.Primary?.CanFire() ?? false,
                IsSecondaryReady = Weapons?.Secondary?.CanFire() ?? false,
                HealthPct = Damage.Health.Pct,
                ShieldPct = Damage.Shield.Pct,
            };
        }

        public bool IsFriendly(Ship otherShip)
        {
            if (!otherShip) return false;
            return this.teamNumber == otherShip.teamNumber;
        }
    }
}