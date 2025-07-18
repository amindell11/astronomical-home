using UnityEngine;
using System.Collections;

/// <summary>
/// A pooled AudioSource component that replaces AudioSource.PlayClipAtPoint
/// to avoid GameObject allocation overhead for one-shot audio playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PooledAudioSource : MonoBehaviour
{
    private AudioSource audioSource;

    // Static prefab instance used by the pool – created once then reused
    private static PooledAudioSource prefab;
    /// <summary>
    /// Play a clip at the specified position with volume, then return to pool
    /// </summary>
    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        // Lazily create a single hidden prefab that will be cloned by the pool.
        if (prefab == null)
        {
            prefab = CreateNewInstance();
            prefab.gameObject.name = "PooledAudioSource_Prefab";

            // Hide the prefab in hierarchy & keep across scenes
            prefab.gameObject.SetActive(false);
            Object.DontDestroyOnLoad(prefab.gameObject);
        }

        // Retrieve an instance from the pool (will instantiate the first time)
        PooledAudioSource pooledSource = SimplePool<PooledAudioSource>.Get(
            prefab, position, Quaternion.identity);

        // Play the requested clip
        pooledSource.PlayClip(clip, volume);
    }
    
    private void PlayClip(AudioClip clip, float volume)
    {
        // Lazy initialization of audioSource
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        
        // Cancel any pending return-to-pool calls
        CancelInvoke(nameof(ReturnToPool));
        
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
        
        // Schedule return to pool after clip finishes
        Invoke(nameof(ReturnToPool), clip.length);
    }
    
    private void ReturnToPool()
    {
        // Stop audio and return to pool
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        
        SimplePool<PooledAudioSource>.Release(this);
    }
    
    private static PooledAudioSource CreateNewInstance()
    {
        // Create a new instance directly (no prefab needed)
        GameObject go = new GameObject("PooledAudioSource");
        
        // Add AudioSource component and configure it
        AudioSource audioSrc = go.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 1f; // 3D spatial audio
        
        return go.AddComponent<PooledAudioSource>();
    }
} 