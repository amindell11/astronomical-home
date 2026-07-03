using Asteroids.Spawning;
using UnityEngine;
using Utils;

namespace Asteroids.Fields
{
    /// <summary>
    /// Base for hand-placed asteroid field elements: owns the settings asset
    /// and the sibling <see cref="Asteroids.Spawning.AsteroidSpawner"/>. The
    /// deterministic streaming brain lives in <see cref="UpdatingAsteroidField"/>.
    /// </summary>
    public partial class AsteroidField : MonoBehaviour
    {
        [SerializeField] protected AsteroidFieldSettings settings;

        protected AsteroidSpawner AsteroidSpawner { get; private set; }

        protected virtual void Awake()
        {
            gameObject.tag = TagNames.AsteroidField;
            AsteroidSpawner = GetComponent<AsteroidSpawner>() ?? gameObject.AddComponent<AsteroidSpawner>();
            CacheSettings();
        }

        protected virtual void CacheSettings()
        {
        }

        public void SetWorldAnchor(Transform anchor) => AsteroidSpawner?.SetWorldAnchor(anchor);

        public virtual void DespawnAll() => AsteroidSpawner?.DespawnAll();
    }
}
