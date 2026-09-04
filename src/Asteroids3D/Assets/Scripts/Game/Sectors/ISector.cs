using System;
using System.Collections;
using Game.Services;
using Ships;

namespace Game.Sectors
{
    public interface ISector
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IGameServices services, SectorSettings config, WorldHandle world, Ship player);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
