using Ships.Control;
using Ships.Visuals;
using UnityEngine;
using Weapons;
using MoveController = Ships.Movement.Controller;
using Ships.Weapons;

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
        public WeaponSystem Weapons { get; internal set; }
        public Damage Damage { get; internal set; }
        public ICommandSource Commander { get; internal set; }  

        private bool isInitialized = false;

        public State CurrentState { get; private set; }
        public Command CurrentCommand { get; internal set; }
        private bool HasValidCommand { get; set; } = false;

        public Transform TargetPoint => transform;
        public LockChannel Lock { get; } = new LockChannel();
        public Vector3 Velocity => Movement ? Movement.Kinematics.WorldVel : Vector3.zero;
        
        private void OnEnable()
        {
            PopulateSettings();
        }
        
        private void Start()
        {
            Initialize(settings, teamNumber);
        }
        
        private void PopulateSettings()
        {            
            Movement?.PopulateSettings(settings);
            Damage?.PopulateSettings(settings);
        }
        
        public void Initialize(Settings shipSettings, int team)
        {
            if (isInitialized) return;
            FindComponents();
            settings = shipSettings;
            teamNumber = team;
            
            Commander?.InitializeCommander(this);
            PopulateSettings();

            if (Damage)
                Damage.OnDeath += (victim, killer) => HandleShipDeath();

            isInitialized = true;
        }
        private void FindComponents(){            
            Movement        = GetComponent<MoveController>();
            Weapons    = GetComponentInChildren<WeaponSystem>();
            Damage   = GetComponent<Damage>();
            Commander     =  GetComponentInChildren<Commander>();
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
            CurrentCommand = cmd;
        }

        private void UpdateState()
        {
            CurrentState = new State
            {
                Kinematics = Movement.Kinematics,
                IsLaserReady = Weapons?.LaserGun?.CanFire() ?? false,
                LaserHeatPct = Weapons?.LaserGun?.HeatPct ?? 0f,
                MissileState = Weapons?.MissileLauncher?.State ?? MissileLauncher.LockState.Idle,
                MissileAmmo = Weapons?.MissileLauncher?.AmmoCount ?? 0,
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