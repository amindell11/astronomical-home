using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Global static class for game-wide settings like graphics, audio, etc.
    /// Loads settings from PlayerPrefs on startup.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Global toggle for all visual effects (VFX).</summary>
        public static bool VfxEnabled { get; private set; }

        /// <summary>
        /// Global toggle for ship visual presentation (each ship prefab's embedded visual rig). A
        /// per-session policy — a headless/RL session turns it off so every ship's rig self-disables
        /// and stays renderer-, audio- and particle-free while the ship remains fully simulated.
        /// Defaults on; runtime-only (never persisted).
        /// </summary>
        public static bool PresentationEnabled { get; private set; } = true;

        /// <summary>
        /// This method is called by Unity when the game engine starts,
        /// before any scene has loaded. It ensures our settings are loaded right away.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            LoadSettings();
        }

        /// <summary>
        /// Loads settings from PlayerPrefs. Defaults to 'true' (enabled) if not found.
        /// </summary>
        public static void LoadSettings()
        {
            // Get the "VFX_ENABLED" value from storage, defaulting to 1 (true).
            VfxEnabled = PlayerPrefs.GetInt("VFX_ENABLED", 1) == 1;

            // Presentation is a per-session policy, not a saved preference: default it on each load and
            // let a game-tier caller (MainGameManager) turn it off for a headless/RL session.
            PresentationEnabled = true;
        }

        /// <summary>
        /// Sets the global VFX toggle. By default this is a runtime-only override (does NOT persist),
        /// which is what a game-tier caller like <c>MainGameManager</c> wants — a headless/RL session
        /// disabling VFX must not leak into the player's saved preference. Pass <paramref name="persist"/>
        /// = true (e.g. from a settings menu) to also write it to PlayerPrefs for future sessions.
        /// </summary>
        public static void SetVfxEnabled(bool enabled, bool persist = false)
        {
            VfxEnabled = enabled;
            if (persist)
            {
                PlayerPrefs.SetInt("VFX_ENABLED", enabled ? 1 : 0);
                PlayerPrefs.Save(); // Immediately write to disk
            }
        }

        /// <summary>
        /// Set the runtime presentation toggle (session policy — a headless/RL session turns it off).
        /// Runtime-only: never persisted, so a headless run can't leak into the player's saved prefs.
        /// </summary>
        public static void SetPresentationEnabled(bool enabled) => PresentationEnabled = enabled;
    }
} 