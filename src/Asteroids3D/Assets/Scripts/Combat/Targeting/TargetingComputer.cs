using System;
using Combat.Projectile;
using Combat.Weapons;
using Ships;
using UnityEngine;
using Utils;

namespace Combat.Targeting
{
    public partial class TargetingComputer : MonoBehaviour, ILockStateSource, ILockProvider
    {
        [Header("Lock-On Settings")]
        [SerializeField] private float lockOnConeAngle = 30f;
        [SerializeField] private float lockOnTime = 0.6f;
        [SerializeField] private float lockExpiry = 3f;
        [SerializeField] private float maxLockDistance = 100f;
        [SerializeField] private float scanInterval = 0.1f;

        [SerializeField] private Transform firePoint;
        [SerializeField] private WeaponBase<Missile> weapon;

        private LockController lockController;
        private Sensors.FanSensor sensor;
        private IShipRegistry registry;
        private ShipId selfShipId;
        private Coroutine scanRoutine;
        private WaitForSeconds scanWait;

        public event Action<LockState, LockState> OnStateChanged;

        public LockState State => lockController?.State ?? LockState.Idle;
        public ITargetable CurrentTarget => lockController?.CurrentTarget;
        public float LockProgress => lockController?.LockProgress ?? 0f;
        public bool IsLocked => lockController?.IsLocked ?? false;
        public bool HasRegistry => registry != null;

        public void SetRegistry(IShipRegistry shipRegistry)
        {
            if (ReferenceEquals(registry, shipRegistry))
                return;

            registry = shipRegistry;

            if (registry == null)
            {
                StopScanRoutine();
                enabled = false;
                return;
            }

            if (!gameObject.activeInHierarchy)
                return;

            if (!enabled)
                enabled = true;
            else
                StartScanRoutineIfNeeded();
        }

        private void Awake()
        {
            if (!weapon)
                weapon = GetComponent<WeaponBase<Missile>>();
            if (!firePoint)
                firePoint = transform;

            var selfShip = GetComponentInParent<Ship>();
            if (selfShip)
                selfShipId = new ShipId(selfShip.GetInstanceID());

            lockController = new LockController(lockOnTime, lockExpiry, CanLock);
            lockController.OnStateChanged += HandleLockStateChanged;

            sensor = new Sensors.FanSensor(
                firePoint,
                maxLockDistance,
                lockOnConeAngle,
                5f,
                2f,
                LayerIds.Mask(LayerIds.Ship)
            );
        }

        private void Start()
        {
            scanWait = new WaitForSeconds(scanInterval);
            if (registry == null)
                enabled = false;
        }

        private bool CanLock()
        {
            return weapon && weapon.CanFire();
        }

        private void OnEnable()
        {
            StartScanRoutineIfNeeded();
        }

        private void OnDisable()
        {
            StopScanRoutine();
        }

        private void OnDestroy()
        {
            StopScanRoutine();
            if (lockController != null)
                lockController.OnStateChanged -= HandleLockStateChanged;
        }

        private void FixedUpdate()
        {
            if (registry == null)
                return;
            lockController.Update(Time.deltaTime, IsAcquired(CurrentTarget));
        }

        private System.Collections.IEnumerator ScanRoutine()
        {
            while (enabled && registry != null)
            {
                if (lockController.CanStartNewLock())
                    ScanForTarget();
                yield return scanWait;
            }

            scanRoutine = null;
        }

        private void StartScanRoutineIfNeeded()
        {
            if (scanRoutine != null || registry == null || lockController == null || !enabled)
                return;
            scanRoutine = StartCoroutine(ScanRoutine());
        }

        private void StopScanRoutine()
        {
            if (scanRoutine == null)
                return;

            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }

        public ITargetable ConsumeLock() => lockController.ConsumeLock();

        private void ScanForTarget()
        {
            var bestTarget = FindBestTargetInCone();
            if (bestTarget != null)
                lockController.TryStartLock(bestTarget);
        }

        private bool IsAcquired(ITargetable t) =>
            IsValid(t) && InRange(t) && InCone(t) && InLineOfSight(t);

        private static bool IsValid(ITargetable t) => t != null && t.TargetPoint;

        private bool InRange(ITargetable t)
        {
            return Vector3.Distance(firePoint.position, t.TargetPoint.position) <= maxLockDistance;
        }

        private bool InCone(ITargetable t)
        {
            return Vector3.Angle(firePoint.up, (t.TargetPoint.position - firePoint.position).normalized) <= lockOnConeAngle / 2f;
        }

        private bool InLineOfSight(ITargetable t)
        {
            return LineOfSight.IsClear(firePoint.position, t.TargetPoint.position, t.TargetPoint.root);
        }

        private ITargetable FindBestTargetInCone()
        {
            if (registry == null)
                return null;

            var colliderCount = sensor.Detect(firePoint.up);
            var colliders = sensor.Buffer;

            ITargetable bestCandidate = null;
            var smallestAngle = lockOnConeAngle / 2f;

            for (var i = 0; i < colliderCount; i++)
            {
                if (!registry.TryGetShip(colliders[i], out var ship, selfShipId)) continue;
                if (!IsValid(ship)) continue;

                var dirToTarget = ship.TargetPoint.position - firePoint.position;
                var angle = Vector3.Angle(firePoint.up, dirToTarget.normalized);

                if (angle >= smallestAngle) continue;

                smallestAngle = angle;
                bestCandidate = ship;
            }

            return bestCandidate;
        }

        private void HandleLockStateChanged(LockState previous, LockState next)
        {
            OnStateChanged?.Invoke(previous, next);
        }
    }
}
