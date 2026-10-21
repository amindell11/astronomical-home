using FMOD.Studio;
using FMODUnity;
using Ships.Command;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace UI.Audio
{
    /// Holds a warning while any missile tracks the player. Polls the bound IShipStatus.
    public sealed class UIMissileWarningAudio : MonoBehaviour
    {
        [Tooltip("Looping FMOD event held while at least one missile tracks the player.")]
        [SerializeField] private EventReference incomingMissileEvent;

        private IShipStatus status;
        private EventInstance warning;

        public void Initialize(IShipStatus status)
        {
            Stop();
            this.status = status;
        }

        private void OnDisable() => Stop();

        private void Update()
        {
            if (status == null) return;

            // Death leaves trackers registered, so the warning also needs a living player.
            var tracked = status.HealthPct > 0f && status.TrackingMissiles.Count > 0;
            if (tracked && !warning.isValid())
                Begin();
            else if (!tracked)
                Stop();
        }

        private void Begin()
        {
            warning = RuntimeManager.CreateInstance(incomingMissileEvent);
            warning.start();
        }

        private void Stop()
        {
            if (!warning.isValid()) return;
            warning.stop(STOP_MODE.ALLOWFADEOUT);
            warning.release();
            warning = default;
        }
    }
}
