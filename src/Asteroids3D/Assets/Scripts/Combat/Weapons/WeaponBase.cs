using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using UnityEngine;
using Utils;

namespace Combat.Weapons
{
    /// <summary>
    /// Contextual data about a target used for weapon firing decisions
    /// (the input to <see cref="WeaponComponent.ShouldFire"/>).
    /// </summary>
    public struct TargetingContext
    {
        public Vector2 targetPosition;
        public float distanceToTarget;
        public float angleToTarget;
        public bool hasLineOfSight;
    }

    /// <summary>
    /// Marker for a weapon's displayable state contracts (heat, ammo, lock…). A weapon exposes
    /// its readouts via <see cref="WeaponComponent.Readouts"/>; the HUD maps each readout
    /// interface to a widget. Implementations are the sim objects themselves — the contract is
    /// read-only and event-driven, so UI never touches sim internals.
    /// </summary>
    public interface IWeaponReadout
    {
    }

    public abstract class WeaponComponent : MonoBehaviour
    {
        [SerializeField] public Transform firePoint;
        [Tooltip("Name shown on this weapon's HUD readout panel. Empty = the prefab name.")]
        [SerializeField] private string displayName;
        protected IShooter shooter;
        protected WeaponCondition[] conditions;
        private List<IWeaponReadout> readouts;
        
        public event Action OnFire;

        protected virtual void Awake()
        {            
            shooter = GetComponentInParent<IShooter>();
            conditions = GetComponents<WeaponCondition>();
            if (!firePoint) firePoint = transform;
            foreach (var condition in conditions)
                condition.Initialize(this);
        }

        public abstract ProjectileBase Fire();

        /// <summary>Muzzle speed of this weapon's projectile, used for AI intercept lead. 0 if not applicable.</summary>
        public virtual float ProjectileSpeed => 0f;

        /// <summary>
        /// Whether the weapon repeats while the trigger is held (full-auto, paced by its cooldown),
        /// or fires once per trigger press (semi-auto). Consumed by this weapon's own
        /// <see cref="HandleTrigger"/> — commanders send raw trigger state and never interpret it.
        /// </summary>
        public virtual bool AutoFire => true;

        /// <summary>
        /// Applies one step of trigger state; the weapon owns its firing semantics. Default:
        /// full-auto fires while the trigger is held, semi-auto on each press (an AI commander
        /// "mashes" — reports a press every step it wants fire — so semi-auto still paces by its
        /// own conditions under sustained AI intent). Charge weapons override to accumulate while
        /// held and fire on release or at full charge.
        /// </summary>
        public virtual void HandleTrigger(bool pressed, bool held)
        {
            if (AutoFire ? held : pressed)
                Fire();
        }

        public virtual bool CanFire()
        {
            return conditions.All(c => c.CanFire());
        }

        /// <summary>Name shown on this weapon's HUD readout panel.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? name.Replace("(Clone)", string.Empty).Trim()
            : displayName;

        /// <summary>
        /// Stat line shown when hovering this weapon in the hangar. The hangar reads it off the
        /// prefab asset, so overrides must use serialized config (GetComponent is fine), never
        /// Awake-cached state.
        /// </summary>
        public virtual string HangarStats => DisplayName;

        /// <summary>
        /// The displayable state this weapon carries (conditions implementing
        /// <see cref="IWeaponReadout"/> plus its <see cref="LockSource"/>), in display order.
        /// Built lazily so subclasses can finish wiring their lock source in Awake first.
        /// Before Awake (e.g. a mount instantiated under an inactive ship) nothing is wired yet —
        /// return empty WITHOUT caching so the list builds correctly once the weapon wakes.
        /// </summary>
        public IReadOnlyList<IWeaponReadout> Readouts
        {
            get
            {
                if (readouts != null) return readouts;
                if (conditions == null) return Array.Empty<IWeaponReadout>();

                readouts = new List<IWeaponReadout>();
                foreach (var condition in conditions)
                    if (condition is IWeaponReadout readout)
                        readouts.Add(readout);
                if (LockSource != null)
                    readouts.Add(LockSource);
                return readouts;
            }
        }

        /// <summary>The lock-state source driving this weapon's guidance UI, or null if it has none.</summary>
        public virtual ILockStateSource LockSource => null;

        /// <summary>
        /// Determines if this weapon should fire based on the given targeting context.
        /// Override in derived classes to provide weapon-specific AI firing logic.
        /// </summary>
        public virtual bool ShouldFire(TargetingContext context)
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

    public abstract class WeaponBase<TProj> : WeaponComponent where TProj : ProjectileBase
    {
        [Header("Launcher Settings")]
        [SerializeField] internal TProj projectilePrefab;

        protected override void Awake()
        {
            base.Awake();
            if (projectilePrefab)
                SimplePool<TProj>.Warm(projectilePrefab);
        }
        
        public override bool CanFire() => projectilePrefab && base.CanFire();
        
        public override ProjectileBase Fire()
        {
            if (!CanFire()) return null;

            foreach (var condition in conditions)
                condition.ProcessFire();

            var proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
            proj.Initialize(shooter);
            proj.Launch(firePoint.up);
            InvokeOnFire();

            return proj;
        }
    }
}
