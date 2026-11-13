using System;
using System.Collections.Generic;

namespace Game.Runs
{
    /// <summary>
    /// The pick screens and sector rows of one open run. The host notes them as they close
    /// and reads them once into the run record.
    /// </summary>
    public sealed class RunCourse
    {
        private readonly List<PickRow> picks = new();
        private readonly List<SectorRow> sectors = new();
        private bool open;
        private int sector;
        private float start;
        private float hullIn;
        private float shieldIn;

        public bool IsOpen => open;

        public IReadOnlyList<PickRow> Picks => picks;

        public IReadOnlyList<SectorRow> Sectors => sectors;

        public void NotePick(string kind, int sectorNumber, float seconds, IReadOnlyList<string> offered,
            string taken, string replaced)
        {
            picks.Add(new PickRow
            {
                kind = kind,
                sector = sectorNumber,
                seconds = seconds,
                offered = new List<string>(offered),
                taken = taken ?? string.Empty,
                replaced = replaced ?? string.Empty,
            });
        }

        public void Enter(int sectorNumber, float now, float hull, float shield)
        {
            if (open) throw new InvalidOperationException("A sector row is already open.");
            open = true;
            sector = sectorNumber;
            start = now;
            hullIn = hull;
            shieldIn = shield;
        }

        public void End(float now, int kills, float hull, float shield, bool eliteMet, bool eliteBeaten)
        {
            if (!open) throw new InvalidOperationException("No sector row is open.");
            sectors.Add(new SectorRow
            {
                sector = sector,
                seconds = now - start,
                kills = kills,
                hullIn = hullIn,
                shieldIn = shieldIn,
                hullOut = hull,
                shieldOut = shield,
                eliteMet = eliteMet,
                eliteBeaten = eliteBeaten,
            });
            open = false;
        }
    }
}
