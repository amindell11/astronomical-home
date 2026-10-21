using Damage;
using Ships;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using Ships.Registry;

namespace Ships.Audio
{
    /// <summary>
    /// Centralises damage-related SFX for a ship (shield hits, hull hits, death explosion).
    /// Requires its own <see cref="AudioSource"/> so that volume & EQ can be routed
    /// independently from weapons or engine sounds. The damage surface is injected via
    /// <see cref="Bind"/> (see <see cref="IShipVisual"/>).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ShipDamageAudio : MonoBehaviour, IShipVisual
    {
        [Header("Clips")] [SerializeField] private AudioClip shieldHitClip;
        [SerializeField] private AudioClip shieldDepletedClip;
        [SerializeField] private AudioClip hullHitClip;
        [SerializeField] private AudioClip deathClip;
        [Tooltip("Death sound when the killing blow was an asteroid collision. Null → deathClip.")]
        [SerializeField] private AudioClip collisionDeathClip;

        [Header("Volumes")] [SerializeField, Range(0f, 1f)]
        private float shieldVolume = 0.8f;

        [SerializeField, Range(0f, 1f)] private float shieldDepletedVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float hullVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;

        private AudioSource source;
        private IDamageEvents damage;
        private bool subscribed;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // fully 3-D positional
        }

        public void Bind(in ShipView view)
        {
            damage = view.Damage;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (damage != null) Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || damage == null) return;
            damage.Shield.OnValueChanged += HandleShieldChanged;
            damage.Health.OnValueChanged += HandleHealthChanged;
            damage.OnDeath += HandleDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || damage == null) return;
            damage.Shield.OnValueChanged -= HandleShieldChanged;
            damage.Health.OnValueChanged -= HandleHealthChanged;
            damage.OnDeath -= HandleDeath;
            subscribed = false;
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

        private void HandleDeath(ShipId _victimId, DamageInfo killingBlow)
        {
            var clip = killingBlow.Kind == DamageKind.Collision && collisionDeathClip
                ? collisionDeathClip
                : deathClip;
            if (clip)
                global::Audio.PooledAudioSource.PlayClipAtPoint(clip, transform.position, deathVolume);
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
