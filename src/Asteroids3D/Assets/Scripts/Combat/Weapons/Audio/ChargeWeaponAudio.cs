using Combat.Weapons.Conditions;
using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Combat.Weapons.Audio
{
    /// <summary>
    /// A charge weapon's sustained sound. A shot differs from a drop by order alone: the shot's
    /// <see cref="ChargeTime.OnChargeSpent"/> keys the instance off and forgets it before the
    /// charge-up zeroes, so that zero stops only an unfired charge-up.
    /// </summary>
    [RequireComponent(typeof(ChargeTime))]
    public sealed class ChargeWeaponAudio : MonoBehaviour, IPresentationPart
    {
        private const string ChargeParameter = "Charge";

        [Tooltip("FMOD event with a sustain point: started at charge-up, keyed off on the shot.")]
        [SerializeField] private EventReference fireEvent;

        private ChargeTime charge;
        private EventInstance instance;

        private void Awake()
        {
            charge = GetComponent<ChargeTime>();
        }

        private void OnEnable()
        {
            charge.OnChargeChanged += FollowCharge;
            charge.OnChargeSpent += KeyOff;
        }

        private void OnDisable()
        {
            charge.OnChargeChanged -= FollowCharge;
            charge.OnChargeSpent -= KeyOff;
            Stop(STOP_MODE.IMMEDIATE);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void FollowCharge(float level)
        {
            if (level <= 0f)
            {
                Stop(STOP_MODE.ALLOWFADEOUT);
                return;
            }

            if (!instance.isValid())
                Begin();
            instance.setParameterByName(ChargeParameter, level);
        }

        private void Begin()
        {
            instance = RuntimeManager.CreateInstance(fireEvent);
            RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
            instance.start();
        }

        // Charge already holds the spent level; left attached, the release tail follows the ship.
        private void KeyOff(float spent)
        {
            if (!instance.isValid()) return;
            instance.keyOff();
            Forget();
        }

        private void Stop(STOP_MODE mode)
        {
            if (!instance.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(instance);
            instance.stop(mode);
            Forget();
        }

        private void Forget()
        {
            instance.release();
            instance = default;
        }
    }
}
