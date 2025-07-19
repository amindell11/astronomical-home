using UnityEngine;

/// <summary>
/// Centralises damage-related SFX for a ship (shield hits, hull hits, death explosion).
/// Requires its own <see cref="AudioSource"/> so that volume & EQ can be routed
/// independently from weapons or engine sounds.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class ShipDamageAudio : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip shieldHitClip;
    [SerializeField] private AudioClip hullHitClip;
    [SerializeField] private AudioClip deathClip;

    [Header("Volumes")]
    [SerializeField, Range(0f,1f)] private float shieldVolume = 0.8f;
    [SerializeField, Range(0f,1f)] private float hullVolume   = 1f;
    [SerializeField, Range(0f,1f)] private float deathVolume  = 1f;

    private AudioSource source;
    private ShipDamageHandler damageHandler;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop         = false;
        source.playOnAwake  = false;
        source.spatialBlend = 1f; // fully 3-D positional
    }

    void OnEnable()
    {
        if (!damageHandler)
            damageHandler = GetComponentInParent<ShipDamageHandler>();

        if (damageHandler != null)
        {
            damageHandler.OnShieldDamaged += HandleShieldHit;
            damageHandler.OnDamaged       += HandleHullHit;
            damageHandler.OnDeath         += HandleDeath;
        }
    }

    void OnDisable()
    {
        if (damageHandler != null)
        {
            damageHandler.OnShieldDamaged -= HandleShieldHit;
            damageHandler.OnDamaged       -= HandleHullHit;
            damageHandler.OnDeath         -= HandleDeath;
        }
    }

    void HandleShieldHit(float dmg, Vector3 _)
    {
        if (shieldHitClip)
            source.PlayOneShot(shieldHitClip, shieldVolume);
    }

    void HandleHullHit(float dmg, Vector3 _)
    {
        if (hullHitClip)
            source.PlayOneShot(hullHitClip, hullVolume);
    }

    void HandleDeath(Ship victim, Ship _)
    {
        if (deathClip)
            PooledAudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);
    }
} 