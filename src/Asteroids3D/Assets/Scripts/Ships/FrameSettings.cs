using Ships.Presentation;
using UnityEngine;

namespace Ships
{
    /// <summary>
    /// The ship archetype / chassis handle. Carries chassis stats (health, mass, geometry) and slot
    /// metadata, plus references to the DEFAULT modules a ship of this frame ships with. This is the
    /// single drop-in replacement for the old monolithic <c>ShipSettings</c> everywhere a "which ship"
    /// handle is passed through spawn/config/factory plumbing.
    /// </summary>
    [CreateAssetMenu(fileName = "FrameSettings", menuName = "Ship/FrameSettings")]
    public class FrameSettings : TunableModule
    {
        [Header("Chassis")]
        public float mass = 1000;
        public int startingLives = 1;
        public float maxHealth = 100f;
        public float maxBankAngle = 45f;

        [Header("Geometry")]
        [Tooltip("Uniform spawn scale applied to the ship root. Base geometry is authored at scale 1; " +
                 "the collision radius is derived from the scaled collider bounds at spawn.")]
        public float size = 1f;

        [Header("Visuals")]
        [Tooltip("Visual-rig prefab the presentation installer attaches under a ship of this frame. " +
                 "Null → no rig (headless/RL ship stays renderer-, audio- and particle-free).")]
        public ShipVisualRig visualRig;

        [Header("Default Modules")]
        public EngineModule defaultEngine;
        public ShieldModule defaultShield;

        /// <summary>Number of weapon slots this frame exposes. Hardcoded for now (not yet modelled).</summary>
        public const int WeaponSlotCount = 2;

        /// <summary>Resolve this frame's stats using its default engine + shield modules.</summary>
        public ResolvedShipStats Resolve() => ResolvedShipStats.Resolve(this, defaultEngine, defaultShield);
    }
}
