#if UNITY_EDITOR
using Game.Services;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Editor-only debug-overlay installer for <see cref="SessionRig"/>. The overlay is an
    /// editor-assembly component this runtime partial cannot name, so the editor assembly's
    /// ArenaDebugOverlayInstaller assigns <see cref="installDebugOverlay"/> — an interim seam
    /// until the Player domain's own editor-assembly conversion.
    /// </summary>
    public partial class SessionRig
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay;

        internal static System.Func<GameObject, IUnitService, Component> installDebugOverlay;

        private Component debugOverlay;

        partial void InitializeDebugOverlay(IGameServices services)
        {
            if (!enableDebugOverlay || installDebugOverlay == null) return;
            debugOverlay = installDebugOverlay(gameObject, services.UnitService);
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
