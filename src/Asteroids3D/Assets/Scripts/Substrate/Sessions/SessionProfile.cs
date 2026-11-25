using Substrate.Services.Locales;
using UnityEngine;

namespace Substrate.Sessions
{
    /// <summary>
    /// Host-supplied composition inputs — "what exists / what to load" for a session, orthogonal to
    /// the host's clock/reset policy. A host authors it (the interactive host serializes one on its
    /// inspector) and hands it to the <see cref="Session"/> constructor.
    /// </summary>
    [System.Serializable]
    public class SessionProfile
    {
        [Tooltip("The sector run to load: its sectors in order, each with its locale.")]
        public SectorRun run;

        [Tooltip("Locale shown while no sector is loaded; a sector naming the same locale keeps its instance. " +
                 "Unassigned → no sky, under the session scene's default lighting.")]
        public LocaleSky idleLocale;

        [Tooltip("When false, ship visual rigs, HUD/UI and one-shot VFX are disabled (headless/RL) — " +
                 "ships stay renderer/audio/particle-free while fully simulated.")]
        public bool presentation = true;

        [Tooltip("This session's in-plane frame offset. Zero for the single-arena game; multi-session " +
                 "hosts supply a per-session offset.")]
        public Vector2 offset;
    }
}
