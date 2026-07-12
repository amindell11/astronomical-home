using Game.Services;
using Player;
using UnityEditor;
using UnityEngine;

namespace AI.Debug
{
    /// <summary>Backs SessionRig's editor-overlay hook: the overlay is an editor-assembly
    /// MonoBehaviour the runtime rig cannot name. Interim seam until the Player domain's
    /// editor-assembly conversion.</summary>
    internal static class ArenaDebugOverlayInstaller
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            SessionRig.installDebugOverlay = Install;
        }

        private static Component Install(GameObject host, IUnitService units)
        {
            var overlay = host.GetComponent<ArenaDebugOverlay>();
            if (!overlay)
            {
                overlay = host.AddComponent<ArenaDebugOverlay>();
                // Build() can run on the rig prefab ASSET; DontSave keeps this editor-assembly
                // component from being baked into a shipping asset.
                overlay.hideFlags = HideFlags.DontSave;
            }
            overlay.Initialize(units);
            return overlay;
        }
    }
}
