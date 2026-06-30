#if UNITY_EDITOR
using AI.Debug;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Editor-only debug-overlay installer for <see cref="PlaySector"/>. The overlay is an
    /// <c>#if UNITY_EDITOR</c> component, so it cannot be authored on a prefab — it is added at
    /// runtime and self-subscribes to <c>UnitService.OnShipSpawned</c> to auto-track every ship.
    /// </summary>
    public partial class PlaySector
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay;
        [SerializeField] private AIDebugSettings debugSettings;

        private ArenaDebugOverlay debugOverlay;

        partial void InitializeDebugOverlay()
        {
            if (!enableDebugOverlay) return;

            debugOverlay = gameObject.AddComponent<ArenaDebugOverlay>();
            debugOverlay.Initialize(debugSettings, Services.UnitService);
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
