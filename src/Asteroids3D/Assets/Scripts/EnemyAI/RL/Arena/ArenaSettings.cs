using UnityEngine;

namespace EnemyAI.RL.Arena
{
    /// <summary>
    /// ScriptableObject to hold all environment-specific settings for an arena.
    /// These settings can be shared across multiple arenas or customized for each.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaSettings", menuName = "ML-Agents/Arena Settings", order = 0)]
    public class ArenaSettings : ScriptableObject
    {
        [Tooltip("Radius of the arena in world units")]
        public float arenaSize = 100f;

        [Tooltip("Target density of asteroids in the field")]
        public float asteroidDensity = 0.05f;

        [Tooltip("Difficulty of the curriculum bot (if present)")]
        public float botDifficulty = 1f;
    }
} 