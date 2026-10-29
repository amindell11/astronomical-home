using System;
using Combat.Weapons;
using Combat.Weapons.Mods;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public abstract class WeaponCondition : MonoBehaviour
    {
        protected WeaponComponent weapon;
        protected WeaponMods mods = WeaponMods.None;

        // Float clocks land just short of step-multiple thresholds; half a step short counts as reached.
        protected static float HalfStep => Time.fixedDeltaTime * 0.5f;

        public void Initialize(WeaponComponent weapon)
        {
            this.weapon = weapon;
        }

        /// <summary>Puts a set of mods in force: the authored numbers stay, the effective reads change.</summary>
        public void SetMods(WeaponMods mods)
        {
            this.mods = mods ?? throw new ArgumentNullException(nameof(mods));
            OnModsChanged();
        }

        protected virtual void OnModsChanged() { }

        /// <summary>
        /// Checks if this condition allows firing.
        /// </summary>
        public abstract bool CanFire();

        /// <summary>
        /// Called by the weapon when it fires.
        /// </summary>
        public virtual void ProcessFire() { }
        
        /// <summary>
        /// Called by the weapon on reset.
        /// </summary>
        public virtual void Reset() { }
    }
}