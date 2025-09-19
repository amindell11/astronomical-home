using UnityEngine;

namespace Asteroids.Fragnetics
{
    [CreateAssetMenu(fileName = "FragSettings", menuName = "Asteroid/Fragmentation Settings", order = 0)]
    public class Settings : ScriptableObject
    {
        
        [Header("Fragmentation Settings")]
        [SerializeField]
        [Tooltip("Minimum mass threshold before an asteroid stops fragmenting")]
        public float minMass = 30f;
    
        [SerializeField] 
        [Tooltip("Minimum number of fragments created when an asteroid breaks")]
        public int minFragments = 2;
    
        [SerializeField]
        [Tooltip("Maximum number of fragments created when an asteroid breaks")] 
        public int maxFragments = 4;
    
        [SerializeField, Range(0.01f, 0.99f)]
        [Tooltip("Controls bias toward maximum fragments (0 = minimum, 1 = maximum)")]
        public float highCountBias = 0.5f;
    
        [SerializeField]
        [Tooltip("Base velocity for fragment separation")]
        public float baseSeparationSpeed = 5f;
    
        [SerializeField]
        [Tooltip("Maximum random rotation speed added to fragments in degrees/sec")]
        public float spinVariation = 30f;
        
        [SerializeField]
        [Tooltip("How efficiently projectile momentum couples into fragment separation (1 = direct)")]
        public float momentumCoupling = 1.0f;
    
        [SerializeField]
        [Tooltip("How strongly fragments move away from the asteroid's center")]
        public float outwardBias = 0.7f;
    
        [SerializeField]
        [Tooltip("How strongly fragments follow the direction of the impacting projectile")]
        public float bulletBias = 1.0f;
    
        [SerializeField]
        [Tooltip("Amount of random variation added to fragment directions")]
        public float randomBias = 0.3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction of asteroid mass preserved in fragments (e.g., 0.8 = 80% mass retained, 0.2 lost)")]
        public float massLossFactor = 1.0f;

        [Header("Visual Smoothing")]
        [SerializeField]
        [Tooltip("How long to fade in fragments after spawning (0 = instant)")]
        public float fragmentFadeInTime = 0.1f;
    }
}