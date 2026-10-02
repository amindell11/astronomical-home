using System;
using System.Collections.Generic;
using System.Globalization;
using Damage;
using Game.Player;
using Ships.Loadout;
using Ships.Registry;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runs
{
    /// <summary>A ship's parts by asset name; an empty string is an empty slot.</summary>
    [Serializable]
    public struct LoadoutRow
    {
        public string chassis;
        public string engine;
        public string shield;
        public string primary;
        public string secondary;
        public string statHash;
    }

    [Serializable]
    public struct DamageRow
    {
        public string kind;
        public float total;
        public int hits;
        public string source;
        public int spawn;
    }

    [Serializable]
    public struct KillingBlowRow
    {
        public string kind;
        public int spawn;
    }

    [Serializable]
    public struct SpawnRow
    {
        public float spawnSeconds;
        public string pilot;
        public LoadoutRow loadout;
        public float aliveSeconds;
        public bool killedByPlayer;
    }

    /// <summary>
    /// One finished run as one JSON line: what it was played on (build identity, stat fingerprint),
    /// what the player flew, how it went, what hurt the player, and every ship that spawned.
    /// A damage row and the killing blow name their attacker as an index into <see cref="spawns"/>,
    /// <see cref="NoSpawn"/> when no logged ship dealt it, so no Unity instance id reaches the file.
    /// What a record holds and why: https://github.com/amindell11/astronomical-home/issues/772#issuecomment-5905712068
    /// </summary>
    [Serializable]
    public struct RunRecord
    {
        public string schema;
        public string endedUtc;
        public string sector;
        public BuildIdentity buildIdentity;
        public string statFingerprint;
        public LoadoutRow player;
        public int kills;
        public float secondsSurvived;
        public List<DamageRow> damage;
        public KillingBlowRow killingBlow;
        public List<SpawnRow> spawns;

        public const string SchemaId = "run-record-v1";
        public const int NoSpawn = -1;

        public string ToJsonLine() => JsonUtility.ToJson(this);

        public static RunRecord Compose(DateTime endedUtc, string sector, BuildIdentity buildIdentity,
            string statFingerprint, ShipLoadout playerLoadout, string playerLoadoutStatHash, IRunTally tally,
            DamageLedger ledger, SpawnLog spawnLog, DamageInfo killingBlow)
        {
            var entries = spawnLog.Entries();
            int SpawnOf(ShipId attacker) => entries.FindLastIndex(entry => entry.Id == attacker);

            var record = new RunRecord
            {
                schema = SchemaId,
                endedUtc = endedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                sector = sector,
                buildIdentity = buildIdentity,
                statFingerprint = statFingerprint,
                player = new LoadoutRow
                {
                    chassis = NameOf(playerLoadout.Ship),
                    engine = NameOf(playerLoadout.Engine),
                    shield = NameOf(playerLoadout.Shield),
                    primary = NameOf(playerLoadout.PrimaryWeapon),
                    secondary = NameOf(playerLoadout.SecondaryWeapon),
                    statHash = playerLoadoutStatHash,
                },
                kills = tally.Kills,
                secondsSurvived = tally.SecondsSurvived,
                damage = new List<DamageRow>(ledger.Rows.Count),
                killingBlow = new KillingBlowRow
                {
                    kind = killingBlow.Kind.ToString(),
                    spawn = SpawnOf(killingBlow.AttackerId),
                },
                spawns = new List<SpawnRow>(entries.Count),
            };

            foreach (var row in ledger.Rows)
                record.damage.Add(new DamageRow
                {
                    kind = row.Kind.ToString(),
                    total = row.Total,
                    hits = row.Hits,
                    source = row.SourceName,
                    spawn = SpawnOf(row.AttackerId),
                });

            foreach (var entry in entries)
                record.spawns.Add(new SpawnRow
                {
                    spawnSeconds = entry.SpawnSeconds,
                    pilot = entry.Pilot,
                    loadout = new LoadoutRow
                    {
                        chassis = entry.Chassis,
                        engine = entry.Engine,
                        shield = entry.Shield,
                        primary = entry.Primary,
                        secondary = entry.Secondary,
                        statHash = entry.LoadoutStatHash,
                    },
                    aliveSeconds = entry.AliveSeconds,
                    killedByPlayer = entry.KilledByPlayer,
                });

            return record;
        }

        private static string NameOf(Object part) => part ? part.name : string.Empty;
    }
}
