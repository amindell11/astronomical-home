using UnityEngine;

/// <summary>
/// Plays audio cues driven by LaserGun overheat events.
/// Subscribes to the LaserGun's OnOverheat and OnCooldownStart events.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UILaserAudio : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Sound that plays when the laser gun overheats.")]
    [SerializeField] private AudioClip overheatClip;
    [Tooltip("Optional sound that plays when the laser gun starts cooling down from overheat.")]
    [SerializeField] private AudioClip cooldownClip;

    [Header("Settings")]
    [Tooltip("Volume for overheat sound effects.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    private AudioSource audioSource;
    private LaserGun laserGun;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // Attempt to find the player's laser gun at startup
        TryAssignLaserGun();
    }

    void Start()
    {
        // Try again in Start in case player wasn't ready in Awake
        if (laserGun == null)
        {
            TryAssignLaserGun();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (laserGun != null)
        {
            laserGun.OnOverheat -= PlayOverheatSound;
            laserGun.OnCooldownStart -= PlayCooldownSound;
        }
    }

    void PlayOverheatSound()
    {
        if (overheatClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(overheatClip, volume);
        }
    }

    void PlayCooldownSound()
    {
        if (cooldownClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(cooldownClip, volume);
        }
    }

    void TryAssignLaserGun()
    {
        var playerObj = GameObject.FindGameObjectWithTag(TagNames.Player);
        if (playerObj != null)
        {
            laserGun = playerObj.GetComponentInChildren<LaserGun>();
            
            // Subscribe to events
            if (laserGun != null)
            {
                laserGun.OnOverheat += PlayOverheatSound;
                laserGun.OnCooldownStart += PlayCooldownSound;
            }
        }
    }
} 