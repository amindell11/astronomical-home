using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Damage;
using Game.Player;
using Ships.Loadout;
using Ships.Registry;
using Substrate.Services.Locales;
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
        public float shield;
        public float hull;
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
    public struct PickRow
    {
        public string kind;
        public int sector;
        public float seconds;
        public List<string> offered;
        public string taken;
        public string replaced;
    }

    [Serializable]
    public struct SectorRow
    {
        public int sector;
        public float seconds;
        public int kills;
        public float hullIn;
        public float shieldIn;
        public float hullOut;
        public float shieldOut;
        public bool eliteMet;
        public bool eliteBeaten;
    }

    [Serializable]
    public struct DealtRow
    {
        public int sector;
        public string kind;
        public float total;
        public float shield;
        public float hull;
        public int hits;
    }

    [Serializable]
    public struct EndPhaseRow
    {
        public int index;
        public bool inLull;
        public bool done;
        public float elapsedSeconds;
    }

    [Serializable]
    public struct SpawnRow
    {
        public float spawnSeconds;
        public string pilot;
        public LoadoutRow loadout;
        public float aliveSeconds;
        public bool killedByPlayer;
        public bool elite;
    }

    /// <summary>
    /// One finished run as one JSON line. <see cref="player"/> is the hangar loadout at run start;
    /// <see cref="playerEnd"/> is the loadout mounted when the run ended. The line also holds what
    /// the run was played on, how it went, the picks and the sectors, what hurt the player and what
    /// the player dealt, and every ship that spawned. A run ends extracted, dead, or abandoned; only
    /// a death carries a killing blow.
    /// A damage row and the killing blow name their attacker as an index into <see cref="spawns"/>,
    /// <see cref="NoSpawn"/> when no logged ship dealt it, so no Unity instance id reaches the file.
    /// Older v2 rows lack a measured shield/hull split; hasDamageSplit distinguishes those from zero.
    /// What a record holds and why: https://github.com/amindell11/astronomical-home/issues/772#issuecomment-5905712068
    /// </summary>
    [Serializable]
    public struct RunRecord
    {
        public string schema;
        public string endedUtc;
        public string outcome;
        public int sectorsCleared;
        /// <summary>The sector the run ended in, named by its run row: "Sector 2" is the run's second row.</summary>
        public string sector;
        /// <summary>The locale each of the run's sectors drew, in order; empty when the run was not presented.</summary>
        public List<string> locales;
        public BuildIdentity buildIdentity;
        public string statFingerprint;
        public LoadoutRow player;
        public LoadoutRow playerEnd;
        public List<string> mods;
        public int kills;
        public float secondsSurvived;
        public List<DamageRow> damage;
        public bool hasDamageSplit;
        public KillingBlowRow killingBlow;
        public List<SpawnRow> spawns;
        public List<PickRow> picks;
        public List<SectorRow> sectors;
        public List<DealtRow> dealt;
        public EndPhaseRow endPhase;
        public string origin;

        public const string SchemaId = "run-record-v3";
        public const string SchemaV2 = "run-record-v2";
        public const string Died = "died";
        public const string Extracted = "extracted";
        public const string Abandoned = "abandoned";
        public const string PickMod = "mod";
        public const string PickWeapon = "weapon";
        public const string OriginEditor = "editor";
        public const string OriginBuild = "build";
        public const int NoSpawn = -1;
        public const string EndedUtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>A v2 or v3 line that finished died or extracted. Abandoned stays in the file and off every reader.</summary>
        public static bool Counts(string schema, string outcome) =>
            (schema == SchemaId || schema == SchemaV2) && outcome != Abandoned;

        public string ToJsonLine() => JsonUtility.ToJson(this);

        /// <summary>A null <paramref name="killingBlow"/> extracted, unless <paramref name="abandoned"/> is set.</summary>
        public static RunRecord Compose(DateTime endedUtc, int sectorsCleared, int endedInSector,
            IReadOnlyList<LocaleSky> locales, BuildIdentity buildIdentity, string statFingerprint,
            ShipLoadout playerLoadout, string playerLoadoutStatHash, IRunTally tally, DamageLedger ledger,
            SpawnLog spawnLog, DamageInfo? killingBlow, RunCourse course = null, LoadoutRow playerEnd = default,
            IReadOnlyList<string> mods = null, EndPhaseRow endPhase = default, string origin = null,
            IReadOnlyList<DealtRow> dealt = null, bool abandoned = false)
        {
            var entries = spawnLog.Entries();
            int SpawnOf(ShipId attacker) => entries.FindLastIndex(entry => entry.Id == attacker);

            var record = new RunRecord
            {
                schema = SchemaId,
                endedUtc = endedUtc.ToString(EndedUtcFormat, CultureInfo.InvariantCulture),
                outcome = abandoned ? Abandoned : killingBlow.HasValue ? Died : Extracted,
                sectorsCleared = sectorsCleared,
                sector = $"Sector {endedInSector + 1}",
                locales = locales == null ? new List<string>() : locales.Select(NameOf).ToList(),
                buildIdentity = buildIdentity,
                statFingerprint = statFingerprint,
                player = new LoadoutRow
                {
                    chassis = NameOf(playerLoadout.Ship),
                    engine = NameOf(playerLoadout.Ship.Engine),
                    shield = NameOf(playerLoadout.Ship.Shield),
                    primary = NameOf(playerLoadout.PrimaryWeapon),
                    secondary = NameOf(playerLoadout.SecondaryWeapon),
                    statHash = playerLoadoutStatHash,
                },
                kills = tally.Kills,
                secondsSurvived = tally.SecondsSurvived,
                damage = new List<DamageRow>(ledger.Rows.Count),
                hasDamageSplit = true,
                killingBlow = killingBlow is { } blow
                    ? new KillingBlowRow { kind = blow.Kind.ToString(), spawn = SpawnOf(blow.AttackerId) }
                    : new KillingBlowRow { kind = string.Empty, spawn = NoSpawn },
                spawns = new List<SpawnRow>(entries.Count),
                playerEnd = playerEnd,
                mods = mods == null ? new List<string>() : new List<string>(mods),
                picks = course == null ? new List<PickRow>() : new List<PickRow>(course.Picks),
                sectors = course == null ? new List<SectorRow>() : new List<SectorRow>(course.Sectors),
                dealt = dealt == null ? new List<DealtRow>() : new List<DealtRow>(dealt),
                endPhase = endPhase,
                origin = origin ?? string.Empty,
            };

            // Re-aggregated from the hits, because a ledger row spans every life of its attacker.
            var rowOf = new Dictionary<(ShipId, int, DamageKind), int>();
            foreach (var hit in ledger.Hits)
            {
                var spawn = spawnLog.LifeAt(entries, hit.AttackerId, hit.Time);
                if (!rowOf.TryGetValue((hit.AttackerId, spawn, hit.Kind), out var i))
                {
                    i = record.damage.Count;
                    rowOf.Add((hit.AttackerId, spawn, hit.Kind), i);
                    record.damage.Add(new DamageRow
                    {
                        kind = hit.Kind.ToString(),
                        source = SourceOf(ledger, hit),
                        spawn = spawn,
                    });
                }
                var row = record.damage[i];
                row.total += hit.Amount;
                row.shield += hit.Shield;
                row.hull += hit.Hull;
                row.hits++;
                record.damage[i] = row;
            }

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
                    elite = entry.Elite,
                });

            return record;
        }

        private static string SourceOf(DamageLedger ledger, DamageLedger.Hit hit)
        {
            foreach (var row in ledger.Rows)
                if (row.AttackerId == hit.AttackerId && row.Kind == hit.Kind)
                    return row.SourceName;
            throw new InvalidOperationException("Every ledger hit aggregates into a ledger row.");
        }

        private static string NameOf(Object part) => part ? part.name : string.Empty;
    }
}
