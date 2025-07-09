using System;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Component that computes and caches AIContext data for behavior tree consumption.
/// Should be attached to the same GameObject as the BehaviorGraphAgent.
/// The provider itself is passed to the blackboard instead of the context data.
/// </summary>
[RequireComponent(typeof(BehaviorGraphAgent))]
public class AIContextProvider : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Maximum age in seconds before recomputing context")]
    [Range(0.02f, 0.5f)]
    public float maxContextAge = 0.1f;
    
    [Tooltip("Maximum distance to consider nearby ships")]
    public float nearbyShipRadius = 30f;
    
    [Tooltip("Typical engagement range for relative distance calculations")]
    public float engagementRange = 20f;
    
    [Tooltip("Radius to scan for asteroid cover")]
    public float asteroidCoverRadius = 15f;
    
    [Header("Debug")]
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    
    [Tooltip("Log context updates for debugging")]
    public bool logUpdates = false;

    // Cached references
    private BehaviorGraphAgent behaviorAgent;
    private Ship ship;
    private AICommander aiCommander;
    
    // Buffer for physics queries
    private readonly Collider[] nearbyColliders = new Collider[32];
    private AIContext currentContext;
    
    // Public properties to expose context data
    public AIContext Context => currentContext;
    public bool IsContextValid => currentContext.IsValid;
    public float ShieldPct => currentContext.shieldPct;
    public float HealthPct => currentContext.healthPct;
    public float LaserHeatPct => currentContext.laserHeatPct;
    public int MissileAmmo => currentContext.missileAmmo;
    public MissileLauncher.LockState MissileState => currentContext.missileState;
    public float RelDistance => currentContext.relDistance;
    public float RelSpeed => currentContext.relSpeed;
    public bool HasLineOfSight => currentContext.lineOfSight;
    public float TargetAngle => currentContext.targetAngle;
    public bool IncomingMissile => currentContext.incomingMissile;
    public int NearbyEnemyCount => currentContext.nearbyEnemyCount;
    public int NearbyFriendCount => currentContext.nearbyFriendCount;
    public float NearestThreatDistance => currentContext.nearestThreatDistance;
    public bool NearAsteroidCover => currentContext.nearAsteroidCover;
    public float SpeedPct => currentContext.speedPct;
    public Ship Enemy => currentContext.enemy;  
    void Awake()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        ship = GetComponentInParent<Ship>();
        aiCommander = GetComponent<AICommander>();
        
        if (!ship)
        {
            RLog.AIError($"AIContextProvider on {name}: No Ship component found in parent");
            enabled = false;
            return;
        }
        
        if (!behaviorAgent)
        {
            RLog.AIError($"AIContextProvider on {name}: No BehaviorGraphAgent component found");
            enabled = false;
            return;
        }
    }
    
    void Start()
    {
        // Initialize with invalid context
        currentContext = AIContext.Invalid;
    }
    
    void Update()
    {
        UpdateContext();
    }
    
    /// <summary>
    /// Computes and caches the current AIContext
    /// </summary>
    public void UpdateContext()
    {
        if (!ship || !behaviorAgent) return;
        
        // Check if we need to recompute
        if (!currentContext.IsStale(maxContextAge))
            return;
            
        var context = ComputeContext();
        currentContext = context;
        
        if (logUpdates)
        {
            RLog.AI($"[{name}] Context updated: {context}");
        }
    }
    
    /// <summary>
    /// Forces immediate context recomputation
    /// </summary>
    public void ForceUpdate()
    {
        var context = ComputeContext();
        currentContext = context;
    }
    
    private AIContext ComputeContext()
    {
        var state = ship.CurrentState;
        var context = new AIContext();
        
        // Basic ship status
        context.shieldPct = state.ShieldPct;
        context.healthPct = state.HealthPct;
        context.laserHeatPct = state.LaserHeatPct;
        context.missileAmmo = state.MissileAmmo;
        context.missileState = state.MissileState;
        context.speedPct = state.Kinematics.Speed / (ship.settings?.maxSpeed ?? 1f);
        
        // Target information
        Ship target = null;
        if (behaviorAgent.GetVariable("Enemy", out BlackboardVariable enemyVar) && 
            enemyVar.ObjectValue is Ship enemyShip && 
            enemyShip && enemyShip.gameObject.activeInHierarchy)
        {
            target = enemyShip;
            context.enemy = enemyShip;
        }
        
        if (target)
        {
            ComputeTargetInfo(ref context, target);
        }
        else
        {
            // No target
            context.relDistance = float.MaxValue;
            context.relSpeed = 0f;
            context.lineOfSight = false;
            context.targetAngle = 0f;
        }
        
        // Threat and tactical assessment
        ComputeThreatInfo(ref context);
        ComputeTacticalInfo(ref context);
        
        // Timestamps
        context.computeTime = Time.time;
        context.computeFrame = Time.frameCount;
        
        return context;
    }
    
    private void ComputeTargetInfo(ref AIContext context, Ship target)
    {
        Vector3 selfPos = transform.position;
        Vector3 targetPos = target.transform.position;
        Vector3 toTarget = targetPos - selfPos;
        
        float distance = toTarget.magnitude;
        context.relDistance = distance / engagementRange;
        context.targetAngle = Vector3.Angle(transform.up, toTarget);
        
        // Relative speed (positive if closing)
        Vector3 relVel = ship.Velocity - target.Velocity;
        context.relSpeed = Vector3.Dot(relVel, toTarget.normalized);
        
        // Line of sight check
        if (aiCommander)
        {
            context.lineOfSight = aiCommander.HasLineOfSight(target.transform);
        }
        else
        {
            // Fallback LOS check
            context.lineOfSight = LineOfSight.IsClear(selfPos, targetPos, LayerIds.Mask(LayerIds.Asteroid));
        }
    }
    
    private void ComputeThreatInfo(ref AIContext context)
    {
        Vector3 selfPos = transform.position;
        
        // Scan for nearby ships
        int hitCount = Physics.OverlapSphereNonAlloc(
            selfPos, nearbyShipRadius, nearbyColliders, LayerIds.Mask(LayerIds.Ship));
        
        int enemyCount = 0;
        int friendCount = 0;
        float nearestThreat = float.MaxValue;
        
        for (int i = 0; i < hitCount; i++)
        {
            var col = nearbyColliders[i];
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
                if (distance < nearestThreat)
                    nearestThreat = distance;
            }
        }
        
        context.nearbyEnemyCount = enemyCount;
        context.nearbyFriendCount = friendCount;
        context.nearestThreatDistance = nearestThreat;
        
        // TODO: Implement incoming missile detection
        context.incomingMissile = false;
    }
    
    private void ComputeTacticalInfo(ref AIContext context)
    {
        // Check for nearby asteroid cover
        Vector3 selfPos = transform.position;
        int asteroidHits = Physics.OverlapSphereNonAlloc(
            selfPos, asteroidCoverRadius, nearbyColliders, LayerIds.Mask(LayerIds.Asteroid));
        
        context.nearAsteroidCover = asteroidHits > 0;
    }
    
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        
        Vector3 pos = transform.position;
        
        // Nearby ship radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(pos, nearbyShipRadius);
        
        // Engagement range
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(pos, engagementRange);
        
        // Asteroid cover radius
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(pos, asteroidCoverRadius);
        
        // Draw context info
        if (currentContext.IsValid)
        {
            UnityEditor.Handles.color = Color.white;
            string info = $"Context:\nShield: {currentContext.shieldPct:F2}\n" +
                         $"Enemies: {currentContext.nearbyEnemyCount}\n" +
                         $"Friends: {currentContext.nearbyFriendCount}\n" +
                         $"LOS: {currentContext.lineOfSight}";
            UnityEditor.Handles.Label(pos + Vector3.up * 3f, info);
        }
    }
#endif
} 