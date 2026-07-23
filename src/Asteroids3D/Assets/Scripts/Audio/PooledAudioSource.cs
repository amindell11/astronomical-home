using UnityEngine;
using Utils;

namespace Audio
{
    /// <summary>
    /// A pooled AudioSource component that replaces AudioSource.PlayClipAtPoint
    /// to avoid GameObject allocation overhead for one-shot audio playback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioSource : MonoBehaviour
    {
        private AudioSource audioSource;

        // Static prefab instance used by the pool – created once then reused
        private static PooledAudioSource _prefab;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
        /// <summary>
        /// Play a clip at the specified position with volume, then return to pool
        /// </summary>
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            // Interim process-wide backstop (this static seam has no session identity): a headless
            // session plays no one-shots even from callers the spawn seams don't cover.
            if (!GameSettings.PresentationEnabled) return;

            // Lazily create a single hidden prefab that will be cloned by the pool.
            if (_prefab == null)
            {
                _prefab = CreateNewInstance();
                _prefab.gameObject.name = "PooledAudioSource_Prefab";

                // Hide the prefab in hierarchy & keep across scenes
                _prefab.gameObject.SetActive(false);
                Object.DontDestroyOnLoad(_prefab.gameObject);
            }

            // Retrieve an instance from the pool (will instantiate the first time)
            var pooledSource = SimplePool<PooledAudioSource>.Get(
                _prefab, position, Quaternion.identity);

            // Play the requested clip
            pooledSource.PlayClip(clip, volume);
        }
    
        private void PlayClip(AudioClip clip, float volume)
        {
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
            if (audioSource && audioSource.isPlaying)
                audioSource.Stop();
        
            SimplePool<PooledAudioSource>.Release(this);
        }
    
        private static PooledAudioSource CreateNewInstance()
        {
            // Create a new instance directly (no prefab needed)
            var go = new GameObject("PooledAudioSource");
        
            // Add AudioSource component and configure it
            var audioSrc = go.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f; // 3D spatial audio
        
            return go.AddComponent<PooledAudioSource>();
        }
    }
} 
