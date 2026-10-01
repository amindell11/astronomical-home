using System;
using System.Collections.Generic;
using Balance;
using Damage;
using Ships;
using Ships.Registry;
using Substrate.Services.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Player
{
    /// <summary>
    /// What flew against the player this run: one entry per ship the unit service spawned or
    /// adopted, with its parts by asset name, its loadout stat hash taken at spawn, and its fate.
    /// Consumer-side recorder beside the damage ledger and the run tally, never sim state. It logs
    /// every spawn, the player's included, because the player's id is not known until the spawn
    /// returns; <see cref="Entries"/> leaves the current player out. A ship logged before
    /// <see cref="Begin"/> (placed during the sector load) reads as spawned at second 0.
    /// </summary>
    public sealed class SpawnLog
    {
        public readonly struct Entry
        {
            public readonly ShipId Id;
            public readonly string Chassis;
            public readonly string Pilot;
            public readonly string Engine;
            public readonly string Shield;
            public readonly string Primary;
            public readonly string Secondary;
            public readonly string LoadoutStatHash;
            public readonly float SpawnSeconds;
            public readonly float AliveSeconds;
            public readonly bool KilledByPlayer;

            internal Entry(Spawn spawn, float runStart, float runEnd)
            {
                Id = spawn.Id;
                Chassis = spawn.Chassis;
                Pilot = spawn.Pilot;
                Engine = spawn.Engine;
                Shield = spawn.Shield;
                Primary = spawn.Primary;
                Secondary = spawn.Secondary;
                LoadoutStatHash = spawn.LoadoutStatHash;

                var spawnedAt = Mathf.Max(spawn.SpawnedAt, runStart);
                SpawnSeconds = spawnedAt - runStart;
                AliveSeconds = (spawn.Dead ? spawn.DiedAt : runEnd) - spawnedAt;
                KilledByPlayer = spawn.KilledByPlayer;
            }
        }

        internal sealed class Spawn
        {
            public ShipId Id;
            public string Chassis;
            public string Pilot;
            public string Engine;
            public string Shield;
            public string Primary;
            public string Secondary;
            public string LoadoutStatHash;
            public float SpawnedAt;
            public float DiedAt;
            public bool Dead;
            public bool KilledByPlayer;
        }

        private const string CloneSuffix = "(Clone)";

        private readonly List<Spawn> spawns = new();
        private IUnitService units;
        private Func<ShipId> playerId;
        private float startTime;
        private float endTime;
        private bool running;

        /// <summary>Re-bindable; a null <paramref name="unitService"/> unbinds and stops logging.</summary>
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
            spawns.Clear();
            running = false;
            startTime = endTime = 0f;
        }

        public void Begin(float now)
        {
            startTime = now;
            running = true;
        }

        public void End(float now)
        {
            if (!running) return;
            endTime = now;
            running = false;
        }

        /// <summary>Every logged ship but the current player, in spawn order.</summary>
        public List<Entry> Entries()
        {
            var player = playerId();
            var runEnd = running ? Time.time : endTime;
            var entries = new List<Entry>(spawns.Count);
            foreach (var spawn in spawns)
                if (spawn.Id != player)
                    entries.Add(new Entry(spawn, startTime, runEnd));
            return entries;
        }

        // The id rides beside the ship because an EditMode instance never ran Awake and has none.
        internal void Log(ShipId id, Ship ship, float now)
        {
            var weapons = ship.Weapons;
            spawns.Add(new Spawn
            {
                Id = id,
                Chassis = NameOf(ship),
                Pilot = NameOf(ship.Commander),
                Engine = NameOf(ship.Engine),
                Shield = NameOf(ship.Shield),
                Primary = NameOf(weapons ? weapons.PrimaryMountPrefab : null),
                Secondary = NameOf(weapons ? weapons.SecondaryMountPrefab : null),
                LoadoutStatHash = StatHash.OfLoadout(ship),
                SpawnedAt = now,
            });
        }

        /// <summary>The run tally's rule: a death counts only between Begin and End.</summary>
        internal void MarkDead(ShipId victim, DamageInfo killingBlow, float now)
        {
            if (!running) return;

            var spawn = spawns.FindLast(s => s.Id == victim);
            if (spawn == null) return;

            var player = playerId();
            spawn.Dead = true;
            spawn.DiedAt = now;
            spawn.KilledByPlayer = killingBlow.AttackerId == player && victim != player;
        }

        private void OnShipSpawned(Ship ship)
        {
            Log(ship.Id, ship, Time.time);
            ship.Damage.OnDeath += OnDeath;
        }

        private void OnDeath(ShipId victim, DamageInfo killingBlow) => MarkDead(victim, killingBlow, Time.time);

        private static string NameOf(Object part) =>
            part ? part.name.Replace(CloneSuffix, string.Empty) : string.Empty;
    }
}
