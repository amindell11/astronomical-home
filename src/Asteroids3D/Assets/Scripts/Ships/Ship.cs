using System;
using Combat;
using Combat.Targeting;
using Ships.Command;
using Ships.Damage;
using Ships.Movement;

using UnityEngine;
using Ships.Weapons;
namespace Ships
{
    [RequireComponent(typeof(Movement.MovementController))]
    [RequireComponent(typeof(Damage.DamageController))]
    [RequireComponent(typeof(Weapons.WeaponsController))]
    [DefaultExecutionOrder(-90)]
    public class Ship : MonoBehaviour, ITargetable, IShooter
    {
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public Settings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber = 0;
        
        public StatePoller StatePoller { get; private set; }
        public ICommandSource Commander { get; private set; }
        public MovementController Movement { get; private set; }
        public WeaponsController Weapons { get; private set; }
        public DamageController Damage { get; private set; }

        private bool isInitialized = false;

        public State CurrentState { get; private set; }
        public Command.Command CurrentCommand { get; private set; }

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        private void Awake()
        { 
            StatePoller = GetComponent<StatePoller>();
            Movement = GetComponent<MovementController>();
            Damage   = GetComponent<DamageController>();
            Weapons = GetComponent<WeaponsController>();
        }
        
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
            Movement.Initialize(settings, ()=>StatePoller.Kinematics);
            Damage?.PopulateSettings(settings);

            if (Damage)
                Damage.OnDeath += (victim, killer) => HandleShipDeath();

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
            UpdateState();
            // Refresh command just before MovementController pulls it at Order 50
            var cmd = CurrentCommand;
            if (Commander != null && Commander.TryGetCommand(CurrentState, out cmd))
            {
                CurrentCommand = cmd;
            }
        }

        private void Update()
        {
            var cmd = CurrentCommand;
            if (Commander != null && Commander.TryGetCommand(CurrentState, out cmd))
            {
                CurrentCommand = cmd;
            }
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

        public bool IsFriendly(Ship otherShip) => otherShip && otherShip.teamNumber == teamNumber;
        public bool IsHostile(Ship otherShip) => !IsFriendly(otherShip);

    }
}