using UnityEngine;
using ShipControl;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(DamageHandler))]
public class Ship : MonoBehaviour, ITargetable, IShooter
{
    public static readonly List<Transform> ActiveShips = new();
    public static event System.Action<Ship, Ship, float> OnGlobalShipDamaged; // victim, attacker, damage

    /* ─────────── Events ─────────── */
    public event System.Action<float, float, float> OnHealthChanged; // current, previous, max
    public event System.Action<float, float, float> OnShieldChanged; // current, previous, max
    public event System.Action<Ship, Ship> OnDeath; // victim, killer

    /* ─────────── Tunable Parameters ─────────── */
    [Header("Settings Asset")]
    [Tooltip("ShipSettings asset that holds all tunable parameters.")]
    public Settings settings;

    [Header("Team Settings")]
    [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
    public int teamNumber = 0;

    /* ─────────── Cached Components ─────────── */
    public Movement Movement { get; private set; }
    public LaserGun LaserGun { get; private set; }
    public MissileLauncher MissileLauncher { get; private set; }
    public DamageHandler DamageHandler { get; private set; }
    public Hull Hull { get; private set; }
    public ICommandSource[] CommandSources { get; private set; }

    /* ─────────── Current State ─────────── */
    public State CurrentState { get; private set; }
    public Command CurrentCommand { get; internal set; }
    public bool HasValidCommand { get; private set; } = false;

    /* ─────────── ITargetable Implementation ─────────── */
    public Transform TargetPoint => transform;

    /* ─────────── Lock-On Channel ─────────── */
    public LockChannel Lock { get; } = new LockChannel();

    /* ─────────── IShooter Implementation ─────────── */
    public Vector3 Velocity => Movement != null ? Movement.Kinematics.WorldVel : Vector3.zero;

    /* ─────────── Team System ─────────── */
    /// <summary>
    /// Determines if another ship is friendly to this ship.
    /// Ships are considered friendly if they have the same team number.
    /// </summary>
    /// <param name="otherShip">The other ship to check</param>
    /// <returns>True if the ships are on the same team, false otherwise</returns>
    public bool IsFriendly(Ship otherShip)
    {
        if (!otherShip) return false;
        return this.teamNumber == otherShip.teamNumber;
    }

    /* ────────────────────────────────────────── */
    private void Awake()
    {
        Movement       = GetComponent<Movement>();
        LaserGun       = GetComponentInChildren<LaserGun>();
        MissileLauncher = GetComponentInChildren<MissileLauncher>();
        DamageHandler  = GetComponent<DamageHandler>();
        Hull  = GetComponentInChildren<Hull>();
        // Discover all command sources within the ship hierarchy (includeInactive=true allows pooled objects to register before activation)
        CommandSources = GetComponentsInChildren<ICommandSource>(true)
            .OrderByDescending(cs => cs.Priority)
            .ToArray();

        // Relay events from damage handler
        DamageHandler.OnHealthChanged += (cur, prev, max) => OnHealthChanged?.Invoke(cur, prev, max);
        DamageHandler.OnShieldChanged += (cur, prev, max) => OnShieldChanged?.Invoke(cur, prev, max);
        DamageHandler.OnDeath += (victim, killer) => OnDeath?.Invoke(victim, killer);
        DamageHandler.OnDeath += (victim, killer) => HandleShipDeath();

        if (!settings)
            settings = ScriptableObject.CreateInstance<Settings>();
    }

    private void Start()
    {
        // Apply settings to movement & damage subsystems now that all Awakes are done.
        Movement?.PopulateSettings(settings);
        DamageHandler?.PopulateSettings(settings);

        // Initialize commanders now that all components are cached and configured.
        foreach (var source in CommandSources)
        {
            source?.InitializeCommander(this);
        }
    }

    private void OnEnable()
    {
        // Apply settings to movement & damage subsystems now that all Awakes are done.
        Movement?.PopulateSettings(settings);
        DamageHandler?.PopulateSettings(settings);

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

    internal static void BroadcastShipDamaged(Ship victim, Ship attacker, float damage)
    {
        OnGlobalShipDamaged?.Invoke(victim, attacker, damage);
    }   
    
    private void HandleShipDeath()
    {
        Lock.Released?.Invoke();
        MissileLauncher.CancelLock();
    }
    /// <summary>
    /// Resets the ship to its initial state.
    /// </summary>
    public void ResetShip()
    {
        Movement.ResetMovement();
        LaserGun.ResetHeat();
        MissileLauncher.ReplenishAmmo();
        DamageHandler.ResetDamageState();
    }

    private void FixedUpdate()
    {
        if (HasValidCommand)
        {
            if (Movement)
                Movement.SetCommand(CurrentCommand);
            if (CurrentCommand.PrimaryFire && LaserGun)
                LaserGun.Fire();
            if (CurrentCommand.SecondaryFire && MissileLauncher)
                MissileLauncher.Fire();
        }
        HasValidCommand = false;
    }

    // With command polling now in Update(), FixedUpdate simply exists so that other
    // components (e.g., ShipMovement) can continue to rely on physics-step timing.
    private void Update()
    {
        UpdateState();
        HasValidCommand = TryGetCommand(CurrentState, out var cmd);
        if (HasValidCommand)
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
    private bool TryGetCommand(State state,out Command cmd)
    {
        foreach (var src in CommandSources) {
            if (src is null || !src.TryGetCommand(state, out var c))continue;
            cmd = c;
            return true;
        }
        cmd = default;
        return false;
    }
}