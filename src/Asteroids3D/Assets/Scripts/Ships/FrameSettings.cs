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
        [Tooltip("Approximate collision radius of the ship. Used by MPC to inflate obstacle boundaries.")]
        public float shipRadius = 1f;

        [Header("Default Modules")]
        public EngineModule defaultEngine;
        public ShieldModule defaultShield;

        /// <summary>Number of weapon slots this frame exposes. Hardcoded for now (not yet modelled).</summary>
        public const int WeaponSlotCount = 2;

        /// <summary>Resolve this frame's stats using its default engine + shield modules.</summary>
        public ResolvedShipStats Resolve() => ResolvedShipStats.Resolve(this, defaultEngine, defaultShield);
    }
}
