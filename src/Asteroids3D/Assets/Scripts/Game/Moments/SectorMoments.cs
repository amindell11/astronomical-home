using System;
using Ships;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;

namespace Game.Moments
{
    /// <summary>
    /// Bridges one sector's hunt and elite events onto the run's moment stream.
    /// The host disposes it when the sector closes, so a dead sector's events
    /// never reach the next one's stream.
    /// </summary>
    public sealed class SectorMoments : IDisposable
    {
        private readonly RunMomentStream moments;
        private readonly string faction;
        private readonly SectorHunt hunt;
        private readonly IEliteStatus elite;

        public SectorMoments(RunMomentStream moments, string faction, SectorHunt hunt, IEliteStatus elite)
        {
            this.moments = moments ?? throw new ArgumentNullException(nameof(moments));
            this.faction = faction ?? throw new ArgumentNullException(nameof(faction));
            this.hunt = hunt;
            this.elite = elite;
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
    }
}
