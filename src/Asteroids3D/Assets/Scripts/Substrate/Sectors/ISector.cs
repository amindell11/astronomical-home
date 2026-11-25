using System;
using System.Collections;
using Substrate.Services.Units;
using Substrate.Services.Objectives;
using Substrate.Sessions;
using Ships;

namespace Substrate.Sectors
{
    public interface ISector
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IUnitService units, IObjectiveService objectives, bool presentationEnabled,
            SessionFrame frame, Ship hero, SectorEntry row, int sectorIndex = 0);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
