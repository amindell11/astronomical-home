using Game.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Audio
{
    [RequireComponent(typeof(Missile), typeof(AudioSource))]
    public class MissileAudio : MonoBehaviour, IPresentationPart
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
        private bool engineActive;
        private bool enginePending;
        private bool engineFadingIn;
        private float engineStartTime;
        private float engineFadeElapsed;

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

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Update()
        {
            if (!audioSource || !engineLoopClip) return;

            if (enginePending)
            {
                if (Time.time < engineStartTime) return;
                StartEngineLoop();
            }

            if (engineFadingIn)
            {
                engineFadeElapsed += Time.deltaTime;
                if (engineFadeInTime > 0f)
                    audioSource.volume = Mathf.Lerp(0f, engineVolume, Mathf.Clamp01(engineFadeElapsed / engineFadeInTime));

                if (engineFadeElapsed >= engineFadeInTime)
                {
                    audioSource.volume = engineVolume;
                    engineFadingIn = false;
                    engineActive = true;
                }
            }

            if (!engineActive || audioSource.clip != engineLoopClip) return;
            audioSource.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, missile ? missile.NormalizedSpeed : 0f);
        }

        private void HandleLaunched()
        {
            if (!audioSource) return;
            if (launchClip) audioSource.PlayOneShot(launchClip, launchVolume);
            StopEngine();
            enginePending = true;
            engineStartTime = Time.time + engineStartDelay;
        }

        private void HandleDetonated(Vector3 position)
        {
            StopEngine();
            if (detonationClip) global::Audio.PooledAudioSource.PlayClipAtPoint(detonationClip, position, detonationVolume);
        }

        private void StartEngineLoop()
        {
            if (!engineLoopClip || !audioSource || !isActiveAndEnabled) return;

            audioSource.clip = engineLoopClip;
            audioSource.volume = 0f;
            audioSource.loop = true;
            audioSource.pitch = minEnginePitch;
            audioSource.Play();
            enginePending = false;
            engineFadeElapsed = 0f;
            engineFadingIn = engineFadeInTime > 0f;
            if (!engineFadingIn)
            {
                audioSource.volume = engineVolume;
                engineActive = true;
            }
        }

        private void StopEngine()
        {
            engineActive = false;
            enginePending = false;
            engineFadingIn = false;
            engineFadeElapsed = 0f;
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
