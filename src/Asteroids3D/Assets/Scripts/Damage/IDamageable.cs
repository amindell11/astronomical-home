using UnityEngine;

namespace Damage
{
    public interface IDamageable
    {
        /// <summary>
        /// The GameObject this IDamageable component is attached to
        /// </summary>
        GameObject gameObject { get; }

        /// <summary>
        /// Apply damage to this object
        /// </summary>
        void TakeDamage(in DamageInfo hit);
    }
}
