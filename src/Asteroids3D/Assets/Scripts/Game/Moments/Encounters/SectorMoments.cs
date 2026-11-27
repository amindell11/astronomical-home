using System;
using Ships;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;

namespace Game.Moments.Encounters
{
    /// <summary>
    /// Bridges one sector's hunt, elite, first wave and fallen rivals onto the run's moment stream.
    /// A first wave whose rival the host's introduction test accepts introduces it instead of starting combat.
    /// The host disposes it when the sector closes, so a dead sector's events
    /// never reach the next one's stream.
    /// </summary>
    public sealed class SectorMoments : IDisposable
    {
        private readonly RunMomentStream moments;
        private readonly string faction;
        private readonly SectorHunt hunt;
        private readonly IEliteStatus elite;
        private readonly IWaveSignal waves;
        private readonly Func<Ship, bool> introduces;
        private bool combatStarted;

        public SectorMoments(RunMomentStream moments, string faction, SectorHunt hunt, IEliteStatus elite,
            IWaveSignal waves, Func<Ship, bool> introduces)
        {
            this.moments = moments ?? throw new ArgumentNullException(nameof(moments));
            this.faction = faction ?? throw new ArgumentNullException(nameof(faction));
            this.introduces = introduces ?? throw new ArgumentNullException(nameof(introduces));
            this.hunt = hunt;
            this.elite = elite;
            this.waves = waves;
            if (hunt)
            {
                hunt.PatrolSpotted += OnPatrolSpotted;
                hunt.AlarmRaised += OnAlarmRaised;
                hunt.PatrolSilenced += OnPatrolSilenced;
            }

            if (elite != null)
            {
                elite.Announced += OnEliteAnnounced;
                elite.Beaten += OnEliteBeaten;
            }

            if (waves != null)
            {
                waves.WaveSent += OnWaveSent;
                waves.RivalFell += OnRivalFell;
            }
        }

        public void Dispose()
        {
            if (hunt)
            {
                hunt.PatrolSpotted -= OnPatrolSpotted;
                hunt.AlarmRaised -= OnAlarmRaised;
                hunt.PatrolSilenced -= OnPatrolSilenced;
            }

            if (elite != null)
            {
                elite.Announced -= OnEliteAnnounced;
                elite.Beaten -= OnEliteBeaten;
            }

            if (waves != null)
            {
                waves.WaveSent -= OnWaveSent;
                waves.RivalFell -= OnRivalFell;
            }
        }

        private void OnPatrolSpotted(int sectorIndex, Ship ship) =>
            moments.Raise(new PatrolSpotted(sectorIndex + 1, faction, ship.Id));

        private void OnAlarmRaised(int sectorIndex, HuntAlarmReason reason, Ship ship)
        {
            ShipId? patrol = null;
            if (ship) patrol = ship.Id;
            moments.Raise(new AlarmRaised(sectorIndex + 1, faction, reason, patrol));
        }

        private void OnPatrolSilenced(int sectorIndex, Ship ship) =>
            moments.Raise(new PatrolSilenced(sectorIndex + 1, faction, ship.Id));

        private void OnEliteAnnounced(EliteMoment moment) => moments.Raise(new EliteAnnounced(moment));

        private void OnEliteBeaten(EliteMoment moment) => moments.Raise(new EliteBeaten(moment));

        private void OnWaveSent(int sectorIndex, Ship rival)
        {
            if (combatStarted) return;
            combatStarted = true;
            if (introduces(rival)) moments.Raise(new RivalIntroduced(sectorIndex + 1, faction, rival));
            else moments.Raise(new CombatStarted(sectorIndex + 1, faction));
        }

        private void OnRivalFell(int sectorIndex) => moments.Raise(new RivalFell(sectorIndex + 1, faction));
    }
}
