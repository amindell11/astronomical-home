using UnityEngine;

namespace Game.Session
{
    public interface ISectorSessionOrchestrator
    {
        bool IsSessionActive { get; }
        SessionContext CurrentContext { get; }
        Coroutine StartSession(SectorSessionConfig config);
        void StopSession();
        Coroutine RestartSession(SectorSessionConfig config = null);
    }
}
