using System;

namespace Substrate.Sectors.Elements.Elites
{
    public interface IEliteStatus
    {
        event Action<EliteMoment> Announced;

        string Name { get; }

        /// <summary>True while an elite ship is alive in the sector.</summary>
        bool Present { get; }

        float HullPct { get; }
        float ShieldPct { get; }
    }
}
