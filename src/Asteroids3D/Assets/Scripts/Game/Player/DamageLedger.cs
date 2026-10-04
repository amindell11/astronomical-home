using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Registry;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Per-life record of the player's received hits — each hit stamped with game time, and their
    /// aggregate per damage source — consumer-side recorder, never sim state. Source names are
    /// captured at event time because the attacker may despawn before the death recap reads the row.
    /// </summary>
    public sealed class DamageLedger
    {
        public readonly struct Row
        {
            public readonly ShipId AttackerId;
            public readonly DamageKind Kind;
            public readonly string SourceName;
            public readonly float Total;
            public readonly int Hits;

            public Row(ShipId attackerId, DamageKind kind, string sourceName, float total, int hits)
            {
                AttackerId = attackerId;
                Kind = kind;
                SourceName = sourceName;
                Total = total;
                Hits = hits;
            }
        }

        public readonly struct Hit
        {
            public readonly float Time;
            public readonly float Amount;
            public readonly DamageKind Kind;
            public readonly ShipId AttackerId;

            public Hit(float time, float amount, DamageKind kind, ShipId attackerId)
            {
                Time = time;
                Amount = amount;
                Kind = kind;
                AttackerId = attackerId;
            }
        }

        private readonly List<Row> rows = new();
        private readonly List<Hit> hits = new();
        private IDamageEvents source;
        private IShipRegistry registry;

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>In arrival order; the last is the life's killing blow once the player has died.</summary>
        public IReadOnlyList<Hit> Hits => hits;

        /// <summary>Re-bindable across player rebuilds.</summary>
        public void Bind(IDamageEvents damage, IShipRegistry shipRegistry)
        {
            if (source != null) source.OnDamaged -= OnDamaged;
            source = damage;
            registry = shipRegistry;
            if (source != null) source.OnDamaged += OnDamaged;
        }

        public void Clear()
        {
            rows.Clear();
            hits.Clear();
        }

        public static string DescribeKind(DamageKind kind) => kind switch
        {
            DamageKind.Laser => "laser fire",
            DamageKind.Railgun => "railgun",
            DamageKind.Missile => "missile",
            DamageKind.ConcussionWave => "concussion wave",
            DamageKind.Collision => "asteroid collision",
            _ => kind.ToString(),
        };

        private void OnDamaged(DamageInfo hit) => Record(hit, Time.time);

        internal void Record(in DamageInfo hit, float now)
        {
            hits.Add(new Hit(now, hit.Amount, hit.Kind, hit.AttackerId));

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.AttackerId != hit.AttackerId || row.Kind != hit.Kind) continue;
                rows[i] = new Row(row.AttackerId, row.Kind, row.SourceName,
                    row.Total + hit.Amount, row.Hits + 1);
                return;
            }

            rows.Add(new Row(hit.AttackerId, hit.Kind, ResolveName(hit), hit.Amount, 1));
        }

        private string ResolveName(in DamageInfo hit)
        {
            if (registry != null && registry.TryGetShip(hit.AttackerId, out var ship))
                return ship.name.Replace("(Clone)", "");
            return hit.Kind == DamageKind.Collision ? "asteroid" : "unknown";
        }
    }
}
