using System;
using UnityEngine;
using Unity.Properties;

/// <summary>
/// Context struct containing processed ship and environment data for utility-based AI decisions.
/// Computed once per frame and cached on the blackboard for efficiency.
/// </summary>
[Serializable, GeneratePropertyBag]
public struct AIContext
{
    [Header("Ship Status")]
    [Tooltip("Current shield as percentage of maximum (0.0 to 1.0)")]
    public float shieldPct;
    
    [Tooltip("Current health as percentage of maximum (0.0 to 1.0)")]
    public float healthPct;
    
    [Tooltip("Current laser heat as percentage of maximum (0.0 to 1.0)")]
    public float laserHeatPct;
    
    [Tooltip("Number of remaining missiles")]
    public int missileAmmo;
    
    [Tooltip("Current missile launcher state")]
    public MissileLauncher.LockState missileState;

    [Header("Target Information")]
    [Tooltip("Distance to current target relative to typical engagement range")]
    public float relDistance;
    
    [Tooltip("Relative speed toward/away from target (positive = closing)")]
    public float relSpeed;
    
    [Tooltip("True if line of sight to target is clear")]
    public bool lineOfSight;
    
    [Tooltip("Angle to target in degrees (0 = directly ahead)")]
    public float targetAngle;

    [Header("Threats")]
    [Tooltip("True if incoming missile detected")]
    public bool incomingMissile;
    
    [Tooltip("Number of enemy ships within engagement range")]
    public int nearbyEnemyCount;
    
    [Tooltip("Distance to nearest threat")]
    public float nearestThreatDistance;

    [Header("Tactical Situation")]
    [Tooltip("Number of friendly ships nearby")]
    public int nearbyFriendCount;
    
    [Tooltip("True if ship is within asteroid field for cover")]
    public bool nearAsteroidCover;
    
    [Tooltip("Current speed as percentage of maximum")]
    public float speedPct;

    [Header("Timestamps")]
    [Tooltip("Time when this context was last computed")]
    public float computeTime;
    
    [Tooltip("Frame count when this context was computed")]
    public int computeFrame;

    /// <summary>
    /// Creates an invalid/empty context for initialization
    /// </summary>
    public static AIContext Invalid => new AIContext 
    { 
        computeTime = -1f, 
        computeFrame = -1 
    };

    /// <summary>
    /// Returns true if this context is valid and recently computed
    /// </summary>
    public bool IsValid => computeTime >= 0f && computeFrame >= 0;

    /// <summary>
    /// Returns true if this context is stale and should be recomputed
    /// </summary>
    /// <param name="maxAgeSeconds">Maximum age in seconds before considering stale</param>
    public bool IsStale(float maxAgeSeconds = 0.1f)
    {
        return !IsValid || (Time.time - computeTime) > maxAgeSeconds;
    }

    /// <summary>
    /// Creates a context summary string for debugging
    /// </summary>
    public override string ToString()
    {
        return $"AIContext[Shield:{shieldPct:F2} Health:{healthPct:F2} RelDist:{relDistance:F1} " +
               $"LOS:{lineOfSight} Enemies:{nearbyEnemyCount} Friends:{nearbyFriendCount}]";
    }
} 