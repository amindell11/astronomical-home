using UnityEngine;

namespace Asteroids.Audio
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip explosionSound;
        [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.7f;

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

        private void HandleDestroyed(Vector3 position)
        {
            if (!explosionSound) return;
            global::Audio.PooledAudioSource.PlayClipAtPoint(explosionSound, position, explosionVolume);
        }
    }
}
