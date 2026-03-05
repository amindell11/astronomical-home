using System;
using System.Collections;
using Game.Services;
using Ships;

namespace Game.Sectors
{
    public interface ISectorManager
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IGameServices services, SectorConfigSO config);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
