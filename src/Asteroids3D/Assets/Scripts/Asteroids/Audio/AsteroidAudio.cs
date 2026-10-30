using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Asteroids.Audio
{
    [RequireComponent(typeof(AsteroidController))]
    [RequireComponent(typeof(AsteroidDamage))]
    public sealed class AsteroidAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the asteroid breaks.")]
        [SerializeField] private EventReference breakEvent;

        [Tooltip("FMOD one-shot played where each hit lands on the asteroid.")]
        [SerializeField] private EventReference hitEvent;

        private AsteroidController asteroid;
        private AsteroidDamage damage;

        private void Awake()
        {
            asteroid = GetComponent<AsteroidController>();
            damage = GetComponent<AsteroidDamage>();
        }

        private void OnEnable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed += HandleDestroyed;
            damage.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed -= HandleDestroyed;
            damage.OnHit -= HandleHit;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void HandleDestroyed(Vector3 position) => RuntimeManager.PlayOneShot(breakEvent, position);

        private void HandleHit(Vector3 point) => RuntimeManager.PlayOneShot(hitEvent, point);
    }
}
