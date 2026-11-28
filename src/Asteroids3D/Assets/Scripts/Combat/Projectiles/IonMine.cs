using Balance;
using System;
using Damage;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    /// <summary>
    /// Drops behind the carrier at rest, arms, and goes off on an enemy in proximity or at
    /// the fuse. Its IonBlast stalls enemy ships only.
    /// Design: arc #1775.
    /// </summary>
    public class IonMine : Projectile<IonMine>, ITransientSpawner, IDetonating
    {
        [Header("Drop")]
        [Tooltip("Distance from the hull's rear to the dropped mine.")]
        [Stat, SerializeField, Min(0f)] private float dropGap = 1.5f;

        [Header("Fuse")]
        [Tooltip("Seconds after the drop before proximity can set the mine off. The fuse does not wait.")]
        [Stat, SerializeField, Min(0f)] private float armingTime = 0.5f;
        [Tooltip("Seconds from the drop until the mine goes off, armed or not.")]
        [Stat, SerializeField, Min(0.01f)] private float fuseTime = 4f;
        [Tooltip("An armed mine goes off when an enemy ship comes within this distance.")]
        [Stat, SerializeField, Min(0f)] private float proximityRadius = 4f;

        [Header("Blast")]
        [Stat, SerializeField] private IonBlast blastPrefab;

        private bool detonated;
        private float sinceLaunch;

        private bool Armed => sinceLaunch >= armingTime;

        // Never deals damage; the blast only stalls.
        protected override DamageKind Kind => DamageKind.ConcussionWave;

        public IonBlast BlastPrefab => blastPrefab;
        internal float DropGap => dropGap;
        internal float ArmingTime => armingTime;
        internal float FuseTime => fuseTime;
        internal float ProximityRadius => proximityRadius;

        public event Action<MonoBehaviour, Action> Spawned;
        public event Action<Vector3> OnDetonated;

        protected override void Awake()
        {
            base.Awake();
            SimplePool<IonBlast>.Warm(blastPrefab);
        }

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {
            var carrier = AttackerShip;
            if (!carrier)
                throw new InvalidOperationException("An ion mine drops behind a ship.");
            // The hull's heading, not the muzzle's: a turret mount aims the muzzle at the cursor.
            var behind = -Vector3.ProjectOnPlane(carrier.transform.up, GamePlane.Normal).normalized;
            var drop = GamePlane.WorldPointOnPlane(carrier.transform.position + behind * (carrier.ShipRadius + dropGap));
            transform.position = drop;
            rb.position = drop;
            rb.linearVelocity = Vector3.zero;
            base.Launch(direction, targetPoint);
        }

        protected override void FixedUpdate()
        {
            sinceLaunch += Time.fixedDeltaTime;
            if (sinceLaunch >= fuseTime || Armed && EnemyInProximity())
            {
                Detonate();
                return;
            }

            base.FixedUpdate();
        }

        private bool EnemyInProximity()
        {
            var buffer = PhysicsBuffers.GetColliderBuffer();
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, proximityRadius, buffer,
                1 << LayerIds.Ship, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
                if (IonBlast.TryEnemy(buffer[i], AttackerShip, out _)) return true;
            return false;
        }

        // Contact neither detonates nor damages; proximity covers it.
        protected override void OnHit(IDamageable other) { }

        private void Detonate()
        {
            if (detonated) return;
            detonated = true;

            var blast = SimplePool<IonBlast>.Get(blastPrefab, transform.position, Quaternion.identity);
            blast.Begin(AttackerShip);
            Spawned?.Invoke(blast, blast.ReturnToPoolImmediate);
            OnDetonated?.Invoke(transform.position);
            Dispose();
        }

        protected override void OnReturnToPool()
        {
            detonated = false;
            sinceLaunch = 0f;
        }
    }
}
