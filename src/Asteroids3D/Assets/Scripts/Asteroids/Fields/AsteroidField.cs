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
    public class AsteroidField : MonoBehaviour
    {
        [SerializeField] protected internal AsteroidFieldSettings settings;

        protected AsteroidSpawner AsteroidSpawner { get; private set; }

        private bool presentationEnabled = true;

        protected virtual void Awake()
        {
            gameObject.tag = TagNames.AsteroidField;
            AsteroidSpawner = GetComponent<AsteroidSpawner>() ?? gameObject.AddComponent<AsteroidSpawner>();
            AsteroidSpawner.SetPresentation(presentationEnabled);
            CacheSettings();
        }

        protected virtual void CacheSettings()
        {
        }

        public void SetWorldAnchor(Transform anchor) => AsteroidSpawner?.SetWorldAnchor(anchor);

        /// <summary>Pre-Awake-safe stash (sector Produce runs before this field's Awake); forwarded to the spawner on wiring.</summary>
        public void SetPresentation(bool enabled)
        {
            presentationEnabled = enabled;
            AsteroidSpawner?.SetPresentation(enabled);
        }

        public virtual void DespawnAll() => AsteroidSpawner?.DespawnAll();
    }
}
