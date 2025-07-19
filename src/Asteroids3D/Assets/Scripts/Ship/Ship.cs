using UnityEngine;
using ShipControl;
using System.Collections.Generic;

[RequireComponent(typeof(ShipMovement))]
[RequireComponent(typeof(ShipDamageHandler))]
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
    public ShipSettings settings;

    [Header("Team Settings")]
    [Tooltip("Team number for this ship. Ships with the same team number are considered friendly.")]
    public int teamNumber = 0;

    /* ─────────── Cached Components ─────────── */
    public ShipMovement  movement{get; private set;}
    public LaserGun      laserGun{get; private set;}
    public MissileLauncher missileLauncher{get; private set;}
    public ShipDamageHandler damageHandler{get; private set;}
    public ShipHealthVisuals healthVisuals{get; private set;}
    public IShipCommandSource[] commandSources{get; private set;}

    /* ─────────── Current State ─────────── */
    public ShipState CurrentState { get; private set; } 
    public ShipCommand CurrentCommand { get; private set; }
    private bool hasValidCommand = false;

    /* ─────────── ITargetable Implementation ─────────── */
    public Transform TargetPoint => transform;

    /* ─────────── Lock-On Channel ─────────── */
    public LockChannel Lock { get; } = new LockChannel();

    /* ─────────── IShooter Implementation ─────────── */
    public Vector3 Velocity => movement != null ? movement.Kinematics.WorldVel : Vector3.zero;

    /* ─────────── Team System ─────────── */
    /// <summary>
    /// Determines if another ship is friendly to this ship.
    /// Ships are considered friendly if they have the same team number.
    /// </summary>
    /// <param name="otherShip">The other ship to check</param>
    /// <returns>True if the ships are on the same team, false otherwise</returns>
    public bool IsFriendly(Ship otherShip)
    {
        if (otherShip == null) return false;
        return this.teamNumber == otherShip.teamNumber;
    }

    /* ────────────────────────────────────────── */
    void Awake()
    {
        movement       = GetComponent<ShipMovement>();
        laserGun       = GetComponentInChildren<LaserGun>();
        missileLauncher = GetComponentInChildren<MissileLauncher>();
        damageHandler  = GetComponent<ShipDamageHandler>();
        healthVisuals  = GetComponentInChildren<ShipHealthVisuals>();
        // Discover all command sources within the ship hierarchy (includeInactive=true allows pooled objects to register before activation)
        commandSources = GetComponentsInChildren<IShipCommandSource>(true);


        // Relay events from damage handler
        damageHandler.OnHealthChanged += (cur, prev, max) => OnHealthChanged?.Invoke(cur, prev, max);
        damageHandler.OnShieldChanged += (cur, prev, max) => OnShieldChanged?.Invoke(cur, prev, max);
        damageHandler.OnDeath += (victim, killer) => OnDeath?.Invoke(victim, killer);
        damageHandler.OnDeath += (victim, killer) => HandleShipDeath();
        
        if (!settings)
        {
            RLog.ShipError($"{name}: ShipSettings asset reference missing – using runtime default values.");
            settings = ScriptableObject.CreateInstance<ShipSettings>();
        }

        // Apply settings to movement & damage subsystems
        movement?.ApplySettings(settings);
        damageHandler?.ApplySettings(settings);

        foreach (var source in commandSources)
        {
            source?.InitializeCommander(this);
        }
    }

    void OnEnable()
    {
        if (!ActiveShips.Contains(transform))
            ActiveShips.Add(transform);
    }

    void OnDisable()
    {
        ActiveShips.Remove(transform);
    }

    void OnDestroy()
    {
        ActiveShips.Remove(transform);
    }

    internal static void BroadcastShipDamaged(Ship victim, Ship attacker, float damage)
    {
        OnGlobalShipDamaged?.Invoke(victim, attacker, damage);
    }   
    void HandleShipDeath()
    {
        Lock.Released?.Invoke();
        missileLauncher.CancelLock();
    }
    /// <summary>
    /// Resets the ship to its initial state.
    /// </summary>
    public void ResetShip()
    {
        movement.ResetMovement();
        laserGun.ResetHeat();
        missileLauncher.ReplenishAmmo();
        damageHandler.ResetDamageState();
    }

    void FixedUpdate()
    {
        if (hasValidCommand)
        {
            if (movement != null)
                movement.SetCommand(CurrentCommand);
            if (CurrentCommand.PrimaryFire && laserGun)
                laserGun.Fire();
            if (CurrentCommand.SecondaryFire && missileLauncher)
                missileLauncher.Fire();
        }
        hasValidCommand = false;
    }

    // With command polling now in Update(), FixedUpdate simply exists so that other
    // components (e.g., ShipMovement) can continue to rely on physics-step timing.
    void Update() { 
        ShipCommand cmd = default;
        bool hasCmd = false;
        int highest = int.MinValue;

        var state = new ShipState
        {
            Kinematics = movement.Kinematics,
            IsLaserReady = laserGun?.CanFire() ?? false,
            LaserHeatPct = laserGun?.HeatPct ?? 0f,
            MissileState = missileLauncher?.State ?? MissileLauncher.LockState.Idle,
            MissileAmmo = missileLauncher?.AmmoCount ?? 0,
            HealthPct = damageHandler.HealthPct,
            ShieldPct = damageHandler.ShieldPct,
        };
        CurrentState = state;

        foreach (var src in commandSources)
        {
            if (src == null) continue;
            if (src.TryGetCommand(state, out ShipCommand c))
            {
                int p = src.Priority;
                if (!hasCmd || p > highest)
                {
                    cmd = c;
                    highest = p;
                    hasCmd  = true;
                }
            }
        }
        CurrentCommand = cmd;
        hasValidCommand = hasCmd;
    }
}