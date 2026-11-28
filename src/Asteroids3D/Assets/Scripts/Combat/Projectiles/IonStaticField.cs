using Balance;
using System;
using Ships.Status;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    public sealed class IonStaticField : MonoBehaviour, IBlastFront
    {
        [Header("Field")]
        [Stat, SerializeField, Min(0.01f)] private float radius = 8f;
        [Stat, SerializeField, Min(0.01f)] private float seconds = 3f;

        [Header("Stealth")]
        [Tooltip("How long an enemy stays exposed after leaving the field.")]
        [Stat, SerializeField, Min(0.01f)] private float exposeSeconds = 0.25f;

        private IonStallLedger ledger;
        private bool open;
        private float elapsed;

        public float Radius => radius;
        public float MaxRadius => radius;
        internal float Seconds => seconds;

        public event Action Begun;
        public event Action Released;

        public void Begin(IonStallLedger ledger)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            this.ledger = ledger;
            ledger.Open();
            open = true;
            elapsed = 0f;
            Begun?.Invoke();
        }

        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            Sweep();
            if (elapsed >= seconds)
                ReturnToPoolImmediate();
        }

        public void ReturnToPoolImmediate()
        {
            Released?.Invoke();
            if (open)
            {
                ledger.Close();
                open = false;
            }
            SimplePool<IonStaticField>.Release(this);
        }

        private void Sweep()
        {
            var mask = 1 << LayerIds.Ship;
            // Regrow until the overlap fits, so a full buffer cannot drop a ship.
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            while (hitCount == buffer.Length)
            {
                buffer = PhysicsBuffers.GetColliderBuffer(buffer.Length * 2);
                hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            }

            for (var i = 0; i < hitCount; i++)
            {
                if (!IonStallLedger.TryEnemy(buffer[i], ledger.Carrier, out var ship)) continue;
                ship.Status.Apply(StatusEffect.Exposed, exposeSeconds);
                ledger.TryStall(ship);
            }
        }
    }
}
