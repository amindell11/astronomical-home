using Editor;
using Ships;
using UnityEngine;
using Utils;

namespace Weapons
{
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

        /// <summary>
        /// Raised whenever <see cref="AmmoCount"/> changes. Passes the new ammo value.
        /// </summary>
        public event System.Action<int> AmmoCountChanged;

        public int AmmoCount => currentAmmo;
        public int MaxAmmo => maxAmmo;

        LockState state = LockState.Idle;
        ITargetable currentTarget;
        float lockTimer;
        float lockAcquiredTime;

        private static readonly RaycastHit[] raycastBuffer = new RaycastHit[1];

        /// <summary>True if a target is currently locked.</summary>
        public bool IsLocked => state == LockState.Locked;

        /// <summary>Current lock-on state for UI display.</summary>
        public LockState State => state;

        /// <summary>Lock progress from 0-1 during locking phase.</summary>
        public float LockProgress => (state == LockState.Locking && lockOnTime > 0f) ? Mathf.Clamp01(lockTimer / lockOnTime) : 0f;

        /// <summary>Resets ammo count to maximum.</summary>
        public override void Reset()
        {
            currentAmmo = maxAmmo;
            AmmoCountChanged?.Invoke(currentAmmo);
            CancelLock();
        }

        /// <summary>Begin lock-on sequence towards the given target (if valid).</summary>
        public bool TryStartLock(ITargetable candidate)
        {
            if (Time.time < NextFireTime) return false;
        
            if (currentAmmo <= 0)
            {
                return false;
            }
        
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
            ResetLock();
            state         = LockState.Idle;
        }

        void Start()
        {
            Reset();
        }

        void FixedUpdate()
        {
            switch (state)
            {
                case LockState.Idle:     UpdateIdleState();     break;
                case LockState.Locking:  UpdateLockingState();  break;
                case LockState.Locked:   UpdateLockedState();   break;
                case LockState.Cooldown: UpdateCooldownState(); break;
            }
        }

        void UpdateIdleState()
        {
            if (Time.time >= NextFireTime && currentAmmo > 0)
            {
                ScanForTarget();
            }
        }

        void UpdateLockingState()
        {
            if (currentAmmo <= 0)
            {
                CancelLock();
                return;
            }

            if (!IsTargetStillValid())
            {
                CancelLock();
                return;
            }

            currentTarget?.Lock.Progress?.Invoke(LockProgress);

            lockTimer += Time.deltaTime;
            if (lockTimer >= lockOnTime)
            {
                state = LockState.Locked;
                lockAcquiredTime = Time.time;
                currentTarget?.Lock.Acquired?.Invoke();
            }
        }

        void UpdateLockedState()
        {
            if (currentAmmo <= 0)
            {
                CancelLock();
                return;
            }
        
            bool lockExpired = Time.time - lockAcquiredTime > lockExpiry;
            if (lockExpired || !IsTargetStillValid())
            {
                CancelLock();
            }
        }
    
        void UpdateCooldownState()
        {
            if (Time.time >= NextFireTime)
            {
                state = LockState.Idle;
            }
        }
    
        /// <summary>Checks if the current target is still valid for locking.</summary>
        bool IsTargetStillValid()
        {
            if (!ValidateTarget(currentTarget))
            {
                return false;
            }

            Vector3 dirToTarget = currentTarget.TargetPoint.position - firePoint.position;
            float dist = dirToTarget.magnitude;

            if (dist > maxLockDistance)
            {
                return false;
            }

            float angle = Vector3.Angle(firePoint.up, dirToTarget.normalized);
            if (angle > lockOnConeAngle / 2f)
            {
                return false;
            }

            bool losClear = LineOfSight.IsClear(
                firePoint.position,
                currentTarget.TargetPoint.position,
                currentTarget.TargetPoint.root);
            if (!losClear)
            {
                return false;
            }

            return true;
        }

        /// <summary>Resets all locking-related state variables without changing the main FSM state.</summary>
        void ResetLock()
        {
            currentTarget?.Lock.Released?.Invoke();

            currentTarget = null;
            lockTimer     = 0f;
        }

        bool ValidateTarget(ITargetable tgt) => tgt != null && tgt.TargetPoint != null;

        public override bool CanFire()
        {
            return base.CanFire() && currentAmmo > 0;
        }

        public override ProjectileBase Fire()
        {
            bool wasLocked = state == LockState.Locked && currentTarget != null;

            MissileProjectile proj = base.Fire() as MissileProjectile;

            if (proj == null)
            {
                return null;
            }
        
            currentAmmo--;
            AmmoCountChanged?.Invoke(currentAmmo);

            if (wasLocked)
            {
                proj.SetTarget(currentTarget.TargetPoint);
            }

            ResetLock();
            state = LockState.Cooldown;
        
            return proj;
        }
        
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

            currentTarget = candidate;
            lockTimer = 0f;
            state = LockState.Locking;
            return true;
        }
    
        /// <summary>Simple forward raycast to pick first <see cref="ITargetable"/> object in LOS.</summary>
        ITargetable FindBestTargetInCone()
        {
            var shipMask = LayerIds.Mask(LayerIds.Ship);
            int colliderCount = Physics.OverlapSphereNonAlloc(firePoint.position, maxLockDistance, PhysicsBuffers.GetColliderBuffer(32), shipMask);
        
            ITargetable bestCandidate = null;
            float smallestAngle = lockOnConeAngle / 2f;
            Ships.Ship selfShip = GetComponentInParent<Ships.Ship>();

            for (int i = 0; i < colliderCount; i++)
            {
                var col = PhysicsBuffers.GetColliderBuffer(32)[i];
                var targetable = col.GetComponentInParent<ITargetable>();
            
                if (targetable == null || !ValidateTarget(targetable)) 
                {
                    continue;
                }
            
                if ((targetable as Ships.Ship) == selfShip)
                {
                    continue;
                }

                Vector3 dirToTarget = (targetable.TargetPoint.position - firePoint.position);
                float angle = Vector3.Angle(firePoint.up, dirToTarget.normalized);

                if (angle < smallestAngle)
                {
                    int hitCount = Physics.RaycastNonAlloc(firePoint.position, dirToTarget.normalized, raycastBuffer, dirToTarget.magnitude);
                    if (hitCount > 0)
                    {
                        if (raycastBuffer[0].collider.GetComponentInParent<ITargetable>() == targetable)
                        {
                            smallestAngle = angle;
                            bestCandidate = targetable;
                        }
                    }
                }
            }
        
            return bestCandidate;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Vector3 origin = firePoint.position;
            Vector3 forward = firePoint.up;

            Color stateColor = state switch
            {
                LockState.Idle => Color.white,
                LockState.Locking => Color.yellow,
                LockState.Locked => Color.green,
                LockState.Cooldown => Color.gray,
                _ => Color.gray
            };

            Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f);
            Gizmos.DrawWireSphere(origin, maxLockDistance);

            Gizmos.color = stateColor;
            Vector3 forwardEnd = origin + forward * maxLockDistance;
            Gizmos.DrawRay(origin, forward * maxLockDistance);

            if (currentTarget != null && currentTarget.TargetPoint != null)
            {
                Vector3 targetPos = currentTarget.TargetPoint.position;
            
                Gizmos.color = stateColor;
                Gizmos.DrawLine(origin, targetPos);
            
                Gizmos.color = state == LockState.Locked ? Color.green : Color.red;
                Gizmos.DrawWireSphere(targetPos, 1f);
            }

            if (state == LockState.Locking && lockOnTime > 0f)
            {
                float progress = Mathf.Clamp01(lockTimer / lockOnTime);
                Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            
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

            UnityEditor.Handles.color = stateColor;
            float cooldownRemaining = Mathf.Max(0, NextFireTime - Time.time);
            string ammoText = $"Ammo: {currentAmmo}/{maxAmmo}";
            UnityEditor.Handles.Label(origin + Vector3.up * 3f, $"Missile: {state}\n{ammoText}\nTimer: {lockTimer:F1}s\nCooldown: {cooldownRemaining:F1}s");
        }
#endif
    }
} 
