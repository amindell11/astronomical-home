using UnityEngine;
using ShipControl;
using System.Collections.Generic;

[RequireComponent(typeof(ShipMovement))]
[RequireComponent(typeof(ShipDamageHandler))]
public class Ship : MonoBehaviour, ITargetable
{
    public static readonly List<Transform> ActiveShips = new();

    /* ─────────── Tunable Parameters ─────────── */
    [Header("Settings Asset")]
    [Tooltip("ShipSettings asset that holds all tunable parameters.")]
    public ShipSettings settings;

    /* ─────────── Cached Components ─────────── */
    public ShipMovement  movement{get; private set;}
    public LaserGun      laserGun{get; private set;}
    public ShipDamageHandler damageHandler{get; private set;}
    public IShipCommandSource[] commandSources{get; private set;}

    /* ─────────── ITargetable Implementation ─────────── */
    public Transform TargetPoint => transform;

    public LockOnIndicator Indicator { get; private set; }

    /* ────────────────────────────────────────── */
    void Awake()
    {
        movement       = GetComponent<ShipMovement>();
        laserGun       = GetComponentInChildren<LaserGun>();
        damageHandler  = GetComponent<ShipDamageHandler>();
        commandSources = GetComponents<IShipCommandSource>();

        if (!settings)
        {
            RLog.LogError($"{name}: ShipSettings asset reference missing – using runtime default values.");
            settings = ScriptableObject.CreateInstance<ShipSettings>();
        }

        // Apply settings to movement & damage subsystems
        movement?.ApplySettings(settings);
        damageHandler?.ApplySettings(settings);

        Indicator = GetComponentInChildren<LockOnIndicator>(true);
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

    void FixedUpdate()
    {
        // Aggregate the highest-priority command for this frame.
        ShipCommand cmd = default;
        bool hasCmd = false;
        int highest = int.MinValue;
        foreach (var src in commandSources)
        {
            if (src == null) continue;
            if (src.TryGetCommand(out ShipCommand c))
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

        if (hasCmd && movement != null && movement.Controller != null)
        {
            // ShipMovement expects inputs in its 2-D controller.
            movement.Controller.SetControls(cmd.Thrust, cmd.Strafe);
            movement.Controller.SetRotationTarget(cmd.RotateToTarget, cmd.TargetAngle);

            // Weapons
            if (cmd.Fire && laserGun)
                laserGun.Fire();
        }
    }
}