using Damage;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Audio
{
    [RequireComponent(typeof(Laser))]
    public class LaserAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the projectile hits.")]
        [SerializeField] private EventReference impactEvent;

        private Laser laser;

        private void Awake()
        {
            laser = GetComponent<Laser>();
        }

        private void OnEnable()
        {
            if (laser) laser.Hit += HandleHit;
        }

        private void OnDisable()
        {
            if (laser) laser.Hit -= HandleHit;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void HandleHit(Vector3 position, IDamageable _) => RuntimeManager.PlayOneShot(impactEvent, position);
    }
}
