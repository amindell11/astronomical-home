using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Combat.Projectiles.Audio
{
    [RequireComponent(typeof(Grenade))]
    public sealed class GrenadeFlightAudio : MonoBehaviour, IPresentationPart
    {
        private const string SpeedParameter = "Speed";

        [Tooltip("FMOD event with a sustain point: started at launch, fed Speed every frame, stopped with fade-out when the charge leaves play.")]
        [SerializeField] private EventReference flightEvent;
        [Tooltip("FMOD event with a sustain point: started when the braking burn lights, stopped with fade-out when it ends.")]
        [SerializeField] private EventReference thrusterEvent;

        private Grenade charge;
        private EventInstance flight;
        private EventInstance thruster;

        private float NormalizedSpeed => Mathf.Clamp01(charge.rb.linearVelocity.magnitude / charge.LaunchSpeed);

        private void Awake()
        {
            charge = GetComponent<Grenade>();
        }

        private void OnEnable() => charge.Launched += BeginFlight;

        private void OnDisable()
        {
            charge.Launched -= BeginFlight;
            Stop(ref flight);
            Stop(ref thruster);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Update()
        {
            if (flight.isValid())
                flight.setParameterByName(SpeedParameter, NormalizedSpeed);

            if (charge.Burning && !thruster.isValid())
                thruster = Play(thrusterEvent);
            else if (!charge.Burning)
                Stop(ref thruster);
        }

        private void BeginFlight()
        {
            flight = Play(flightEvent);
            flight.setParameterByName(SpeedParameter, NormalizedSpeed);
        }

        private EventInstance Play(EventReference reference)
        {
            var instance = RuntimeManager.CreateInstance(reference);
            // Only the Rigidbody overload sends FMOD the body's velocity, which Doppler needs.
            RuntimeManager.AttachInstanceToGameObject(instance, gameObject, charge.rb);
            instance.start();
            return instance;
        }

        // Detached first: the fade-out stays where the charge vanished, not on the pooled object.
        private static void Stop(ref EventInstance instance)
        {
            if (!instance.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(instance);
            instance.stop(STOP_MODE.ALLOWFADEOUT);
            instance.release();
            instance = default;
        }
    }
}
