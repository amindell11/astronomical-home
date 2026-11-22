using Cameras.Shake;
using Substrate.Presentation;
using UnityEngine;

namespace Asteroids
{
    [RequireComponent(typeof(AsteroidController))]
    [RequireComponent(typeof(AsteroidDamage))]
    public sealed class AsteroidShake : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Camera shake played where the asteroid breaks. Its radius is the blast size.")]
        [SerializeField] private CameraShakeProfile profile;

        private AsteroidController asteroid;

        private void Awake()
        {
            asteroid = GetComponent<AsteroidController>();
        }

        private void OnEnable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed += HandleDestroyed;
        }

        private void OnDisable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed -= HandleDestroyed;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void HandleDestroyed(Vector3 position) => ShakeListener.Hear(position, profile, asteroid.Radius);
    }
}
