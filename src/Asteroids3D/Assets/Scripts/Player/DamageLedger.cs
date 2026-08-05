using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;

namespace Player
{
    /// <summary>
    /// Per-life accumulation of the player's received hits, aggregated per damage source —
    /// consumer-side recorder, never sim state. Source names are captured at event time
    /// because the attacker may despawn before the death recap reads the row.
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

        private readonly List<Row> rows = new();
        private IDamageEvents source;
        private IShipRegistry registry;

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>Re-bindable across player rebuilds.</summary>
        public void Bind(IDamageEvents damage, IShipRegistry shipRegistry)
        {
            if (source != null) source.OnDamaged -= Record;
            source = damage;
            registry = shipRegistry;
            if (source != null) source.OnDamaged += Record;
        }

        public void Clear() => rows.Clear();

        public static string DescribeKind(DamageKind kind) => kind switch
        {
            DamageKind.Laser => "laser fire",
            DamageKind.Railgun => "railgun",
            DamageKind.Missile => "missile",
            DamageKind.ConcussionWave => "concussion wave",
            DamageKind.Collision => "asteroid collision",
            _ => kind.ToString(),
        };

        private void Record(DamageInfo hit)
        {
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
