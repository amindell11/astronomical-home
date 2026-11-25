using System;
using System.Collections.Generic;
using AI.Scanning;
using Balance;
using Combat.Projectiles;
using Movement;
using Substrate;
using UnityEngine;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Substrate.Services.Projectiles;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Announces each charge the weapon launches, so the owner's HUD can mark where it will stop.</summary>
    public interface IChargeLaunchReadout : IWeaponReadout
    {
        event Action<IChargeFlight> Launched;
    }

    /// <summary>
    /// Concussion charge launcher: semi-auto, each charge flies to the trigger's target point (see <see cref="Grenade.Launch"/>).
    /// An AI gunner aims it by the charge's braked flight and launches only clear of its own blast,
    /// never while one of its own charges or blasts is still live; its AI pilot keeps out of both.
    /// </summary>
    public class Grenades : WeaponBase<Grenade>, IChargeLaunchReadout
    {
        [Header("AI Firing")]
        [Tooltip("Distance an AI pilot carrying this launcher holds from its target.")]
        [Stat, SerializeField, Min(0f)] private float holdRange = 18f;
        [Tooltip("An AI gunner launches only at an aim point at least the blast radius plus this margin away.")]
        [Stat, SerializeField, Min(0f)] private float blastMargin = 4f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        // Own live charges and blasts, each mapped to its unsubscribe action.
        private readonly Dictionary<MonoBehaviour, Action> live = new();

        public override bool AutoFire => false;
        public override bool IsOrdnance => true;
        public override float? HoldRange => holdRange;
        public Rounds Rounds => rounds;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public float BlastDamage => projectilePrefab.WavePrefab.MaxDamage;
        public float BlastRadius => projectilePrefab.WavePrefab.MaxRadius;
        public float Range => projectilePrefab.MaxDistance;
        internal float MinLobDistance => BlastRadius + blastMargin;

        public event Action<IChargeFlight> Launched;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override bool Supports(WeaponModField field) =>
            field == WeaponModField.BlastRadius || base.Supports(field);

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var charge = (Grenade)base.Fire(targetPoint, projectiles);
            if (!charge) return null;
            charge.SetBlastRadiusScale(Mods.Factor(WeaponModField.BlastRadius));
            Track(charge);
            Launched?.Invoke(charge);
            return charge;
        }

        // No ship velocity. Within the burn distance b a charge reaches v₀t/2, past it v₀t − b (Grenade.FlightTime).
        public override Vector2 AimPoint(in Kinematics shooterPose, Vector2 targetPos, Vector2 targetVel)
        {
            var offset = targetPos - shooterPose.pos;
            var speed = projectilePrefab.LaunchSpeed;
            var burnDistance = projectilePrefab.BurnDistance;
            var flightTime = InterceptTime(offset, targetVel, 0.5f * speed, 0f);
            if (0.5f * speed * flightTime > burnDistance)
                flightTime = InterceptTime(offset, targetVel, speed, burnDistance);
            flightTime = Mathf.Min(flightTime, projectilePrefab.FlightTime(Range));
            return targetPos + targetVel * flightTime;
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= Range
            && context.distanceToTarget >= MinLobDistance;

        // A charge launched into a live charge's blast detonates there, by the launcher.
        public override bool HoldTrigger(TargetingContext context) =>
            live.Count == 0 && CanFire() && InEnvelope(in context);

        // A charge may detonate short of its stop point, so its circle adds the lob margin.
        public override int KeepOutCircles(Span<DetectedObstacle.PlaneCircle> circles)
        {
            var count = 0;
            foreach (var transient in live.Keys)
            {
                var charge = transient as Grenade;
                circles[count++] = charge
                    ? new DetectedObstacle.PlaneCircle(GamePlane.WorldPointToPlane(charge.TargetPoint), MinLobDistance)
                    : new DetectedObstacle.PlaneCircle(GamePlane.WorldPointToPlane(transient.transform.position), BlastRadius);
            }
            return count;
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var unsubscribe in live.Values)
                unsubscribe();
            live.Clear();
        }

        /// <summary>When a shot reaching <paramref name="speed"/>·t − <paramref name="lag"/> first meets a coasting target; infinite if never.</summary>
        private static float InterceptTime(Vector2 offset, Vector2 targetVel, float speed, float lag)
        {
            var a = targetVel.sqrMagnitude - speed * speed;
            var b = 2f * (Vector2.Dot(offset, targetVel) + speed * lag);
            var c = offset.sqrMagnitude - lag * lag;
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return float.PositiveInfinity;

            // Roots as q/a and c/q stay precise as a nears zero; a zero divisor yields no root.
            var q = -0.5f * (b + (b < 0f ? -1f : 1f) * Mathf.Sqrt(discriminant));
            // A root before lag/speed solves the mirrored reach, lag − speed·t.
            var earliest = lag / speed;
            var t = float.PositiveInfinity;
            if (q / a >= earliest) t = q / a;
            if (c / q >= earliest) t = Mathf.Min(t, c / q);
            return t;
        }

        private void Track(Grenade charge)
        {
            Action<MonoBehaviour, Action> onSpawned = (child, _) => TrackBlast((ConcussionWave)child);
            Action onEnded = () => Untrack(charge);
            charge.Spawned += onSpawned;
            charge.ReturnedToPool += onEnded;
            live.Add(charge, () =>
            {
                charge.Spawned -= onSpawned;
                charge.ReturnedToPool -= onEnded;
            });
        }

        private void TrackBlast(ConcussionWave blast)
        {
            Action onReleased = () => Untrack(blast);
            blast.Released += onReleased;
            live.Add(blast, () => blast.Released -= onReleased);
        }

        private void Untrack(MonoBehaviour transient)
        {
            if (live.Remove(transient, out var unsubscribe))
                unsubscribe();
        }
    }
}
