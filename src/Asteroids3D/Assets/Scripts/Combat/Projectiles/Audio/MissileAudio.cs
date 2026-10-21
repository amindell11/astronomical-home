using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Combat.Projectiles.Audio
{
    [RequireComponent(typeof(Missile))]
    public sealed class MissileAudio : MonoBehaviour, IPresentationPart
    {
        private const string SpeedParameter = "Speed";

        [Tooltip("FMOD event with a sustain point: started at launch, fed Speed every frame, stopped with fade-out.")]
        [SerializeField] private EventReference flightEvent;
        [Tooltip("FMOD one-shot played where the missile detonates.")]
        [SerializeField] private EventReference detonateEvent;

        private Missile missile;
        private EventInstance flight;

        private void Awake()
        {
            missile = GetComponent<Missile>();
        }

        private void OnEnable()
        {
            missile.Launched += BeginFlight;
            missile.OnDetonated += Detonate;
        }

        private void OnDisable()
        {
            missile.Launched -= BeginFlight;
            missile.OnDetonated -= Detonate;
            StopFlight();
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Update()
        {
            if (!flight.isValid()) return;
            flight.setParameterByName(SpeedParameter, missile.NormalizedSpeed);
        }

        // Only the Rigidbody overload gives FMOD a velocity; the plain one leaves Doppler at zero.
        private void BeginFlight()
        {
            flight = RuntimeManager.CreateInstance(flightEvent);
            RuntimeManager.AttachInstanceToGameObject(flight, gameObject, missile.rb);
            flight.setParameterByName(SpeedParameter, missile.NormalizedSpeed);
            flight.start();
        }

        private void Detonate(Vector3 position)
        {
            StopFlight();
            RuntimeManager.PlayOneShot(detonateEvent, position);
        }

        // Detached first: the fade-out stays where the missile vanished, not on the pooled object.
        private void StopFlight()
        {
            if (!flight.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(flight);
            flight.stop(STOP_MODE.ALLOWFADEOUT);
            flight.release();
            flight = default;
        }
    }
}
