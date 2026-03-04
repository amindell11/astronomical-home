using System;
using System.Collections;
using Game.Services;
using Ships;

namespace Game.Bootstrap
{
    public interface ISectorManager
    {
        event Action<SectorResult> OnSectorComplete;
        Ship PresentationShip { get; }
        void Initialize(IGameServices services, SectorConfigSO config, ShipRespawnRunner respawnRunner = null);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
