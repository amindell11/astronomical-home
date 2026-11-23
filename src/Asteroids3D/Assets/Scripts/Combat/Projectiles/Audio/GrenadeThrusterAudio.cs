using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Combat.Projectiles.Audio
{
    [RequireComponent(typeof(Grenade))]
    public sealed class GrenadeThrusterAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD event with a sustain point: started when the braking burn lights, stopped with fade-out when it ends.")]
        [SerializeField] private EventReference thrusterEvent;

        private Grenade charge;
        private EventInstance thruster;

        private void Awake()
        {
            charge = GetComponent<Grenade>();
        }

        private void OnDisable() => StopThruster();

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Update()
        {
            if (charge.Burning && !thruster.isValid())
                StartThruster();
            else if (!charge.Burning && thruster.isValid())
                StopThruster();
        }

        private void StartThruster()
        {
            thruster = RuntimeManager.CreateInstance(thrusterEvent);
            // Only the Rigidbody overload sends FMOD the body's velocity, which Doppler needs.
            RuntimeManager.AttachInstanceToGameObject(thruster, gameObject, charge.rb);
            thruster.start();
        }

        // Detached first: the fade-out stays where the charge vanished, not on the pooled object.
        private void StopThruster()
        {
            if (!thruster.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(thruster);
            thruster.stop(STOP_MODE.ALLOWFADEOUT);
            thruster.release();
            thruster = default;
        }
    }
}
