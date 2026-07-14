using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Driver-supplied composition inputs — "what exists / what to load" for a session, orthogonal to
    /// the driver's clock/reset policy. The driver sets it on <see cref="GameSession.Profile"/> before
    /// composing; the lifecycle primitives consume it. A headless/RL driver supplies its own.
    /// </summary>
    [System.Serializable]
    public class SessionProfile
    {
        [Tooltip("The sector to load. (Single-sector today; the future home for sector sequencing.)")]
        public SectorEntry sectorEntry;

        [Tooltip("When false, no player ship is built (spectator/headless).")]
        public bool buildPlayer = true;

        [Tooltip("When false, ship visual rigs + HUD/UI are disabled (headless/RL) — ships stay " +
                 "renderer/audio/particle-free while fully simulated.")]
        public bool presentation = true;

        [Tooltip("Global VFX toggle applied at compose — gates the not-yet-rig-migrated " +
                 "weapon/projectile/asteroid explosion effects. Runtime-only.")]
        public bool vfx = true;

        [Tooltip("This arena's in-plane world offset. Zero for the single-arena game; the RL harness " +
                 "supplies a per-arena offset.")]
        public Vector2 offset;
    }
}
