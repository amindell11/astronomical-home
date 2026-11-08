using FMOD.Studio;
using FMODUnity;
using Ships.Keystones;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Ships.Audio
{
    /// <summary>
    /// Each activation starts the kit's event on the ship, and the end of the kit's active window keys
    /// it off, so an event with a sustain point holds for the window and one without plays through.
    /// Rides the kit prefab, so the ship's presentation policy reaches it.
    /// </summary>
    public sealed class KeystoneAudio : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;

        [Tooltip("FMOD event started on the ship at each activation, keyed off when the kit's active window ends.")]
        [SerializeField] private EventReference activationEvent;

        private int heardActivations;
        private bool wasActive;
        private EventInstance instance;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable()
        {
            heardActivations = keystone.Activations;
            wasActive = keystone.Active;
        }

        private void OnDisable()
        {
            if (!instance.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(instance);
            instance.stop(STOP_MODE.IMMEDIATE);
            Forget();
        }

        private void LateUpdate()
        {
            if (keystone.Activations > heardActivations)
                Begin();
            heardActivations = keystone.Activations;

            if (wasActive && !keystone.Active)
                KeyOff();
            wasActive = keystone.Active;
        }

        private void Begin()
        {
            if (instance.isValid())
                Forget();
            instance = RuntimeManager.CreateInstance(activationEvent);
            RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
            instance.start();
        }

        private void KeyOff()
        {
            if (!instance.isValid()) return;
            instance.keyOff();
            Forget();
        }

        private void Forget()
        {
            instance.release();
            instance = default;
        }
    }
}
