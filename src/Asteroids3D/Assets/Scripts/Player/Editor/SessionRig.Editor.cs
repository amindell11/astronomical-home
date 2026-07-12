#if UNITY_EDITOR
using AI.Debug;
using Game.Services;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Editor-only debug-overlay installer for <see cref="SessionRig"/>. The overlay is an
    /// <c>#if UNITY_EDITOR</c> component, so it cannot be authored on a prefab — it is added at
    /// runtime and self-subscribes to <c>UnitService.OnShipSpawned</c> to auto-track every ship.
    /// It lives on the session rig (not the sector) so it tracks ships across sector loads.
    /// </summary>
    public partial class SessionRig
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay;
        [SerializeField] private AIDebugSettings debugSettings;

        private ArenaDebugOverlay debugOverlay;

        partial void InitializeDebugOverlay(IGameServices services)
        {
            if (!enableDebugOverlay) return;

            // Reuse before adding: Build() can run on the rig prefab ASSET (SessionHost references
            // it directly), so an unconditional AddComponent bakes a new overlay into the prefab file
            // every editor session.
            debugOverlay = GetComponent<ArenaDebugOverlay>();
            if (!debugOverlay)
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
