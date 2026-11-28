using System;
using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Presentation;
using Ships.Status;
using UnityEngine;

namespace Combat.Projectiles
{
    /// <summary>
    /// The once-per-mine rule: each ship is stalled once, so the blast, the field and the virus
    /// compose with no special case. It also counts the open blast and field, so the virus
    /// knows when nothing more can catch a ship.
    /// </summary>
    public sealed class IonStallLedger
    {
        private static readonly Word StalledWord = new("STALLED", StatusTint.Stalled);

        private readonly float stallSeconds;
        private readonly float repeatWindow;
        private readonly float repeatScale;
        private readonly List<Ship> caught = new();

        public IonStallLedger(Ship carrier, float stallSeconds, float repeatWindow, float repeatScale)
        {
            if (!carrier) throw new ArgumentNullException(nameof(carrier));
            Carrier = carrier;
            this.stallSeconds = stallSeconds;
            this.repeatWindow = repeatWindow;
            this.repeatScale = repeatScale;
        }

        public Ship Carrier { get; }

        public IReadOnlyList<Ship> Caught => caught;

        public int OpenSources { get; private set; }

        public bool TryStall(Ship ship)
        {
            if (caught.Contains(ship)) return false;
            ship.Status.ApplyDiminishing(StatusEffect.Stalled, stallSeconds, repeatWindow, repeatScale);
            Carrier.Words.Raise(StalledWord, ship.transform.position);
            caught.Add(ship);
            return true;
        }

        public void Open() => OpenSources++;

        public void Close()
        {
            if (OpenSources == 0)
                throw new InvalidOperationException("An ion stall ledger closed with nothing open.");
            OpenSources--;
        }

        public static bool TryEnemy(Collider collider, Ship carrier, out Ship enemy)
        {
            var hull = DamageableColliders.TryGet(collider, out var owner) ? owner as DamageController : null;
            enemy = hull ? hull.Ship : null;
            if (!enemy) return false;
            if (!carrier)
                throw new InvalidOperationException("An ion mine looks for enemies without a carrier.");
            return enemy != carrier && enemy.teamNumber != carrier.teamNumber;
        }
    }
}
