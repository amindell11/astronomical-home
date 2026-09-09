using System;
using System.Collections;
using Cameras;
using Game.Services;
using Game.Sessions;
using Ships;

namespace Game.Sectors
{
    public interface ISector
    {
        event Action<SectorResult> OnSectorComplete;
        void Initialize(IGameServices services, SectorSettings config, SessionFrame frame, Ship player,
            ObserverCam observer = null);
        IEnumerator Setup();
        IEnumerator Teardown();
    }
}
