using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Game-wide presentation policy, composed per session by a game-tier caller (<c>SessionHost</c>,
    /// or an RL host for a headless run). Never persisted, so a headless run cannot leak into play.
    /// </summary>
    public static class GameSettings
    {
        public static bool VfxEnabled { get; private set; } = true;

        /// <summary>
        /// Off makes every ship's embedded visual rig self-disable — renderer-, audio- and
        /// particle-free while the ship remains fully simulated.
        /// </summary>
        public static bool PresentationEnabled { get; private set; } = true;

        // Statics outlive editor play sessions, so restore defaults before any scene loads.
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
