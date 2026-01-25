using UnityEngine;

namespace Audio
{
    [System.Serializable]
    public struct OneShotSfx
    {
        [SerializeField] public AudioClip clip;
        [SerializeField, Range(0f,1f)] public float volume;

        public void PlayOn(AudioSource audioSource)
        {
            if (!audioSource || !clip) return;
            audioSource.PlayOneShot(clip, volume);
        }

        public void PlayAt(Vector3 position)
        {
            if (!clip) return;
            PooledAudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}


