using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Global game-wide presentation settings. Every value is a per-session policy composed by a
    /// game-tier caller (<c>SessionHost</c>, or an RL host for a headless run) and is never persisted —
    /// so a headless/RL session cannot leak into the next play session.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Global toggle for all visual effects (VFX). A sub-capability of presentation.</summary>
        public static bool VfxEnabled { get; private set; } = true;

        /// <summary>
        /// Global toggle for ship visual presentation (each ship prefab's embedded visual rig). A
        /// headless/RL session turns it off so every ship's rig self-disables and stays renderer-,
        /// audio- and particle-free while the ship remains fully simulated.
        /// </summary>
        public static bool PresentationEnabled { get; private set; } = true;

        /// <summary>
        /// Statics survive domain reloads between editor play sessions, so the defaults are restored
        /// before any scene loads rather than left carrying the previous session's policy.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetToSessionDefaults()
        {
            VfxEnabled = true;
            PresentationEnabled = true;
        }

        public static void SetVfxEnabled(bool enabled) => VfxEnabled = enabled;

        public static void SetPresentationEnabled(bool enabled) => PresentationEnabled = enabled;
    }
}
