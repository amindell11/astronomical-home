using Balance;
using System;
using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Presentation;
using Ships.Status;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    public sealed class IonBlast : MonoBehaviour, IBlastFront
    {
        private static readonly Word StalledWord = new("STALLED", StatusTint.Stalled);

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

        private readonly HashSet<Ship> stalled = new();
        private float radius;
        private Ship carrier;

        public float Radius => radius;
        public float MaxRadius => maxRadius;
        public float StallSeconds => stallSeconds;
        internal float RepeatWindow => repeatWindow;
        internal float RepeatScale => repeatScale;

        public event Action Begun;
        public event Action Released;

        public void Begin(Ship carrier)
        {
            if (!carrier) throw new ArgumentNullException(nameof(carrier));
            this.carrier = carrier;
            radius = 0f;
            stalled.Clear();
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
                if (!TryEnemy(buffer[i], carrier, out var ship) || !stalled.Add(ship)) continue;
                ship.Status.ApplyDiminishing(StatusEffect.Stalled, stallSeconds, repeatWindow, repeatScale);
                carrier.Words.Raise(StalledWord, ship.transform.position);
            }
        }

        internal static bool TryEnemy(Collider collider, Ship carrier, out Ship enemy)
        {
            enemy = DamageableColliders.TryGet(collider, out var owner) && owner is DamageController hull ? hull.Ship : null;
            if (!enemy) return false;
            if (!carrier)
                throw new InvalidOperationException("An ion blast looks for enemies without a carrier.");
            return enemy != carrier && enemy.teamNumber != carrier.teamNumber;
        }
    }
}
