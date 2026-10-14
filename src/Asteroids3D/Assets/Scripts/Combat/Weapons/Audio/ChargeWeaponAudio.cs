using Combat.Weapons.Conditions;
using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Combat.Weapons.Audio
{
    /// <summary>
    /// A charge weapon's sustained sound: one instance of an event with a sustain point, started
    /// when the charge-up rises from zero, fed the charge-up level through <c>Charge</c>, keyed off
    /// into its release on the shot and faded out on any other return to zero. The instance rides
    /// the weapon so it follows the ship. The sound announces nothing: <see cref="ChargeTime"/>
    /// tells fire from drop by raising <see cref="ChargeTime.OnChargeSpent"/> before it zeroes.
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

        // The parameter already holds the spent level; the attached release tail follows the ship until it ends.
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
