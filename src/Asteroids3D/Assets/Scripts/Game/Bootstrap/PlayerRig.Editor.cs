#if UNITY_EDITOR
using AI.Debug;
using Game.Services;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Editor-only debug-overlay installer for <see cref="PlayerRig"/>. The overlay is an
    /// <c>#if UNITY_EDITOR</c> component, so it cannot be authored on a prefab — it is added at
    /// runtime and self-subscribes to <c>UnitService.OnShipSpawned</c> to auto-track every ship.
    /// It lives on the session rig (not the sector) so it tracks ships across sector loads.
    /// </summary>
    public partial class PlayerRig
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay;
        [SerializeField] private AIDebugSettings debugSettings;

        private ArenaDebugOverlay debugOverlay;

        partial void InitializeDebugOverlay(IGameServices services)
        {
            if (!enableDebugOverlay) return;

            debugOverlay = gameObject.AddComponent<ArenaDebugOverlay>();
            debugOverlay.Initialize(debugSettings, services.UnitService);
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
