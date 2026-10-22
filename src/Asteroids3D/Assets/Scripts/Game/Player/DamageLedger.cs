using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Registry;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Records received shield and hull damage across the run, including sector changes and repairs.
    /// Source names are captured before attackers despawn. The recorder never changes combat state.
    /// </summary>
    public sealed class DamageLedger
    {
        public readonly struct Row
        {
            public readonly ShipId AttackerId;
            public readonly DamageKind Kind;
            public readonly string SourceName;
            public readonly float Shield;
            public readonly float Hull;
            public float Total => Shield + Hull;
            public readonly int Hits;

            public Row(ShipId attackerId, DamageKind kind, string sourceName, float shield, float hull, int hits)
            {
                AttackerId = attackerId;
                Kind = kind;
                SourceName = sourceName;
                Shield = shield;
                Hull = hull;
                Hits = hits;
            }
        }

        public readonly struct Hit
        {
            public readonly float Time;
            public readonly float Shield;
            public readonly float Hull;
            public float Amount => Shield + Hull;
            public readonly DamageKind Kind;
            public readonly ShipId AttackerId;

            public Hit(float time, in AppliedDamage damage)
            {
                Time = time;
                Shield = damage.Shield;
                Hull = damage.Hull;
                Kind = damage.Info.Kind;
                AttackerId = damage.Info.AttackerId;
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

        public static string DescribeKind(DamageKind kind, ShipId attacker) => kind switch
        {
            DamageKind.Collision when attacker.IsValid => "ram",
            DamageKind.Laser => "laser fire",
            DamageKind.Railgun => "railgun",
            DamageKind.Missile => "missile",
            DamageKind.ConcussionWave => "concussion wave",
            DamageKind.Collision => "asteroid collision",
            _ => kind.ToString(),
        };

        private void OnDamaged(AppliedDamage damage) => Record(damage, Time.time);

        internal void Record(in AppliedDamage damage, float now)
        {
            var hit = damage.Info;
            hits.Add(new Hit(now, damage));

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.AttackerId != hit.AttackerId || row.Kind != hit.Kind) continue;
                rows[i] = new Row(row.AttackerId, row.Kind, row.SourceName,
                    row.Shield + damage.Shield, row.Hull + damage.Hull, row.Hits + 1);
                return;
            }

            rows.Add(new Row(hit.AttackerId, hit.Kind, ResolveName(hit), damage.Shield, damage.Hull, 1));
        }

        private string ResolveName(in DamageInfo hit)
        {
            if (registry != null && registry.TryGetShip(hit.AttackerId, out var ship))
                return ship.name.Replace("(Clone)", "");
            return hit.Kind == DamageKind.Collision && !hit.AttackerId.IsValid ? "asteroid" : "unknown";
        }
    }
}
