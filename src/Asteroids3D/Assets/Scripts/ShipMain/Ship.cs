using System.Collections.Generic;
using ShipMain.Control;
using MoveController = ShipMain.Movement.Controller;
using ShipMain.Visuals;
using UnityEngine;
using Weapons;

namespace ShipMain
{
    [RequireComponent(typeof(MoveController))]
    [RequireComponent(typeof(DamageHandler))]
    public class Ship : MonoBehaviour, ITargetable, IShooter
    {
        public static readonly List<Transform> ActiveShips = new();
        public static event System.Action<Ship, GameObject, float> OnGlobalShipDamaged; // victim, attacker, damage
    
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public Settings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber = 0;

        public MoveController Movement { get; internal set; }
        public LaserGun LaserGun { get; internal set; }
        public MissileLauncher MissileLauncher { get; internal set; }
        public DamageHandler DamageHandler { get; internal set; }
        public Hull Hull { get; internal set; }
        
        public ICommandSource Commander { get; internal set; }  

        private bool isInitialized = false;

        public State CurrentState { get; private set; }
        public Command CurrentCommand { get; internal set; }
        private bool HasValidCommand { get; set; } = false;

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;
        
        private void Start()
        {
            Initialize(settings, teamNumber);
        }
        
        public void Initialize(Settings shipSettings, int team)
        {
            if (isInitialized) return;
            FindComponents();
            settings = shipSettings;
            teamNumber = team;
            
            Commander?.InitializeCommander(this);
            PopulateSettings();

            if (DamageHandler)
                DamageHandler.OnDeath += (victim, killer) => HandleShipDeath();

            isInitialized = true;
        }
        private void FindComponents(){            
            Movement        = GetComponent<MoveController>();
            LaserGun        = GetComponentInChildren<LaserGun>();
            MissileLauncher = GetComponentInChildren<MissileLauncher>();
            DamageHandler   = GetComponent<DamageHandler>();
            Hull            = GetComponent<Hull>();
            Commander     =  GetComponentInChildren<Commander>();}
        private void OnEnable()
        {
            PopulateSettings();
            if (!ActiveShips.Contains(transform))
                ActiveShips.Add(transform);
        }

        private void OnDisable()
        {
            ActiveShips.Remove(transform);
        }

        private void OnDestroy()
        {
            ActiveShips.Remove(transform);
        }

        private void PopulateSettings()
        {
            Movement?.PopulateSettings(settings);
            DamageHandler?.PopulateSettings(settings);
        }

        internal static void BroadcastShipDamaged(Ship victim, GameObject attacker, float damage)
        {
            OnGlobalShipDamaged?.Invoke(victim, attacker, damage);
        }   
    
        private void HandleShipDeath()
        {
            Lock.Released?.Invoke();
            MissileLauncher.CancelLock();
            gameObject.SetActive(false);
        }

        public void ResetShip()
        {
            Movement.ResetMovement();
            LaserGun.ResetHeat();
            MissileLauncher.ReplenishAmmo();
            DamageHandler.ResetDamageState();
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (HasValidCommand)
            {
                if (Movement)
                    Movement.CurrentCommand = CurrentCommand;
                if (CurrentCommand.PrimaryFire && LaserGun)
                    LaserGun.Fire();
                if (CurrentCommand.SecondaryFire && MissileLauncher)
                    MissileLauncher.Fire();
            }
            HasValidCommand = false;
        }
        private void Update()
        {
            UpdateState();
            var cmd = CurrentCommand;
            HasValidCommand = Commander?.TryGetCommand(CurrentState, out cmd) ?? false;
            CurrentCommand = cmd;
        }

        private void UpdateState()
        {
            CurrentState = new State
            {
                Kinematics = Movement.Kinematics,
                IsLaserReady = LaserGun?.CanFire() ?? false,
                LaserHeatPct = LaserGun?.HeatPct ?? 0f,
                MissileState = MissileLauncher?.State ?? MissileLauncher.LockState.Idle,
                MissileAmmo = MissileLauncher?.AmmoCount ?? 0,
                HealthPct = DamageHandler.HealthPct,
                ShieldPct = DamageHandler.ShieldPct,
            };
        
        }

        public bool IsFriendly(Ship otherShip)
        {
            if (!otherShip) return false;
            return this.teamNumber == otherShip.teamNumber;
        }
    }
}