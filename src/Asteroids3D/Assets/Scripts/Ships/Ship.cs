using Ships.Control;
using Ships.Visuals;
using UnityEngine;
using Weapons;
using MoveController = Ships.Movement.Controller;

namespace Ships
{
    [RequireComponent(typeof(MoveController))]
    [RequireComponent(typeof(Damage))]
    public class Ship : MonoBehaviour, ITargetable, IShooter
    {
        [Header("Settings Asset")]
        [Tooltip("ShipSettings asset that holds all tunable parameters.")]
        public Settings settings;

        [Header("Team Settings")]
        [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
        public int teamNumber = 0;

        public MoveController Movement { get; internal set; }
        public LaserGun LaserGun { get; internal set; }
        public MissileLauncher MissileLauncher { get; internal set; }
        public Damage Damage { get; internal set; }
        public ICommandSource Commander { get; internal set; }  

        private bool isInitialized = false;

        public State CurrentState { get; private set; }
        public Command CurrentCommand { get; internal set; }
        private bool HasValidCommand { get; set; } = false;

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;

        private void Awake()
        {
            Movement        = GetComponent<MoveController>();
            LaserGun        = GetComponentInChildren<LaserGun>();
            MissileLauncher = GetComponentInChildren<MissileLauncher>();
            Damage   = GetComponent<Damage>();
            Commander     =  GetComponentInChildren<Commander>();
        }
        
        private void OnEnable()
        {
            PopulateSettings(settings);
        }
        
        private void Start()
        {
            Initialize(settings, teamNumber);
        }
        
        public void Initialize(Settings shipSettings, int team = 0)
        {
            if (isInitialized) return;
            teamNumber = team;
            PopulateSettings(shipSettings);

            Commander?.InitializeCommander(this);

            if (Damage)
                Damage.OnDeath += (victim, killer) => HandleShipDeath();

            isInitialized = true;
        }

        
        private void PopulateSettings(Settings s)
        {            
            settings = s;
            Movement?.PopulateSettings(settings);
            Damage?.PopulateSettings(settings);
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
            Damage.ResetDamageState();
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