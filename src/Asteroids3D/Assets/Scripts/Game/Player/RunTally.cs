using System;
using Damage;
using Ships;
using Ships.Damage;
using Ships.Registry;
using Substrate.Services.Units;
using UnityEngine;

namespace Game.Player
{
    public interface IRunTally
    {
        int Kills { get; }
        float SecondsSurvived { get; }
    }

    /// <summary>
    /// Kills and time survived for one run — consumer-side recorder beside the damage ledger,
    /// never sim state. Hooks the death event of every ship the unit service spawns and counts
    /// deaths whose killing blow came from the current player, only between the host's
    /// <see cref="Begin"/> and <see cref="End"/>. The player id is read at event time because
    /// the hangar can rebuild the player.
    /// </summary>
    public sealed class RunTally : IRunTally
    {
        private IUnitService units;
        private Func<ShipId> playerId;
        private float startTime;
        private float endTime;
        private bool running;
        private bool ended;

        public int Kills { get; private set; }

        public float SecondsSurvived =>
            ended ? endTime - startTime : running ? Time.time - startTime : 0f;

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
            Kills = 0;
            running = false;
            ended = false;
            startTime = endTime = 0f;
        }

        public void Begin(float now)
        {
            startTime = now;
            running = true;
            ended = false;
        }

        public void End(float now)
        {
            if (!running) return;
            endTime = now;
            running = false;
            ended = true;
        }

        public static string FormatSeconds(float seconds)
        {
            var whole = Mathf.FloorToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }

        internal void Watch(IDamageEvents damage) => damage.OnDeath += Record;

        private void OnShipSpawned(Ship ship) => Watch(ship.Damage);

        private void Record(ShipId victim, DamageInfo killingBlow)
        {
            if (!running) return;
            var player = playerId();
            if (killingBlow.AttackerId == player && victim != player)
                Kills++;
        }
    }
}
