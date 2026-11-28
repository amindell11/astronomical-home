using Balance;
using Combat.Projectiles;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectiles.Missile;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;

namespace Combat.Weapons.Arsenal
{
    public interface IWireReadout : IWeaponReadout
    {
        bool WireLive { get; }
    }

    public class Missiles : WeaponBase<Missile>, IWireReadout
    {
        [Header("Targeting")]
        [Stat, SerializeField] private LockOnSensor targetingComputer;

        [Header("Wire Guided")]
        [SerializeField] private WeaponBehaviourMod wireGuided;

        [Header("EMP Warhead")]
        [SerializeField] private WeaponBehaviourMod empWarhead;
        [Tooltip("With EMP warhead held: seconds a direct hit jams the target's weapons and locks.")]
        [Stat, SerializeField, Min(0.01f)] private float empJam = 2f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        [Header("AI Firing")]
        [Tooltip("Distance an AI pilot carrying this weapon holds from its target.")]
        [Stat, SerializeField, Min(0f)] private float holdRange = 13f;

        [Header("AI Firing (No Lock)")]
        [Tooltip("Max distance at which an AI gunner will fire unguided (no lock). Keep it inside the distance the scripted pilots hold from their target: a pilot hovering on this edge launches unguided.")]
        [Stat, SerializeField, Min(0f)] private float fallbackRange = 7f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will fire unguided (no lock).")]
        [Stat, SerializeField, Range(0f, 180f)] private float fallbackAngleTolerance = 15f;

        private Vector3 aimPoint;
        private int wiresLive;

        public Rounds Rounds => rounds;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public float Damage => projectilePrefab.Damage;
        public float SplashDamage => projectilePrefab.SplashDamage;

        public override ILockStateSource LockSource => targetingComputer;
        public bool WireGuided => Behaviours.Holds(wireGuided);
        public bool WireLive => wiresLive > 0;
        public override bool IsOrdnance => true;

        // Missiles are semi-auto: one launch per trigger press, not a held stream.
        public override bool AutoFire => false;
        public override float? HoldRange => holdRange;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
            if (!targetingComputer)
                targetingComputer = GetComponent<LockOnSensor>();
        }

        protected override void Prime(Missile missile)
        {
            if (Behaviours.Holds(empWarhead)) missile.Jam(empJam);
        }

        public override void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            aimPoint = cmd.targetPoint;
            base.HandleTrigger(in cmd, projectiles);
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var proj = base.Fire(targetPoint, projectiles) as Missile;
            if (!proj)
                return null;

            if (WireGuided)
            {
                proj.Wire(() => aimPoint);
                Track(proj);
                return proj;
            }

            if (targetingComputer)
            {
                var holdFor = targetingComputer.GraceLeft;
                var lockedTarget = targetingComputer.ConsumeLock();
                if (lockedTarget != null)
                    proj.SetTarget(lockedTarget, holdFor);
            }

            return proj;
        }

        private void Track(Missile wired)
        {
            void Landed()
            {
                wired.ReturnedToPool -= Landed;
                wiresLive--;
            }

            wired.ReturnedToPool += Landed;
            wiresLive++;
        }

        // The lock cone, or the dumbfire window where an AI gunner launches without a lock.
        public override bool InEnvelope(in TargetingContext context) =>
            InLockCone(in context) || InFallbackEnvelope(in context);

        private bool InLockCone(in TargetingContext context) =>
            targetingComputer
            && context.Within(targetingComputer.MaxLockDistance, targetingComputer.lockOnConeAngle * 0.5f);

        private bool InFallbackEnvelope(in TargetingContext context) =>
            context.Within(fallbackRange, fallbackAngleTolerance);

        // Not a strict InEnvelope && readiness factorization: a held lock fires regardless of current geometry.
        public override bool HoldTrigger(TargetingContext context)
        {
            // Readiness drops the solution after each launch, so the gunner's press edge re-arms.
            if (!CanFire())
                return false;

            var lockState = LockState.Idle;
            if (targetingComputer)
                lockState = targetingComputer.State;

            return lockState switch
            {
                LockState.Locked => true,
                LockState.Idle or LockState.Locking => InFallbackEnvelope(in context),
                _ => false,
            };
        }
    }
}
