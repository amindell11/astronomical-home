using Balance;
using Damage;
using UnityEngine;

namespace Combat.Projectiles
{
    // A Laser to its pool too: weapons fire it as WeaponBase<Laser>, matching Laser's sealed pool return.
    public class Pellet : Laser
    {
        [Tooltip("Impulse (N·s) this pellet gives its target at the contact point, along its flight.")]
        [Stat, SerializeField, Min(0f)] private float knockback = 500f;

        public float Knockback => knockback;

        protected override void OnHit(IDamageable other, Collider collider)
        {
            var body = collider.attachedRigidbody;
            if (body && !body.isKinematic)
                body.AddForceAtPosition(rb.linearVelocity.normalized * knockback, transform.position, ForceMode.Impulse);
            base.OnHit(other, collider);
        }
    }
}
