using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Provides a dedicated <see cref="AudioSource"/> for weapon-related one-shot sounds
/// (lasers, missiles, lock-on beeps). Launcher scripts will automatically find this
/// source via <c>GetComponentInChildren&lt;AudioSource&gt;()</c> when they call
/// <see cref="AudioSource.PlayOneShot"/>.
///
/// Keep this component on the ship root so all turrets/launchers can locate it.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class WeaponsAudio : MonoBehaviour
{
    [Tooltip("Optional mixer group for all weapon SFX")]           
    [SerializeField] private AudioMixerGroup mixerGroup;

    private AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop          = false;
        source.playOnAwake   = false;
        source.spatialBlend  = 1f; // 3-D positional
        if (mixerGroup) source.outputAudioMixerGroup = mixerGroup;
    }

    /// <summary>
    /// Public helper in case another script wants to trigger a weapon one-shot manually.
    /// </summary>
    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip && source)
            source.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Exposes the underlying AudioSource so existing launcher code can continue to
    /// retrieve it if needed.
    /// </summary>
    public AudioSource Source => source;
} 