using System;
using System.Collections;
using Game.Services;

namespace Game.Bootstrap
{
    public interface ISectorManager
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IGameServices services, SectorConfigSO config);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
