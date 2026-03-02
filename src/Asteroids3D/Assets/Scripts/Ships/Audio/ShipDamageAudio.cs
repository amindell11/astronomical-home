using Ships;
using Ships.Damage;
using UnityEngine;

namespace Ships.Audio
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
        private DamageController damage;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            damage = GetComponentInParent<DamageController>();
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // fully 3-D positional
        }

        private void OnEnable()
        {
            if (!damage) return;
            damage.Shield.OnValueChanged += HandleShieldChanged;
            damage.Health.OnValueChanged += HandleHealthChanged;
            damage.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (!damage) return;
            damage.Shield.OnValueChanged -= HandleShieldChanged;
            damage.Health.OnValueChanged -= HandleHealthChanged;
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

        private void HandleDeath(ShipId _victimId, ShipId _killerId)
        {
            if (deathClip)
                global::Audio.PooledAudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);
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
