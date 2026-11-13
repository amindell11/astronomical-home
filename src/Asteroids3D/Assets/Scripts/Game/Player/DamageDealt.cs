using System;
using System.Collections.Generic;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Registry;
using Substrate.Services.Units;

namespace Game.Player
{
    /// <summary>
    /// Damage the player dealt this run, one row per sector and damage kind. Consumer-side
    /// recorder beside the damage ledger: it watches <see cref="IDamageEvents.OnDamaged"/> on
    /// ships the unit service spawns and counts a hit only when the attacker is the current
    /// player, the victim is not, and the host's run is between <see cref="Begin"/> and <see cref="End"/>.
    /// </summary>
    public sealed class DamageDealt
    {
        public readonly struct Row
        {
            public readonly int Sector;
            public readonly DamageKind Kind;
            public readonly float Shield;
            public readonly float Hull;
            public readonly int Hits;
            public float Total => Shield + Hull;

            public Row(int sector, DamageKind kind, float shield, float hull, int hits)
            {
                Sector = sector;
                Kind = kind;
                Shield = shield;
                Hull = hull;
                Hits = hits;
            }
        }

        private readonly List<Row> rows = new();
        private IUnitService units;
        private Func<ShipId> playerId;
        private bool running;
        private int sector;

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>Re-bindable; a null <paramref name="unitService"/> unbinds and stops counting.</summary>
        public void Bind(IUnitService unitService, Func<ShipId> currentPlayerId)
        {
            if (units != null) units.OnShipSpawned -= OnShipSpawned;
            units = unitService;
            playerId = currentPlayerId;
            if (units != null) units.OnShipSpawned += OnShipSpawned;
            else running = false;
        }

        public void Reset()
        {
            rows.Clear();
            running = false;
            sector = 0;
        }

        public void Begin() => running = true;

        public void BeginSector(int sectorNumber) => sector = sectorNumber;

        public void End() => running = false;

        internal void Watch(IDamageEvents damage, ShipId victim) => damage.OnDamaged += hit => Record(victim, hit);

        private void OnShipSpawned(Ship ship)
        {
            var victim = ship.Id;
            ship.Damage.OnDamaged += hit => Record(victim, hit);
        }

        private void Record(ShipId victim, AppliedDamage damage)
        {
            if (!running) return;
            var info = damage.Info;
            var player = playerId();
            if (info.AttackerId != player || victim == player) return;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Sector != sector || row.Kind != info.Kind) continue;
                rows[i] = new Row(row.Sector, row.Kind, row.Shield + damage.Shield, row.Hull + damage.Hull,
                    row.Hits + 1);
                return;
            }

            rows.Add(new Row(sector, info.Kind, damage.Shield, damage.Hull, 1));
        }
    }
}
