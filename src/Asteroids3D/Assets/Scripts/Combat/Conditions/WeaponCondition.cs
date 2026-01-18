using Combat.Weapons;
using UnityEngine;

namespace Combat.Conditions
{
    public abstract class WeaponCondition : MonoBehaviour
    {
        protected WeaponComponent _weapon;

        public void Initialize(WeaponComponent weapon)
        {
            _weapon = weapon;
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