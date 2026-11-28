using System.Collections.Generic;
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

        private PelletBlast blast;

        public float Knockback => knockback;

        /// <summary>Counts this pellet with the rest of its pull's, which shove harder together.</summary>
        public void JoinBlast(PelletBlast pull) => blast = pull;

        protected override void OnHit(IDamageable other, Collider collider)
        {
            var body = collider.attachedRigidbody;
            if (body && !body.isKinematic)
            {
                var flight = rb.linearVelocity.normalized;
                body.AddForceAtPosition(flight * knockback, transform.position, ForceMode.Impulse);
                blast?.Land(body, flight, transform.position);
            }
            base.OnHit(other, collider);
        }

        protected override void OnReturnToPool()
        {
            base.OnReturnToPool();
            blast = null;
        }
    }

    /// <summary>One scattergun pull's pellets: each that lands on a body shoves it harder than the last.</summary>
    public sealed class PelletBlast
    {
        private readonly float shove;
        private readonly Dictionary<Rigidbody, int> landed = new();

        public PelletBlast(float shove) => this.shove = shove;

        public void Land(Rigidbody body, Vector3 direction, Vector3 point)
        {
            landed.TryGetValue(body, out var count);
            landed[body] = ++count;
            body.AddForceAtPosition(direction * (shove * count), point, ForceMode.Impulse);
        }
    }
}
