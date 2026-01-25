using System.Collections;
using UnityEngine;

namespace Combat.Projectile.Audio
{
    [RequireComponent(typeof(Missile), typeof(AudioSource))]
    public class MissileAudio : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip launchClip;
        [SerializeField] private AudioClip engineLoopClip;
        [SerializeField] private AudioClip detonationClip;
        [SerializeField] private float engineStartDelay = 0.15f;
        [SerializeField] private float engineFadeInTime = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float launchVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float engineVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float detonationVolume = 1f;
        [SerializeField] private float minEnginePitch = 0.9f;
        [SerializeField] private float maxEnginePitch = 1.1f;

        private Missile missile;
        private AudioSource audioSource;
        private Coroutine engineCoroutine;
        private bool engineActive;

        private void Awake()
        {
            missile = GetComponent<Missile>();
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) return;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            if (missile)
            {
                missile.Launched += HandleLaunched;
                missile.OnDetonated += HandleDetonated;
            }
            ResetAudio();
        }

        private void OnDisable()
        {
            if (missile)
            {
                missile.Launched -= HandleLaunched;
                missile.OnDetonated -= HandleDetonated;
            }
            StopEngine();
            ResetAudio();
        }

        private void Update()
        {
            if (!engineActive || !audioSource || !engineLoopClip) return;
            if (audioSource.clip != engineLoopClip) return;
            audioSource.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, missile ? missile.NormalizedSpeed : 0f);
        }

        private void HandleLaunched()
        {
            if (!audioSource) return;
            if (launchClip) audioSource.PlayOneShot(launchClip, launchVolume);
            if (engineCoroutine != null) StopCoroutine(engineCoroutine);
            engineCoroutine = StartCoroutine(StartEngineLoop());
        }

        private void HandleDetonated(Vector3 position)
        {
            StopEngine();
            if (detonationClip) global::Audio.PooledAudioSource.PlayClipAtPoint(detonationClip, position, detonationVolume);
        }

        private IEnumerator StartEngineLoop()
        {
            if (!engineLoopClip || !audioSource) yield break;
            yield return new WaitForSeconds(engineStartDelay);
            if (!isActiveAndEnabled || !audioSource) yield break;

            audioSource.clip = engineLoopClip;
            audioSource.volume = 0f;
            audioSource.loop = true;
            audioSource.pitch = minEnginePitch;
            audioSource.Play();

            if (engineFadeInTime > 0f)
            {
                for (var t = 0f; t < engineFadeInTime; t += Time.deltaTime)
                {
                    audioSource.volume = Mathf.Lerp(0f, engineVolume, t / engineFadeInTime);
                    yield return null;
                }
            }

            audioSource.volume = engineVolume;
            engineActive = true;
            engineCoroutine = null;
        }

        private void StopEngine()
        {
            engineActive = false;
            if (engineCoroutine != null)
            {
                StopCoroutine(engineCoroutine);
                engineCoroutine = null;
            }
            if (!audioSource) return;
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.pitch = 1f;
        }

        private void ResetAudio()
        {
            if (!audioSource) return;
            audioSource.volume = 1f;
        }
    }
}
