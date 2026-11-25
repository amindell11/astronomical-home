using Damage;
using UnityEngine;

namespace Tests.Common
{
    public static class DamageableColliderRegistration
    {
        public static void Register(IDamageable owner)
        {
            var behaviour = (MonoBehaviour)owner;
            DamageableColliders.Register(owner, behaviour.GetComponentsInChildren<Collider>(true));
        }
    }
}
