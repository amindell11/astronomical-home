using UnityEngine;
using Utils;

namespace Ships.Visuals.Breakup
{
    /// <summary>
    /// The named knobs every ship's death shares: its debris explosion and how fast its baked debris
    /// plays. Every ship's death reads the asset its rig serializes; the preview host edits that same
    /// asset live and saves it explicitly.
    /// </summary>
    [CreateAssetMenu(fileName = "DeathFeel", menuName = "Ship/Death Feel")]
    public sealed class DeathFeelProfile : ScriptableObject
    {
        [Tooltip("The explosion that bursts where a ship breaks up.")]
        [SerializeField] private PooledVFX debrisExplosion;

        [Tooltip("How fast every ship's baked debris plays (2 = twice as fast). Its motion, piece lifetimes " +
                 "and fade scale together.")]
        [SerializeField, Min(.1f)] private float pace = 1f;

        public PooledVFX DebrisExplosion => debrisExplosion;
        public float Pace => pace;
    }
}
