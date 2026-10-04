using Combat.Weapons;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public abstract class WeaponCondition : MonoBehaviour
    {
        protected WeaponComponent weapon;

        // Float clocks land just short of step-multiple thresholds; within half a step counts as reached.
        protected static float HalfStep => Time.fixedDeltaTime * 0.5f;

        public void Initialize(WeaponComponent weapon)
        {
            this.weapon = weapon;
        }

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