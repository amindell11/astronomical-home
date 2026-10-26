using Balance;
using System;
using AI.Scanning.Sensors;
using Combat.Projectiles;
using Combat.Weapons;
using Movement;
using Ships;
using UnityEngine;
using Utils;
using Ships.Registry;
using Substrate;

namespace Combat.Targeting
{
    public class LockOnSensor : MonoBehaviour, ILockStateSource
    {
        private const float ScanInterval = 0.1f;
        private const float FanRayStepDeg = 5f;
        private const float FanSphereRadius = 2f;

        [Header("Lock-On Settings")]
        [Tooltip("Full width (degrees) of the cone around the ship's nose, measured from its centre, in which a target can be acquired and held.")]
        [Stat, SerializeField] internal float lockOnConeAngle = 30f;
        [Tooltip("Seconds a target must stay in the cone before the lock is acquired.")]
        [Stat, SerializeField] private float lockOnTime = 0.6f;
        [Tooltip("Seconds an acquired lock is held before it drops and must be re-acquired.")]
        [Stat, SerializeField] private float lockExpiry = 3f;
        [Tooltip("Max distance from the ship's centre at which a target can be locked.")]
        [Stat, SerializeField] internal float maxLockDistance = 100f;

        [SerializeField] internal WeaponBase<Missile> weapon;

        private readonly Collider[] fanHits = new Collider[64];
        private TargetLock targetLock;
        private int shipMask;
        private IShipRegistry registry;
        private ShipId selfShipId;
        internal Ship selfShip;
        private Coroutine scanRoutine;
        private WaitForSeconds scanWait;

        public event Action<LockState, LockState> OnStateChanged;

        public LockState State => targetLock?.State ?? LockState.Idle;
        public ITargetable CurrentTarget => targetLock?.CurrentTarget;
        public float LockProgress => targetLock?.LockProgress ?? 0f;
        public bool IsLocked => targetLock?.IsLocked ?? false;
        public bool HasRegistry => registry != null;

        internal Kinematics Pose => selfShip.Kinematics;

        public void SetRegistry(IShipRegistry shipRegistry)
        {
            if (ReferenceEquals(registry, shipRegistry))
                return;

            if (shipRegistry != null && !selfShip)
                throw new InvalidOperationException(
                    $"{nameof(LockOnSensor)} '{name}' has no {nameof(Ship)} above it: the sensor measures from its ship's centre, " +
                    "so only a ship-mounted sensor can take a registry.");

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

            selfShip = GetComponentInParent<Ship>();
            if (selfShip)
                selfShipId = new ShipId(selfShip.GetInstanceID());

            targetLock = new TargetLock(lockOnTime, lockExpiry, CanLock);
            targetLock.OnStateChanged += HandleTargetLockStateChanged;

            shipMask = LayerIds.Mask(LayerIds.Ship);
        }

        private void Start()
        {
            scanWait = new WaitForSeconds(ScanInterval);
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
            // Death and park deactivate the ship. The HUD loop stops only when this reports Idle.
            targetLock?.Cancel();
            StopScanRoutine();
        }

        private void OnDestroy()
        {
            targetLock?.Cancel();
            StopScanRoutine();
            if (targetLock == null) return;
            // The target's channel outlives this sensor; cancelling drops the lock's subscription to it.
            targetLock.Cancel();
            targetLock.OnStateChanged -= HandleTargetLockStateChanged;
        }

        private void FixedUpdate()
        {
            if (registry == null)
                return;
            targetLock.Update(Time.deltaTime, IsAcquired(CurrentTarget));
        }

        private System.Collections.IEnumerator ScanRoutine()
        {
            while (enabled && registry != null)
            {
                if (targetLock.CanStartNewLock())
                    ScanForTarget();
                yield return scanWait;
            }

            scanRoutine = null;
        }

        private void StartScanRoutineIfNeeded()
        {
            if (scanRoutine != null || registry == null || targetLock == null || !enabled)
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

        public ITargetable ConsumeLock() => targetLock.ConsumeLock();

        /// <summary>Drops any lock in progress or held and restarts the scan cadence (respawn reset); an inactive ship restarts via OnEnable instead.</summary>
        public void ResetLock()
        {
            targetLock?.Cancel();
            StopScanRoutine();
            if (gameObject.activeInHierarchy)
                StartScanRoutineIfNeeded();
        }

        private void ScanForTarget()
        {
            var bestTarget = FindBestTargetInCone();
            if (bestTarget != null)
                targetLock.TryStartLock(bestTarget);
        }

        private bool IsAcquired(ITargetable t)
        {
            if (!IsValid(t)) return false;
            var pose = Pose;
            var target = PlanePoint(t);
            return InRange(pose, target) && InCone(pose, target) && InLineOfSight(pose, t);
        }

        // Destroyed MonoBehaviours pass the interface null-check; liveness must test the underlying Object.
        private static bool IsValid(ITargetable t) =>
            (t is UnityEngine.Object obj ? obj : t != null) && t.TargetPoint;

        private static Vector2 PlanePoint(ITargetable t) => GamePlane.WorldPointToPlane(t.TargetPoint.position);

        private bool InRange(in Kinematics pose, Vector2 target) =>
            TargetingMath.DistanceTo(pose, target) <= maxLockDistance;

        private bool InCone(in Kinematics pose, Vector2 target) =>
            TargetingMath.AngleTo(pose, target) <= lockOnConeAngle / 2f;

        private static bool InLineOfSight(in Kinematics pose, ITargetable t) =>
            LineOfSight.IsClear(GamePlane.PlanePointToWorld(pose.pos), t.TargetPoint.position, t.TargetPoint);

        private ITargetable FindBestTargetInCone()
        {
            if (registry == null)
                return null;

            var pose = Pose;
            var colliderCount = RayFanSensor.Detect(
                GamePlane.PlanePointToWorld(pose.pos),
                GamePlane.PlaneDirToWorld(pose.Forward),
                maxLockDistance,
                lockOnConeAngle,
                FanRayStepDeg,
                FanSphereRadius,
                shipMask,
                fanHits);

            ITargetable bestCandidate = null;
            var smallestAngle = lockOnConeAngle / 2f;

            for (var i = 0; i < colliderCount; i++)
            {
                if (!registry.TryGetShip(fanHits[i], out var ship, selfShipId)) continue;
                if (!IsValid(ship) || ship.Lock.IsConcealed) continue;

                var angle = TargetingMath.AngleTo(pose, PlanePoint(ship));

                if (angle >= smallestAngle) continue;

                smallestAngle = angle;
                bestCandidate = ship;
            }

            return bestCandidate;
        }

        private void HandleTargetLockStateChanged(LockState previous, LockState next)
        {
            OnStateChanged?.Invoke(previous, next);
        }
    }
}
