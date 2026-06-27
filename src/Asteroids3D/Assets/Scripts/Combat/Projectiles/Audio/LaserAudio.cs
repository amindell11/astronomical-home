using Damage;
using UnityEngine;

namespace Combat.Projectile.Audio
{
    [RequireComponent(typeof(Laser))]
    public class LaserAudio : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 1f;

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

        private void HandleHit(Vector3 position, IDamageable _)
        {
            if (hitClips == null || hitClips.Length == 0) return;
            var clip = hitClips.Length == 1 ? hitClips[0] : hitClips[Random.Range(0, hitClips.Length)];
            if (clip) global::Audio.PooledAudioSource.PlayClipAtPoint(clip, position, hitVolume);
        }
    }
}
