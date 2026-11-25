using FMODUnity;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace UI.Audio
{
    /// Plays a one-shot cue each time the bound heat readout overheats.
    public class UILaserAudio : MonoBehaviour
    {
        [Tooltip("FMOD one-shot played when the laser gun overheats.")]
        [SerializeField] private EventReference overheatEvent;

        private IHeatReadout heat;

        void OnDestroy()
        {
            if (heat != null)
                heat.OnOverheat -= PlayOverheatSound;
        }

        public void Initialize(IHeatReadout heat)
        {
            if (this.heat != null)
                this.heat.OnOverheat -= PlayOverheatSound;

            this.heat = heat;
            if (this.heat == null) return;

            this.heat.OnOverheat += PlayOverheatSound;
        }

        void PlayOverheatSound() => RuntimeManager.PlayOneShot(overheatEvent);
    }
}
