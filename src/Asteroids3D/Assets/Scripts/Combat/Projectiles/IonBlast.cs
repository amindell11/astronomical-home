using Balance;
using System;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    public sealed class IonBlast : MonoBehaviour, IBlastFront
    {
        [Header("Blast")]
        [Stat, SerializeField, Min(0.01f)] private float maxRadius = 12f;
        [Stat, SerializeField, Min(0.01f)] private float expandSpeed = 20f;

        [Header("Stall")]
        [Tooltip("Seconds the first stall on a ship lasts.")]
        [Stat, SerializeField, Min(0.01f)] private float stallSeconds = 2.5f;
        [Tooltip("Seconds after a stall during which another stall on that ship is shorter.")]
        [Stat, SerializeField, Min(0f)] private float repeatWindow = 6f;
        [Tooltip("Fraction of the stall a repeat within the window lasts.")]
        [Stat, SerializeField, Range(0f, 1f)] private float repeatScale = 0.5f;

        private IonStallLedger ledger;
        private bool open;
        private float radius;

        public float Radius => radius;
        public float MaxRadius => maxRadius;
        public float StallSeconds => stallSeconds;
        internal float ExpandSpeed => expandSpeed;
        internal float RepeatWindow => repeatWindow;
        internal float RepeatScale => repeatScale;

        public event Action Begun;
        public event Action Released;

        public void Begin(IonStallLedger ledger)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            this.ledger = ledger;
            ledger.Open();
            open = true;
            radius = 0f;
            Begun?.Invoke();
        }

        private void FixedUpdate()
        {
            radius += expandSpeed * Time.fixedDeltaTime;
            Sweep();
            if (radius >= MaxRadius)
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
            SimplePool<IonBlast>.Release(this);
        }

        private void Sweep()
        {
            var mask = 1 << LayerIds.Ship;
            // Regrow until the overlap fits, so inner hits cannot crowd out the frontier.
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
                ledger.TryStall(ship);
            }
        }
    }
}
