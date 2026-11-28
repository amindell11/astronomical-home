using Balance;
using System;
using Ships;
using Ships.Status;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    public sealed class IonVirus : MonoBehaviour, IReleasable
    {
        [Tooltip("How close an ally of a stalled ship must fly to catch the stall.")]
        [Stat, SerializeField, Min(0.01f)] private float spreadRadius = 5f;
        [Tooltip("Most ships one mine stalls before the virus stops passing it on.")]
        [Stat, SerializeField, Min(1)] private int maxShips = 4;

        private IonStallLedger ledger;

        internal float SpreadRadius => spreadRadius;
        internal int MaxShips => maxShips;

        public event Action Released;

        public void Begin(IonStallLedger ledger)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            this.ledger = ledger;
        }

        private void FixedUpdate()
        {
            var caught = ledger.Caught;
            var count = caught.Count;
            var sourceStalled = false;
            for (var i = 0; i < count; i++)
            {
                var source = caught[i];
                // A dead ship is deactivated with its stall frozen, so it no longer spreads.
                if (!source || !source.gameObject.activeInHierarchy || !source.Status.Has(StatusEffect.Stalled)) continue;
                sourceStalled = true;
                if (caught.Count >= maxShips) continue;
                Spread(source);
            }

            if (!sourceStalled && ledger.OpenSources == 0)
                ReturnToPoolImmediate();
        }

        public void ReturnToPoolImmediate()
        {
            Released?.Invoke();
            SimplePool<IonVirus>.Release(this);
        }

        private void Spread(Ship source)
        {
            var mask = 1 << LayerIds.Ship;
            // Regrow until the overlap fits, so a full buffer cannot drop a ship.
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(source.transform.position, spreadRadius, buffer, mask,
                QueryTriggerInteraction.Ignore);
            while (hitCount == buffer.Length)
            {
                buffer = PhysicsBuffers.GetColliderBuffer(buffer.Length * 2);
                hitCount = Physics.OverlapSphereNonAlloc(source.transform.position, spreadRadius, buffer, mask,
                    QueryTriggerInteraction.Ignore);
            }

            for (var i = 0; i < hitCount; i++)
            {
                if (!IonStallLedger.TryEnemy(buffer[i], ledger.Carrier, out var ally)) continue;
                if (ally.teamNumber != source.teamNumber) continue;
                ledger.TryStall(ally);
                if (ledger.Caught.Count >= maxShips) return;
            }
        }
    }
}
