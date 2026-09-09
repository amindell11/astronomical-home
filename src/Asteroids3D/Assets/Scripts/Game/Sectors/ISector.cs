using System;
using System.Collections;
using Game.Services.Units;
using Game.Services.Objectives;
using Game.Sessions;
using Ships;

namespace Game.Sectors
{
    public interface ISector
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IUnitService units, IObjectiveService objectives, bool presentationEnabled,
            SectorSettings config, SessionFrame frame, Ship player);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
