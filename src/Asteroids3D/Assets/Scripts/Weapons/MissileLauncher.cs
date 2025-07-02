using UnityEngine;

/// <summary>
/// Launcher component that provides lock-on targeting and fires <see cref="MissileProjectile"/>s.
/// First press begins locking process; a second press either dumb-fires (if still locking) or fires
/// a homing missile once lock has been acquired.
/// </summary>
public class MissileLauncher : LauncherBase<MissileProjectile>
{
    public enum LockState { Idle, Locking, Locked, Cooldown }

    [Header("Lock-On Settings")]
    [SerializeField] private float lockOnConeAngle = 30f;
    [SerializeField] private float lockOnTime     = 0.6f;
    [SerializeField] private float lockExpiry     = 3f;
    [SerializeField] private float maxLockDistance= 100f;

    [Header("Ammo System")]
    [SerializeField] private int maxAmmo = 4;
    private int currentAmmo;

    public int AmmoCount => currentAmmo;
    public int MaxAmmo => maxAmmo;

    // Runtime state
    LockState state = LockState.Idle;
    ITargetable currentTarget;
    float lockTimer;
    float lockAcquiredTime;

    /* ───────────────────────── Public API ───────────────────────── */
    /// <summary>True if a target is currently locked.</summary>
    public bool IsLocked => state == LockState.Locked;

    /// <summary>Current lock-on state for UI display.</summary>
    public LockState State => state;

    /// <summary>Lock progress from 0-1 during locking phase.</summary>
    public float LockProgress => (state == LockState.Locking && lockOnTime > 0f) ? Mathf.Clamp01(lockTimer / lockOnTime) : 0f;

    /// <summary>Resets ammo count to maximum.</summary>
    public void ReplenishAmmo()
    {
        currentAmmo = maxAmmo;
    }

    /// <summary>Begin lock-on sequence towards the given target (if valid).</summary>
    public bool TryStartLock(ITargetable candidate)
    {
        // Prevent initiating a new lock while the launcher is on cooldown so that
        // AI behaviour is consistent with player input (which is gated via Fire()).
        if (Time.time < nextFireTime) return false;
        
        // Don't start locking if we have no ammo
        if (currentAmmo <= 0)
        {
            RLog.Weapon("TryStartLock: No ammo available, cannot start lock.");
            return false;
        }
        
        RLog.Weapon("TryStartLock: " + candidate);
        if (candidate == null) return false;
        if (state != LockState.Idle) return false;

        currentTarget    = candidate;
        lockTimer        = 0f;
        state            = LockState.Locking;
        return true;
    }

    /// <summary>Abort any ongoing or acquired lock.</summary>
    public void CancelLock()
    {
        RLog.Weapon("CancelLock: Resetting lock.");
        ResetLock();
        state         = LockState.Idle;
    }

    /* ───────────────────────── MonoBehaviour ───────────────────────── */
    void Start()
    {
        ReplenishAmmo();
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case LockState.Idle:
                // Auto-scan for targets if not on cooldown and we have ammo.
                if (Time.time >= nextFireTime && currentAmmo > 0)
                {
                    ScanForTarget();
                }
                break;
            case LockState.Locking:
                // Check if we still have ammo during locking
                if (currentAmmo <= 0)
                {
                    RLog.Weapon("HandleLocking: Ran out of ammo during lock sequence. Cancelling lock.");
                    CancelLock();
                    break;
                }
                HandleLocking();
                break;
            case LockState.Locked:
                // Check if we still have ammo while locked
                if (currentAmmo <= 0)
                {
                    RLog.Weapon("HandleLocked: Ran out of ammo while locked. Cancelling lock.");
                    CancelLock();
                    break;
                }
                HandleLocked();
                break;
            case LockState.Cooldown:
                if (Time.time >= nextFireTime)
                {
                    state = LockState.Idle;
                }
                break;
        }
    }

    void HandleLocking()
    {
        if (!ValidateTarget(currentTarget))
        {
            RLog.Weapon("HandleLocking: Target became invalid. Cancelling lock.");
            CancelLock(); return;
        }

        // Update indicator position & progress
        if (currentTarget.Indicator != null)
        {
            currentTarget.Indicator.UpdateProgress(LockProgress);
        }

        // Check continuous LOS via raycast
        Vector3 dir = currentTarget.TargetPoint.position - firePoint.position;
        float dist = dir.magnitude;
        if (dist > maxLockDistance)
        {
            RLog.Weapon($"HandleLocking: Target out of range ({dist}m > {maxLockDistance}m). Cancelling lock.");
            CancelLock();
            return;
        }
        if (Physics.Raycast(firePoint.position, dir.normalized, out RaycastHit hit, maxLockDistance))
        {
            ITargetable hitTgt = hit.collider.GetComponentInParent<ITargetable>();
            if (hitTgt != currentTarget)
            {
                RLog.Weapon($"HandleLocking: Line of sight to target lost. Hit '{hit.collider.name}' instead of '{currentTarget}'. Cancelling lock.");
                CancelLock();
                return;
            }
        }
        else
        {
            RLog.Weapon("HandleLocking: Raycast towards target did not hit anything. Cancelling lock.");
            CancelLock(); return;
        }

        lockTimer += Time.deltaTime;
        if (lockTimer >= lockOnTime)
        {
            RLog.Weapon("Locking complete: " + currentTarget);
            state            = LockState.Locked;
            lockAcquiredTime = Time.time;

            // Notify indicator of complete lock
            if (currentTarget.Indicator != null)
            {
                currentTarget.Indicator.OnLockComplete();
            }
        }
    }

    void HandleLocked()
    {
        if (!ValidateTarget(currentTarget)) { CancelLock(); return; }
        float dist = Vector3.Distance(currentTarget.TargetPoint.position, transform.position);
        if (dist > maxLockDistance)
        {
            RLog.Weapon("Out of range");
            CancelLock();
        }
        if (Time.time - lockAcquiredTime > lockExpiry)
        {
            RLog.Weapon("Lock expired");
            CancelLock();
        }
    }

    /// <summary>Resets all locking-related state variables without changing the main FSM state.</summary>
    void ResetLock()
    {
        // Hide indicator on previous target (if any)
        if (currentTarget?.Indicator != null)
        {
            currentTarget.Indicator.Hide();
        }

        currentTarget = null;
        lockTimer     = 0f;
    }

    bool ValidateTarget(ITargetable tgt) => tgt != null && tgt.TargetPoint != null;

    /* ───────────────────────── Fire override ───────────────────────── */
    public override bool CanFire()
    {
        // To fire in any capacity (locking or launching), we need ammo and the weapon must be off cooldown.
        return base.CanFire() && currentAmmo > 0;
    }

    public override ProjectileBase Fire()
    {
        bool wasLocked = state == LockState.Locked && currentTarget != null;

        // Use the new FireProjectile method to get the instance
        MissileProjectile proj = base.Fire() as MissileProjectile;

        if (proj == null)
        {
            // The base class decided not to fire (e.g., on cooldown)
            return null;
        }
        
        currentAmmo--;

        if (wasLocked)
        {
            RLog.Weapon("Firing locked missile");
            proj.SetTarget(currentTarget.TargetPoint);
        }
        else
        {
            RLog.Weapon("Dumb-firing missile");
        }

        // Reset state and enter cooldown
        ResetLock();
        state = LockState.Cooldown;
        
        return proj;
    }

    /* ───────────────────────── Helpers ───────────────────────── */
    
    /// <summary>Finds the best target in the lock-on cone and starts the locking process.</summary>
    void ScanForTarget()
    {
        ITargetable bestTarget = FindBestTargetInCone();
        if (bestTarget != null)
        {
            StartLock(bestTarget);
        }
    }
    
    /// <summary>Starts the lock-on process for a given target.</summary>
    private bool StartLock(ITargetable candidate)
    {
        if (candidate == null || state != LockState.Idle) return false;

        RLog.Weapon("StartLock: " + candidate);
        currentTarget = candidate;
        lockTimer = 0f;
        state = LockState.Locking;
        return true;
    }
    
    /// <summary>Simple forward raycast to pick first <see cref="ITargetable"/> object in LOS.</summary>
    ITargetable FindBestTargetInCone()
    {
        RLog.Weapon("FindBestTargetInCone: Scanning for targets.");
        var shipMask = LayerMask.GetMask("Ship");
        var colliders = Physics.OverlapSphere(firePoint.position, maxLockDistance, shipMask);
        RLog.Weapon($"FindBestTargetInCone: Found {colliders.Length} colliders on 'Ship' layer.");
        
        ITargetable bestCandidate = null;
        float smallestAngle = lockOnConeAngle / 2f;
        Ship selfShip = GetComponentInParent<Ship>();

        foreach (var col in colliders)
        {
            var targetable = col.GetComponentInParent<ITargetable>();
            
            // Basic validation
            if (targetable == null || !ValidateTarget(targetable)) 
            {
                RLog.Weapon($"FindBestTargetInCone: Collider {col.name} is not a valid target.");
                continue;
            }
            
            // Ensure we don't target ourselves
            if ((targetable as Ship) == selfShip)
            {
                RLog.Weapon($"FindBestTargetInCone: Collider {col.name} is self, skipping.");
                continue;
            }

            Vector3 dirToTarget = (targetable.TargetPoint.position - firePoint.position);
            float angle = Vector3.Angle(firePoint.up, dirToTarget.normalized);

            if (angle < smallestAngle)
            {
                // Line of sight check
                if (Physics.Raycast(firePoint.position, dirToTarget.normalized, out RaycastHit hit, dirToTarget.magnitude))
                {
                    if (hit.collider.GetComponentInParent<ITargetable>() == targetable)
                    {
                        smallestAngle = angle;
                        bestCandidate = targetable;
                        RLog.Weapon($"FindBestTargetInCone: Found candidate {bestCandidate} at angle {angle}.");
                    }
                    else
                    {
                        RLog.Weapon($"FindBestTargetInCone: Candidate {col.name} blocked by {hit.collider.name}.");
                    }
                }
                else
                {
                    RLog.Weapon($"FindBestTargetInCone: Raycast to {col.name} did not hit anything (but should have).");
                }
            }
            else
            {
                RLog.Weapon($"FindBestTargetInCone: Candidate {col.name} is outside lock-on cone (angle: {angle}).");
            }
        }
        
        if (bestCandidate != null)
        {
            RLog.Weapon($"FindBestTargetInCone: Best target found: {bestCandidate}.");
        }
        else
        {
            RLog.Weapon("FindBestTargetInCone: No suitable target found.");
        }
        return bestCandidate;
    }

#if UNITY_EDITOR
    /* ───────────────────────── Debug Gizmos ───────────────────────── */
    void OnDrawGizmos()
    {
        Vector3 origin = firePoint.position;
        Vector3 forward = firePoint.up; // ship forward direction

        // State-based color coding
        Color stateColor = state switch
        {
            LockState.Idle => Color.white,
            LockState.Locking => Color.yellow,
            LockState.Locked => Color.green,
            LockState.Cooldown => Color.gray,
            _ => Color.gray
        };

        // Max lock distance sphere (wireframe)
        Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f);
        Gizmos.DrawWireSphere(origin, maxLockDistance);

        // Forward direction ray
        Gizmos.color = stateColor;
        Vector3 forwardEnd = origin + forward * maxLockDistance;
        Gizmos.DrawRay(origin, forward * maxLockDistance);

        // Current target visualization
        if (currentTarget != null && currentTarget.TargetPoint != null)
        {
            Vector3 targetPos = currentTarget.TargetPoint.position;
            
            // Line to target
            Gizmos.color = stateColor;
            Gizmos.DrawLine(origin, targetPos);
            
            // Target indicator
            Gizmos.color = state == LockState.Locked ? Color.green : Color.red;
            Gizmos.DrawWireSphere(targetPos, 1f);
            
            // Distance text would go here if using Handles
        }

        // Lock progress visualization (arc around firePoint)
        if (state == LockState.Locking && lockOnTime > 0f)
        {
            float progress = Mathf.Clamp01(lockTimer / lockOnTime);
            Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            
            // Simple progress ring (multiple points to approximate arc)
            int segments = 16;
            float radius = 2f;
            for (int i = 0; i < segments * progress; i++)
            {
                float angle1 = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                float angle2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;
                
                Vector3 p1 = origin + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                Vector3 p2 = origin + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;
                
                Gizmos.DrawLine(p1, p2);
            }
        }

        // State label (would use Handles.Label if available)
        UnityEditor.Handles.color = stateColor;
        float cooldownRemaining = Mathf.Max(0, nextFireTime - Time.time);
        string ammoText = $"Ammo: {currentAmmo}/{maxAmmo}";
        UnityEditor.Handles.Label(origin + Vector3.up * 3f, $"Missile: {state}\n{ammoText}\nTimer: {lockTimer:F1}s\nCooldown: {cooldownRemaining:F1}s");
    }
#endif
} 