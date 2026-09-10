using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Game-wide presentation policy, set per session by <c>Session.Compose</c> from its profile.
    /// Never persisted, so a headless run cannot leak into play.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>
        /// Off skips <c>GameSessionHost</c>'s hangar screen and death recap; ships and transients are
        /// darkened by their own spawn seams, not by this flag.
        /// </summary>
        public static bool PresentationEnabled { get; private set; } = true;

        // Statics outlive editor play sessions, so restore defaults before any scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetToSessionDefaults() => PresentationEnabled = true;

        public static void SetPresentationEnabled(bool enabled) => PresentationEnabled = enabled;
    }
}
