using System;
using System.Collections;
using Game.Services;
using Ships;

namespace Game.Bootstrap
{
    public interface ISectorManager
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IGameServices services, SectorConfigSO config, ShipRespawnRunner respawnRunner = null);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
