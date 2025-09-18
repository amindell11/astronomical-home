using Ships;
using UnityEngine;
using Utils;

namespace Audio
{
    /// <summary>
    /// Centralises damage-related SFX for a ship (shield hits, hull hits, death explosion).
    /// Requires its own <see cref="AudioSource"/> so that volume & EQ can be routed
    /// independently from weapons or engine sounds.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ShipDamageAudio : MonoBehaviour
    {
        [Header("Clips")] [SerializeField] private AudioClip shieldHitClip;
        [SerializeField] private AudioClip shieldDepletedClip;
        [SerializeField] private AudioClip hullHitClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Volumes")] [SerializeField, Range(0f, 1f)]
        private float shieldVolume = 0.8f;

        [SerializeField, Range(0f, 1f)] private float shieldDepletedVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float hullVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;

        private AudioSource source;
        private Ships.Damage damage;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // fully 3-D positional
        }

        private void OnEnable()
        {
            if (!damage)
                damage = GetComponentInParent<Ships.Damage>();
            if (!damage) return;
            damage.OnShieldChanged += HandleShieldChanged;
            damage.OnHealthChanged += HandleHealthChanged;
            damage.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (!damage) return;
            damage.OnShieldChanged -= HandleShieldChanged;
            damage.OnHealthChanged -= HandleHealthChanged;
            damage.OnDeath -= HandleDeath;
        }

        private void HandleShieldChanged(float current, float previous, float max)
        {
            if (current < previous) PlayShieldHit();
            if (previous > 0f && current <= 0f) PlayShieldDepleted();
        }

        private void HandleHealthChanged(float current, float previous, float max)
        {
            if (current < previous) PlayHullHit();
        }

        private void HandleDeath(Ships.Ship victim, Ships.Ship _)
        {
            if (deathClip)
                PooledAudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);
        }

        private void PlayShieldDepleted()
        {
            if (shieldDepletedClip)
                source.PlayOneShot(shieldDepletedClip, shieldDepletedVolume);
        }

        private void PlayShieldHit()
        {
            if (shieldHitClip)
                source.PlayOneShot(shieldHitClip, shieldVolume);
        }

        private void PlayHullHit()
        {
            if (hullHitClip)
                source.PlayOneShot(hullHitClip, hullVolume);
        }
    }

} 
