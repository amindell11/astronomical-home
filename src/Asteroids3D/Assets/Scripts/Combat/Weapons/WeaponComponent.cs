using System;
using System.Collections.Generic;
using System.Linq;
using AI;
using AI.Scanning;
using Combat.Projectiles;
using Combat.Targeting;
using Movement;
using UnityEngine;
using Substrate;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;

namespace Combat.Weapons
{
    /// <summary>Target geometry fed to <see cref="WeaponComponent.InEnvelope"/>/<see cref="WeaponComponent.HoldTrigger"/>.</summary>
    public struct TargetingContext
    {
        public Vector2 targetPosition;
        public float distanceToTarget;
        public float angleToTarget;
        public bool hasLineOfSight;

        public bool Within(float maxDistance, float maxAngle) =>
            hasLineOfSight && distanceToTarget <= maxDistance && angleToTarget <= maxAngle;
    }

    public abstract class WeaponComponent : MonoBehaviour
    {
        public Transform firePoint;

        public Vector3 FireOrigin => GamePlane.WorldPointOnPlane((firePoint ? firePoint : transform).position);
        public Vector3 FireDirection => GamePlane.WorldDirOnPlane((firePoint ? firePoint : transform).up);

        [Tooltip("Name shown on this weapon's HUD readout panel. Empty = the prefab name.")]
        [SerializeField] private string displayName;
        protected IShooter shooter;
        protected WeaponCondition[] conditions;
        private List<IWeaponReadout> readouts;
        
        public event Action OnFire;

        /// <summary>The mods in force on this mount; the authored weapon until a controller hands some down.</summary>
        public WeaponMods Mods { get; private set; } = WeaponMods.None;

        protected virtual void Awake()
        {
            shooter = GetComponentInParent<IShooter>();
            conditions = GetComponents<WeaponCondition>();
            if (!firePoint) firePoint = transform;
            foreach (var condition in conditions)
            {
                condition.Initialize(this);
                condition.SetMods(Mods);
            }
        }

        /// <summary>Puts <paramref name="mods"/> in force on this mount and its conditions; pre-Awake they are handed down in Awake.</summary>
        public void SetMods(WeaponMods mods)
        {
            Mods = mods ?? throw new ArgumentNullException(nameof(mods));
            if (conditions == null) return;
            foreach (var condition in conditions)
                condition.SetMods(mods);
        }

        // Serialized refs, not Awake's lookup, so a prefab asset judges fit like its mount.
        protected virtual WeaponCondition[] AuthoredConditions => Array.Empty<WeaponCondition>();

        /// <summary>Whether this weapon has the field a mod changes, prefab asset or live mount alike.</summary>
        public virtual bool Supports(WeaponModField field) => field switch
        {
            WeaponModField.FireRate => Has<Cooldown>(),
            WeaponModField.Damage => true,
            WeaponModField.HeatCapacity or WeaponModField.Cooling => Has<Heat>(),
            WeaponModField.Magazine => Has<Rounds>(),
            WeaponModField.ReloadSpeed => Reloads(),
            WeaponModField.ChargeSpeed => Has<ChargeTime>(),
            _ => false,
        };

        private bool Reloads()
        {
            var rounds = Condition<Rounds>();
            return rounds && rounds.ReloadTime > 0f;
        }

        private bool Has<T>() where T : WeaponCondition => Condition<T>();

        private T Condition<T>() where T : WeaponCondition
        {
            foreach (var condition in AuthoredConditions)
            {
                var match = condition as T;
                if (match) return match;
            }
            return null;
        }

        /// <summary>Fires one shot at <paramref name="targetPoint"/>. The live-projectile registry is a per-call capability, never stored — a call site without one in hand cannot compile.</summary>
        public abstract ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles);

        /// <summary>Muzzle speed of this weapon's projectile, used for AI intercept lead. 0 if not applicable.</summary>
        public virtual float ProjectileSpeed => 0f;

        /// <summary>Max distance at which an AI gunner engages with this weapon, for diagnostics/telemetry. 0 if not distance-gated.</summary>
        public virtual float FireRange => 0f;

        /// <summary>Where an AI gunner aims this weapon at a target: by default the intercept lead for <see cref="ProjectileSpeed"/>.</summary>
        public virtual Vector2 AimPoint(in Kinematics shooterPose, Vector2 targetPos, Vector2 targetVel) =>
            Gunner.AimPoint(in shooterPose, targetPos, targetVel, ProjectileSpeed);

        /// <summary>The distance an AI pilot carrying this weapon holds from its target; null leaves the pilot's own.</summary>
        public virtual float? HoldRange => null;

        /// <summary>Writes the circles an AI pilot carrying this weapon keeps out of right now; returns how many. None by default.</summary>
        public virtual int KeepOutCircles(Span<DetectedObstacle.PlaneCircle> circles) => 0;

        /// <summary>True while this weapon holds its ship's engines off: no thrust, strafe or keystone, only turning.</summary>
        public virtual bool CutsEngines => false;

        /// <summary>Full-auto repeats while held; semi-auto fires once per press. Only this weapon's <see cref="HandleTrigger"/> interprets it.</summary>
        public virtual bool AutoFire => true;

        /// <summary>Applies one step of trigger state; the weapon owns its firing semantics (charge weapons override to fire on release/full charge).</summary>
        public virtual void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            if (AutoFire ? cmd.held : cmd.pressed)
                Fire(cmd.targetPoint, projectiles);
        }

        public void Reload()
        {
            foreach (var condition in conditions)
                if (condition is Rounds rounds) rounds.Reload();
        }

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        protected bool TrySpendFireConditions()
        {
            if (!CanFire()) return false;
            foreach (var condition in conditions)
                condition.ProcessFire();
            return true;
        }

        /// <summary>Name shown on this weapon's HUD readout panel.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? name.Replace("(Clone)", string.Empty).Trim()
            : displayName;

        /// <summary>Displayable state (readout conditions, lock source, the weapon's own readout), built lazily post-Awake; pre-Awake returns empty WITHOUT caching.</summary>
        public IReadOnlyList<IWeaponReadout> Readouts
        {
            get
            {
                if (readouts != null) return readouts;
                if (conditions == null) return Array.Empty<IWeaponReadout>();

                readouts = new List<IWeaponReadout>(conditions.OfType<IWeaponReadout>());
                if (LockSource != null)
                    readouts.Add(LockSource);
                var self = this as IWeaponReadout;
                if (self != null)
                    readouts.Add(self);
                return readouts;
            }
        }

        /// <summary>The lock-state source driving this weapon's guidance UI, or null if it has none.</summary>
        public virtual ILockStateSource LockSource => null;

        /// <summary>Geometric firing envelope only (distance/angle/LOS) — readiness (heat/charge/lock/ammo) excluded.</summary>
        public virtual bool InEnvelope(in TargetingContext context) => false;

        /// <summary>The weapon's fire plan, called once per engaged step; its state clears in <see cref="Reset"/>.</summary>
        public virtual bool HoldTrigger(TargetingContext context)
        {
            return false;
        }

        public virtual void Reset()
        {
            // Null before Awake (a mount instantiated under an inactive ship): nothing live to reset.
            if (conditions == null) return;
            foreach (var condition in conditions)
                condition.Reset();
        }
        // ReSharper disable Unity.PerformanceAnalysis
        protected void InvokeOnFire()
        {
            OnFire?.Invoke();
        }
    }
}
