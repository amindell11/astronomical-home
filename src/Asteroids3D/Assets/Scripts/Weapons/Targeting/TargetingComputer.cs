using System;
using Editor;
using Ships;
using UnityEngine;
using Utils;
using Weapons;
using Ships.Weapons.Conditions;

namespace Weapons
{
    public partial class TargetingComputer : MonoBehaviour
    {

        [Header("Lock-On Settings")]
        [SerializeField] private float lockOnConeAngle = 30f;
        [SerializeField] private float lockOnTime     = 0.6f;
        [SerializeField] private float lockExpiry     = 3f;
        [SerializeField] private float maxLockDistance= 100f;
        
        [SerializeField] private Transform firePoint;
        [SerializeField] private LauncherBase<Missile> launcher;

        public LockState State { get; private set; } = LockState.Idle;
        
        public ITargetable CurrentTarget { get; private set; }
        float lockTimer;
        float lockAcquiredTime;
        
        private static readonly RaycastHit[] RaycastBuffer = new RaycastHit[1];
        
        public float LockProgress => (State == LockState.Locking && lockOnTime > 0f) ? Mathf.Clamp01(lockTimer / lockOnTime) : 0f;
        public bool IsLocked => State == LockState.Locked;
        private bool IsValid(ITargetable t) => t != null && t.TargetPoint;
        private bool InRange(ITargetable t) => t.TargetPoint.position.magnitude <= maxLockDistance;
        private bool InCone(ITargetable t) => Vector3.Angle(firePoint.up, (t.TargetPoint.position - firePoint.position).normalized) <= lockOnConeAngle / 2f;
        private bool InLineOfSight(ITargetable t) => LineOfSight.IsClear(firePoint.position, t.TargetPoint.position, t.TargetPoint.root);
        private bool IsAcquired(ITargetable t) => IsValid(t) && InRange(t) && InCone(t) && InLineOfSight(t);
        
        private void Start()
        {
            if (!launcher)
            {
                launcher = GetComponent<LauncherBase<Missile>>();
            }
        }
        
        private void FixedUpdate()
        {
            switch (State)
            {
                case LockState.Idle:     UpdateIdleState();     break;
                case LockState.Locking:  UpdateLockingState();  break;
                case LockState.Locked:   UpdateLockedState();   break;
                case LockState.Cooldown: UpdateCooldownState(); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void UpdateIdleState()
        {
            if (launcher.CanFire())
                ScanForTarget();
        }

        private void UpdateLockingState()
        {
            if (!IsAcquired(CurrentTarget))
            {
                CancelLock();
                return;
            }

            CurrentTarget?.Lock.Progress?.Invoke(LockProgress);

            lockTimer += Time.deltaTime;
            if (!(lockTimer >= lockOnTime)) return;
            
            State = LockState.Locked;
            lockAcquiredTime = Time.time;
            CurrentTarget?.Lock.Acquired?.Invoke();
        }

        private void UpdateLockedState()
        {
            var lockExpired = Time.time - lockAcquiredTime > lockExpiry;
            if (lockExpired || !IsAcquired(CurrentTarget))
            {
                CancelLock();
            }
        }

        private void UpdateCooldownState()
        {
            if (launcher.CanFire())
            {
                State = LockState.Idle;
            }
        }
                
        private void ScanForTarget()
        {
            var bestTarget = FindBestTargetInCone();
            if (bestTarget != null)
                StartLock(bestTarget);
        }
    
        private bool StartLock(ITargetable candidate)
        {
            if (candidate == null || State != LockState.Idle) return false;

            CurrentTarget = candidate;
            lockTimer = 0f;
            State = LockState.Locking;
            return true;
        }

        private void CancelLock()
        {
            ResetLock();
            State         = LockState.Idle;
        }
        private void ResetLock()
        {
            CurrentTarget?.Lock.Released?.Invoke();
            CurrentTarget = null;
            lockTimer     = 0f;
        }

        public ITargetable ConsumeLock()
        {
            if (!IsLocked) return null;

            var lockedTarget = CurrentTarget;
            ResetLock();
            State = LockState.Cooldown;
            return lockedTarget;
        }
        
        /// <summary>TODO (Terrible method) Simple forward raycast to pick first <see cref="ITargetable"/> object in LOS.</summary>
        private ITargetable FindBestTargetInCone()
        {
            var shipMask = LayerIds.Mask(LayerIds.Ship);
            var colliderCount = Physics.OverlapSphereNonAlloc(firePoint.position, maxLockDistance, PhysicsBuffers.GetColliderBuffer(32), shipMask);
        
            ITargetable bestCandidate = null;
            var smallestAngle = lockOnConeAngle / 2f;
            var selfShip = GetComponentInParent<Ships.Ship>();

            for (var i = 0; i < colliderCount; i++)
            {
                var col = PhysicsBuffers.GetColliderBuffer(32)[i];
                var targetable = col.GetComponentInParent<ITargetable>();
            
                if (targetable == null || !IsValid(targetable)) 
                {
                    continue;
                }
            
                if ((targetable as Ships.Ship) == selfShip)
                {
                    continue;
                }

                var dirToTarget = (targetable.TargetPoint.position - firePoint.position);
                var angle = Vector3.Angle(firePoint.up, dirToTarget.normalized);

                if (angle < smallestAngle)
                {
                    var hitCount = Physics.RaycastNonAlloc(firePoint.position, dirToTarget.normalized, RaycastBuffer, dirToTarget.magnitude);
                    if (hitCount > 0)
                    {
                        if (RaycastBuffer[0].collider.GetComponentInParent<ITargetable>() == targetable)
                        {
                            smallestAngle = angle;
                            bestCandidate = targetable;
                        }
                    }
                }
            }
        
            return bestCandidate;
        }
    }
}