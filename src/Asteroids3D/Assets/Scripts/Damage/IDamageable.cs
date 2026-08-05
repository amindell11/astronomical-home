using UnityEngine;

namespace Damage
{
    public interface IDamageable
    {
        /// <summary>
        /// The GameObject this IDamageable component is attached to
        /// </summary>
        GameObject gameObject { get; }

        void TakeDamage(in DamageInfo hit);
    }
}
