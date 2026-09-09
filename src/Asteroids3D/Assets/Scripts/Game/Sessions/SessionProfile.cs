using Game.Sectors;
using UnityEngine;

namespace Game.Sessions
{
    /// <summary>
    /// Host-supplied composition inputs — "what exists / what to load" for a session, orthogonal to
    /// the host's clock/reset policy. A host authors it (the interactive host serializes one on its
    /// inspector) and hands it to the <see cref="Session"/> constructor.
    /// </summary>
    [System.Serializable]
    public class SessionProfile
    {
        [Tooltip("The sector to load. (Single-sector today; the future home for sector sequencing.)")]
        public SectorEntry sectorEntry;

        [Tooltip("When false, no player ship is built (spectator/headless).")]
        public bool buildPlayer = true;

        [Tooltip("When false, ship visual rigs, HUD/UI and one-shot VFX are disabled (headless/RL) — " +
                 "ships stay renderer/audio/particle-free while fully simulated.")]
        public bool presentation = true;

        [Tooltip("This session's in-plane frame offset. Zero for the single-arena game; multi-session " +
                 "hosts supply a per-session offset.")]
        public Vector2 offset;
    }

    [System.Serializable]
    public class SectorEntry
    {
        public SectorSettings config;
        public Sector prefab;
    }
}
