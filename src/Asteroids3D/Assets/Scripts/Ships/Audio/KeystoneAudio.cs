using FMOD.Studio;
using FMODUnity;
using Ships.Keystones;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Ships.Audio
{
    /// <summary>
    /// The activation event starts on the ship at activation, or at launch for a kit that holds the
    /// throttle first, and keys off when the kit's active window ends. The optional hold event covers
    /// the throttle hold and keys off when the kit lets go. Rides the kit prefab, so the ship's
    /// presentation policy reaches it.
    /// </summary>
    public sealed class KeystoneAudio : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;

        [Tooltip("FMOD event started on the ship at activation, or at launch when the kit holds the throttle first, keyed off when the active window ends.")]
        [SerializeField] private EventReference activationEvent;

        [Tooltip("FMOD event played while the kit's autopilot holds the throttle, keyed off when it lets go. May be empty.")]
        [SerializeField] private EventReference holdEvent;

        private int heardActivations;
        private bool pending;
        private EventInstance instance;
        private EventInstance holdInstance;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable()
        {
            heardActivations = keystone.Activations;
            pending = false;
        }

        private void OnDisable()
        {
            Stop(ref instance);
            Stop(ref holdInstance);
            pending = false;
        }

        private void LateUpdate()
        {
            if (keystone.Activations > heardActivations)
                pending = true;
            heardActivations = keystone.Activations;

            if (pending && !keystone.HoldsThrottle)
            {
                Begin();
                pending = false;
            }

            if (!keystone.Active && !pending)
                KeyOff(ref instance);

            if (keystone.HoldsThrottle && !holdInstance.isValid() && !holdEvent.IsNull)
                holdInstance = Play(holdEvent);
            else if (!keystone.HoldsThrottle)
                KeyOff(ref holdInstance);
        }

        private void Begin()
        {
            if (instance.isValid())
                Forget(ref instance);
            instance = Play(activationEvent);
        }

        private EventInstance Play(EventReference sound)
        {
            var playing = RuntimeManager.CreateInstance(sound);
            RuntimeManager.AttachInstanceToGameObject(playing, gameObject, keystone.Body);
            playing.start();
            return playing;
        }

        private static void KeyOff(ref EventInstance playing)
        {
            if (!playing.isValid()) return;
            playing.keyOff();
            Forget(ref playing);
        }

        private static void Stop(ref EventInstance playing)
        {
            if (!playing.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(playing);
            playing.stop(STOP_MODE.IMMEDIATE);
            Forget(ref playing);
        }

        private static void Forget(ref EventInstance playing)
        {
            playing.release();
            playing = default;
        }
    }
}
