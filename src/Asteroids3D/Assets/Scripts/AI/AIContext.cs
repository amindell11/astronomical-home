using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Properties;

/// <summary>
/// Component that provides on-demand AI context data for AI state machine consumption.
/// Should be attached to the same GameObject as the AICommander.
/// </summary>
[Serializable, GeneratePropertyBag]
public class AIContext : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Maximum distance to consider nearby ships")]
    public float nearbyShipRadius = 30f;
    
    [Tooltip("Radius to scan for asteroid cover")]
    public float asteroidCoverRadius = 15f;
    
    [Header("Debug")]
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;

    // Cached references
    private Ship ship;
    private AICommander aiCommander;
    private AINavigator aiNavigator;
    private AIGunner aiGunner;
    
    // Buffer for physics queries - using shared buffers for efficiency
    
    public void Initialize(Ship ship, AICommander commander, AINavigator navigator, AIGunner gunner)
    {
        this.ship = ship;
        this.aiCommander = commander;
        this.aiNavigator = navigator;
        this.aiGunner = gunner;
        
        if (!ship)
        {
            RLog.AIError($"AIContext on {name}: No Ship provided during initialization");
            enabled = false;
        }
    }
    
    void Awake()
    {
        // Initialization now happens via Initialize method
    }
    
    // ===== Ownship Kinematics =====
    
    /// <summary>
    /// Current 2D plane position of the ship
    /// </summary>
    public Vector2 SelfPosition => ship?.CurrentState.Kinematics.Pos ?? Vector2.zero;
    
    /// <summary>
    /// Current 3D world position of the ship
    /// </summary>
    public Vector3 SelfPosition3D => transform.position;

    /// <summary>
    /// Current 3D world position of the ship
    /// </summary>
    public Transform SelfTransform => transform;

    /// <summary>
    /// Current 2D plane velocity of the ship
    /// </summary>
    public Vector2 SelfVelocity => ship?.CurrentState.Kinematics.Vel ?? Vector2.zero;
    
    /// <summary>
    /// Current speed as percentage of maximum
    /// </summary>
    public float SpeedPct => ship?.CurrentState.Kinematics.Speed / (ship?.settings?.maxSpeed ?? 1f) ?? 0f;
    
    // ===== Ship Status =====
    
    /// <summary>
    /// Current shield as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float ShieldPct => ship?.CurrentState.ShieldPct ?? 0f;
    
    /// <summary>
    /// Current health as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float HealthPct => ship?.CurrentState.HealthPct ?? 0f;
    
    /// <summary>
    /// Current laser heat as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float LaserHeatPct => ship?.CurrentState.LaserHeatPct ?? 0f;
    
    /// <summary>
    /// Number of remaining missiles
    /// </summary>
    public int MissileAmmo => ship?.CurrentState.MissileAmmo ?? 0;
    
    /// <summary>
    /// Current missile launcher state
    /// </summary>
    public MissileLauncher.LockState MissileState => ship?.CurrentState.MissileState ?? MissileLauncher.LockState.Idle;
    
    // ===== Enemy Information =====
    
    /// <summary>
    /// Current enemy ship
    /// </summary>
    public Ship Enemy => FindNearestEnemy();
    public Vector2 EnemyPos => Enemy.CurrentState.Kinematics.Pos;
    public Vector2 EnemyVel => Enemy != null ? Enemy.CurrentState.Kinematics.Vel : Vector2.zero;

    /// <summary>
    /// Vector from ship to the nearest enemy
    /// </summary>
    public Vector2 VectorToEnemy => EnemyPos - SelfPosition;
    
    /// <summary>
    /// Relative velocity between ship and nearest enemy
    /// </summary>
    public Vector2 EnemyRelVelocity => EnemyVel - SelfVelocity;
    
    // ===== Closing Speed =====
    /// <summary>
    /// Signed closing speed along the line-of-sight to the nearest enemy.<br/>
    /// Positive values indicate the range is shrinking (closing).<br/>
    /// Negative values indicate the range is growing (opening).
    /// </summary>
    public float ClosingSpeed
    {
        get
        {
            // If there is no meaningful separation vector, report zero
            if (VectorToEnemy.sqrMagnitude < 0.0001f) return 0f;
            
            // Dot product of relative velocity onto line-of-sight, sign-flipped so that
            // positive values mean closing (distance decreasing).
            return -Vector2.Dot(VectorToEnemy.normalized, EnemyRelVelocity);
        }
    }
    
    
    /// <summary>
    /// True if line of sight to the nearest enemy is clear
    /// </summary>
    public bool LineOfSightToEnemy => Enemy != null && LineOfSight.IsClear(SelfPosition3D, Enemy.transform.position, LayerIds.Mask(LayerIds.Asteroid));
    
    /// <summary>
    /// Angle to the nearest enemy in degrees
    /// </summary>
    public float AngleToEnemy => GetAngleTo(VectorToEnemy);

    /// <summary>
    /// Angle from the enemy's forward direction to our ship (deg).<br/>
    /// 0°  → enemy is pointing directly at us.<br/>
    /// 180° → enemy is facing directly away.
    /// </summary>
    public float EnemyAngleToSelf
    {
        get
        {
            if (Enemy == null) return 180f;
            Vector2 enemyForward = Enemy.CurrentState.Kinematics.Forward;
            Vector2 toSelf = SelfPosition - EnemyPos;
            if (toSelf.sqrMagnitude < 0.01f) return 0f;
            return Vector2.Angle(enemyForward, toSelf);
        }
    }
    
    /// <summary>
    /// Enemy's current health as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float EnemyHealthPct => Enemy?.CurrentState.HealthPct ?? 0f;
    
    /// <summary>
    /// Enemy's current shield as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float EnemyShieldPct => Enemy?.CurrentState.ShieldPct ?? 0f;
    
    /// <summary>
    /// Enemy's current laser heat as percentage of maximum (0.0 to 1.0)
    /// </summary>
    public float EnemyLaserHeatPct => Enemy?.CurrentState.LaserHeatPct ?? 0f;
    
    /// <summary>
    /// Enemy's number of remaining missiles
    /// </summary>
    public int EnemyMissileAmmo => Enemy?.CurrentState.MissileAmmo ?? 0;
    
    // ===== Target Information =====
    
    /// <summary>
    /// Vector from ship to the gunner's current target
    /// </summary>
    public Vector2 VectorToTarget => aiGunner?.VectorToTarget ?? Vector2.zero;
    
    /// <summary>
    /// True if line of sight to the gunner's target is clear
    /// </summary>
    public bool LineOfSightToTarget => aiGunner?.HasLineOfSight() ?? false;
    
    /// <summary>
    /// Angle to the gunner's target in degrees
    /// </summary>
    public float AngleToTarget => aiGunner?.AngleToTarget ?? 0f;
    
    // ===== Threats =====
    
    /// <summary>
    /// True if incoming missile detected
    /// </summary>
    public bool IncomingMissile => false; // TODO: Implement incoming missile detection
    
    /// <summary>
    /// Number of enemy ships within engagement range
    /// </summary>
    public int NearbyEnemyCount => ScanForNearbyShips().enemyCount;
    
    /// <summary>
    /// Distance to nearest threat
    /// </summary>
    public float NearestThreatDistance => ScanForNearbyShips().nearestThreat;
    
    // ===== Tactical Situation =====
    
    /// <summary>
    /// Number of friendly ships nearby
    /// </summary>
    public int NearbyFriendCount => ScanForNearbyShips().friendCount;
    
    /// <summary>
    /// True if ship is within asteroid field for cover
    /// </summary>
    public bool NearAsteroidCover => Physics.OverlapSphereNonAlloc(SelfPosition3D, asteroidCoverRadius, PhysicsBuffers.GetColliderBuffer(), LayerIds.Mask(LayerIds.Asteroid)) > 0;
    
    // ===== Navigation =====
    
    /// <summary>
    /// Vector from the ship to the current navigation waypoint
    /// </summary>
    public Vector2 VectorToWaypoint => aiNavigator?.CurrentWaypoint.isValid == true ? aiNavigator.CurrentWaypoint.position - SelfPosition : Vector2.zero;
    
    public float LaserSpeed => ship?.laserGun?.ProjectileSpeed ?? 0f;
    // ===== Helper Methods =====

    /// <summary>
    /// Gets angle to a target vector in degrees
    /// </summary>
    private float GetAngleTo(Vector2 targetVector)
    {
        if (targetVector.sqrMagnitude < 0.01f) return 0f;
        return Vector2.Angle(ship?.CurrentState.Kinematics.Forward ?? Vector2.up, targetVector);
    }

    /// <summary>
    /// Scans for nearby ships and returns counts and threat info
    /// </summary>
    private (int enemyCount, int friendCount, float nearestThreat) ScanForNearbyShips()
    {
        if (!ship) return (0, 0, float.MaxValue);
        
        Vector3 selfPos = SelfPosition3D;
        var colliders = PhysicsBuffers.GetColliderBuffer();
        int hitCount = Physics.OverlapSphereNonAlloc(selfPos, nearbyShipRadius, colliders, LayerIds.Mask(LayerIds.Ship));
        
        int enemyCount = 0;
        int friendCount = 0;
        float nearestThreat = float.MaxValue;
        
        for (int i = 0; i < hitCount; i++)
        {
            var col = colliders[i];
            if (!col) continue;
            
            var otherShip = col.attachedRigidbody?.GetComponent<Ship>();
            if (!otherShip || otherShip == ship) continue;
            
            float distance = Vector3.Distance(selfPos, otherShip.transform.position);
            
            if (ship.IsFriendly(otherShip))
            {
                friendCount++;
            }
            else
            {
                enemyCount++;
                if (distance < nearestThreat && otherShip != Enemy)
                    nearestThreat = distance;
            }
        }

        // If we have a current enemy, its distance is also a threat distance
        var currentEnemy = Enemy;
        if (currentEnemy)
        {
            float enemyDist = Vector3.Distance(selfPos, currentEnemy.transform.position);
            if (enemyDist < nearestThreat)
            {
                nearestThreat = enemyDist;
            }
        }
        
        return (enemyCount, friendCount, nearestThreat);
    }

    /// <summary>
    /// Finds the nearest enemy ship
    /// </summary>
    private Ship FindNearestEnemy()
    {
        if (!ship) return null;
        
        Vector3 selfPos = SelfPosition3D;
        Ship nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        var colliders = PhysicsBuffers.GetColliderBuffer();
        int hitCount = Physics.OverlapSphereNonAlloc(selfPos, nearbyShipRadius, colliders, LayerIds.Mask(LayerIds.Ship));
        
        for (int i = 0; i < hitCount; i++)
        {
            var col = colliders[i];
            if (!col) continue;
            
            var otherShip = col.attachedRigidbody?.GetComponent<Ship>();
            if (!otherShip || otherShip == ship) continue;
            
            if (!ship.IsFriendly(otherShip))
            {
                float distance = Vector3.Distance(selfPos, otherShip.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = otherShip;
                }
            }
        }
        
        return nearestEnemy;
    }

    /// <summary>
    /// Creates a context summary string for debugging
    /// </summary>
    public override string ToString()
    {
        return $"AIContext[Shield:{ShieldPct:F2} Health:{HealthPct:F2} " +
               $"EnemyDist:{VectorToEnemy.magnitude:F1} LOS:{LineOfSightToEnemy} " +
               $"Enemies:{NearbyEnemyCount} Friends:{NearbyFriendCount}]";
    }
    
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        
        Vector3 pos = transform.position;
        
        // Nearby ship radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(pos, nearbyShipRadius);
        
        // Asteroid cover radius
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(pos, asteroidCoverRadius);
    }
#endif
} 