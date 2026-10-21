using UnityEngine;

namespace Damage
{
    public interface IDamageable
    {
        /// <summary>
        /// The GameObject this IDamageable component is attached to
        /// </summary>
        GameObject gameObject { get; }

        /// <returns>Overkill: damage left over once the target is dead; 0 if it survives or takes none.</returns>
        float TakeDamage(in DamageInfo hit);
    }
}
