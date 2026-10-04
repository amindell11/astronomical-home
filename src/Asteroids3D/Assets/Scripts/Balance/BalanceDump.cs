using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Combat.Weapons;
using Game.Runs;
using Ships.Loadout;
using Substrate.Services.Projectiles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Balance
{
    /// <summary>One measured cycle mode of one weapon; <see cref="stakes"/> follows the dump's pool order.</summary>
    [Serializable]
    public struct DerivedRow
    {
        public string weapon;
        public string mode;
        public float openingDamage;
        public float openingSeconds;
        public float magazineDamage;
        public float dumpSeconds;
        public float recoverySeconds;
        public float cycleSeconds;
        public float sustainedDps;
        public List<float> stakes;
    }

    /// <summary>
    /// One measurement of the item catalog as JSON: what it was measured on (build identity, stat
    /// fingerprint, and as inputs every stat line of the catalog's items and of the run setting) and
    /// what <see cref="WeaponCycleProbe"/> derived from it, stakes taken once per distinct ship
    /// resource pool. Unity only writes it; scripts/balance/balance_table.py is the one renderer.
    /// </summary>
    [Serializable]
    public struct BalanceDump
    {
        public string schema;
        public string takenUtc;
        public BuildIdentity buildIdentity;
        public string statFingerprint;
        public List<string> inputs;
        public List<float> pools;
        public List<DerivedRow> derived;

        public const string SchemaId = "balance-dump-v1";

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>Measures every catalog weapon in play mode, then hands <paramref name="done"/> the dump.</summary>
        public static IEnumerator Measure(ItemCatalog catalog, IProjectileService projectiles, DateTime takenUtc,
            BuildIdentity buildIdentity, string statFingerprint, IEnumerable<string> settingLines,
            Action<BalanceDump> done)
        {
            var dump = new BalanceDump
            {
                schema = SchemaId,
                takenUtc = takenUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                buildIdentity = buildIdentity,
                statFingerprint = statFingerprint,
                inputs = Inputs(catalog, settingLines),
                pools = Pools(catalog),
                derived = new List<DerivedRow>(),
            };

            foreach (var weapon in catalog.Weapons)
            {
                var modes = new List<WeaponCycleMode>();
                yield return WeaponCycleProbe.Measure(weapon, projectiles, modes);
                foreach (var mode in modes)
                    dump.derived.Add(Row(weapon.name, mode, dump.pools));
            }

            done(dump);
        }

        private static List<string> Inputs(ItemCatalog catalog, IEnumerable<string> settingLines)
        {
            var lines = new SortedSet<string>(settingLines, StringComparer.Ordinal);
            var items = catalog.Chassis.Concat<Object>(catalog.Engines).Concat(catalog.Shields).Concat(catalog.Weapons);
            foreach (var item in items)
                lines.UnionWith(StatHash.Lines(item));
            return lines.ToList();
        }

        private static List<float> Pools(ItemCatalog catalog)
        {
            var pools = new SortedSet<float>();
            foreach (var chassis in catalog.Chassis)
                foreach (var shield in catalog.Shields)
                    pools.Add(ResolvedShipStats.Resolve(chassis, null, shield).ShipResourcePool);
            return pools.ToList();
        }

        private static DerivedRow Row(string weapon, WeaponCycleMode mode, List<float> pools) => new()
        {
            weapon = weapon,
            mode = mode.Label,
            openingDamage = mode.OpeningDamage,
            openingSeconds = mode.OpeningSeconds,
            magazineDamage = mode.MagazineDamage,
            dumpSeconds = mode.DumpSeconds,
            recoverySeconds = mode.RecoverySeconds,
            cycleSeconds = mode.CycleSeconds,
            sustainedDps = mode.SustainedDps,
            stakes = pools.Select(mode.Stakes).ToList(),
        };
    }
}
