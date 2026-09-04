using Combat.Weapons;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public abstract class WeaponCondition : MonoBehaviour
    {
        protected WeaponComponent weapon;

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