using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Asteroids.Audio
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the asteroid breaks.")]
        [SerializeField] private EventReference breakEvent;

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

        private void HandleDestroyed(Vector3 position) => RuntimeManager.PlayOneShot(breakEvent, position);
    }
}
