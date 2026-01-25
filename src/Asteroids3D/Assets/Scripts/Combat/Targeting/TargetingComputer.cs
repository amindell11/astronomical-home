using Combat.Projectile;
using Combat.Weapons;
using Game;
using Ships;
using UnityEngine;
using Utils;

namespace Combat.Targeting
{
    public partial class TargetingComputer : MonoBehaviour
    {
        [Header("Lock-On Settings")]
        [SerializeField] private float lockOnConeAngle = 30f;
        [SerializeField] private float lockOnTime     = 0.6f;
        [SerializeField] private float lockExpiry     = 3f;
        [SerializeField] private float maxLockDistance= 100f;
        [SerializeField] private float scanInterval   = 0.1f;

        [SerializeField] private Transform firePoint;
        [SerializeField] private WeaponBase<Missile> weapon;

        private LockController lockController;
        private Sensors.FanSensor sensor;
        private ShipRegistry registry;
        private ShipId selfShipId;

        public LockState State => lockController?.State ?? LockState.Idle;
        public ITargetable CurrentTarget => lockController?.CurrentTarget;
        public float LockProgress => lockController?.LockProgress ?? 0f;
        public bool IsLocked => lockController?.IsLocked ?? false;

        private void Awake()
        {
            if (!weapon)
                weapon = GetComponent<WeaponBase<Missile>>();

            var selfShip = GetComponentInParent<Ship>();
            if (selfShip)
                selfShipId = new ShipId(selfShip.GetInstanceID());
        }

        private void Start()
        {
            registry = GameContext.Instance.ShipRegistry;
            lockController = new LockController(lockOnTime, lockExpiry, () => weapon.CanFire());

            sensor = new Sensors.FanSensor(
                firePoint,
                maxLockDistance,
                lockOnConeAngle,
                5f,
                2f,
                LayerIds.Mask(LayerIds.Ship)
            );
        }

        private void OnEnable()
        {
            if (lockController != null)
                StartCoroutine(ScanRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void FixedUpdate()
        {
            lockController.Update(Time.deltaTime, IsAcquired(CurrentTarget));
        }

        private System.Collections.IEnumerator ScanRoutine()
        {
            var wait = new WaitForSeconds(scanInterval);
            while (enabled)
            {
                if (lockController.CanStartNewLock())
                    ScanForTarget();
                yield return wait;
            }
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
        private bool InRange(ITargetable t) => t.TargetPoint.position.magnitude <= maxLockDistance;
        private bool InCone(ITargetable t) => Vector3.Angle(firePoint.up, (t.TargetPoint.position - firePoint.position).normalized) <= lockOnConeAngle / 2f;
        private bool InLineOfSight(ITargetable t) => LineOfSight.IsClear(firePoint.position, t.TargetPoint.position, t.TargetPoint.root);

        private ITargetable FindBestTargetInCone()
        {
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
               // if (!LineOfSight.IsClear(firePoint.position, ship.TargetPoint.position, ship.TargetPoint.root)) continue;

                smallestAngle = angle;
                bestCandidate = ship;
            }

            return bestCandidate;
        }
    }
}
