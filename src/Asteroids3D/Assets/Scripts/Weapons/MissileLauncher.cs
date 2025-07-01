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
        RLog.Log("TryStartLock: " + candidate);
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
        RLog.Log("CancelLock");
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
            case LockState.Locking:
                HandleLocking();
                break;
            case LockState.Locked:
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
        if (!ValidateTarget(currentTarget)) { CancelLock(); return; }

        // Update indicator position & progress
        if (currentTarget.Indicator != null)
        {
            currentTarget.Indicator.UpdateProgress(LockProgress);
        }

        // Check continuous LOS via raycast
        if (!firePoint) firePoint = transform;
        Vector3 dir = currentTarget.TargetPoint.position - firePoint.position;
        float dist = dir.magnitude;
        if (dist > maxLockDistance)
        {
            CancelLock();
            return;
        }
        if (Physics.Raycast(firePoint.position, dir.normalized, out RaycastHit hit, maxLockDistance))
        {
            ITargetable hitTgt = hit.collider.GetComponentInParent<ITargetable>();
            if (hitTgt != currentTarget)
            {
                CancelLock();
                return;
            }
        }
        else { CancelLock(); return; }

        lockTimer += Time.deltaTime;
        if (lockTimer >= lockOnTime)
        {
            RLog.Log("Locking: " + currentTarget);
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
            RLog.Log("Out of range");
            CancelLock();
        }
        if (Time.time - lockAcquiredTime > lockExpiry)
        {
            RLog.Log("Lock expired");
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

    public override bool Fire()
    {
        // CanFire now handles all readiness checks (cooldown, ammo) for all states.
        if (!CanFire())
        {
            return false;
        }

        if (!projectilePrefab) return false;
        if (!firePoint) firePoint = transform;
        
        switch (state)
        {
            case LockState.Idle:
                // TryStartLock is only called if CanFire() passed, which means we have ammo.
                return TryStartLock(PickTarget());

            case LockState.Locking:
            {
                // Dumb-fire
                MissileProjectile proj = SimplePool<MissileProjectile>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
                currentAmmo--;

                // Capture the IDamageable belonging to the shooter so the projectile can ignore self-collisions.
                IDamageable shooterDmg = GetComponentInParent<IDamageable>();

                proj.Shooter           = shooterDmg != null ? (shooterDmg as Component).gameObject : transform.root.gameObject;
                proj.ShooterDamageable = shooterDmg;
                ResetLock();
                state = LockState.Cooldown;
                nextFireTime = Time.time + fireRate; // apply cooldown only after actual shot
                return true;
            }
            case LockState.Locked:
            {
                // The check at the top of the method handles all conditions.
                RLog.Log("Firing locked missile");
                MissileProjectile proj = SimplePool<MissileProjectile>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
                currentAmmo--;

                // Capture the IDamageable belonging to the shooter so the projectile can ignore self-collisions.
                IDamageable shooterDmg = GetComponentInParent<IDamageable>();

                proj.Shooter           = shooterDmg != null ? (shooterDmg as Component).gameObject : transform.root.gameObject;
                proj.ShooterDamageable = shooterDmg;
                if (currentTarget != null) proj.SetTarget(currentTarget.TargetPoint);
                ResetLock();
                state = LockState.Cooldown;
                nextFireTime = Time.time + fireRate;
                return true;
            }
        }
        return false;
    }

    /* ───────────────────────── Helpers ───────────────────────── */
    /// <summary>Simple forward raycast to pick first <see cref="ITargetable"/> object in LOS.</summary>
    ITargetable PickTarget()
    {
        if (!firePoint) firePoint = transform;
        Vector3 dir = firePoint.up; // ship forward (top-down uses up)
        if (Physics.Raycast(firePoint.position, dir, out RaycastHit hit, maxLockDistance))
        {
            return hit.collider.GetComponentInParent<ITargetable>();
        }
        return null;
    }

#if UNITY_EDITOR
    /* ───────────────────────── Debug Gizmos ───────────────────────── */
    void OnDrawGizmos()
    {
        if (!firePoint) firePoint = transform;
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