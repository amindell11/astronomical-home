#if UNITY_EDITOR
using AI.Debug;
using UnityEngine;

namespace Game.Sectors
{
    public partial class ArenaSector
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay = true;
        [SerializeField] private AIDebugSettings debugSettings;

        private ArenaDebugOverlay debugOverlay;

        partial void InitializeDebugOverlay()
        {
            if (!enableDebugOverlay) return;

            debugOverlay = gameObject.AddComponent<ArenaDebugOverlay>();
            debugOverlay.Initialize(debugSettings);
            if (player) debugOverlay.RegisterShip(player);
            foreach (var ship in arenaShips)
                debugOverlay.RegisterShip(ship);
        }

        partial void TeardownDebugOverlay()
        {
            if (debugOverlay)
                Destroy(debugOverlay);
            debugOverlay = null;
        }
    }
}
#endif
