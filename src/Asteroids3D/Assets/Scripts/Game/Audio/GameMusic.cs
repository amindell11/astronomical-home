using FMOD.Studio;
using FMODUnity;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Game.Audio
{
    /// <summary>
    /// The run's music: one Studio event held for the session, told the run's state and the active
    /// sector's survive phase. Studio owns every transition between states. <see cref="GameHost"/>'s
    /// run loop sets the state, except extraction, which follows the sector spine's step.
    /// </summary>
    public sealed class GameMusic : MonoBehaviour
    {
        // Values are the indices of the MusicState labels in Studio.
        public enum State { Hangar = 0, Combat = 1, Extract = 2, Recap = 3, Victory = 4, Defeat = 5 }

        private const string MusicStateParameter = "MusicState";
        private const string PhaseParameter = "Phase";

        [Tooltip("Looping 2D FMOD event fed the run's MusicState and the survive phase's index as Phase.")]
        [SerializeField] private EventReference musicEvent;

        private IObjectiveService objectives;
        private PhaseSpineModule phases;
        private EventInstance music;

        public void Initialize(IObjectiveService objectiveService)
        {
            objectives = objectiveService;
            objectives.OnSpineStepChanged += OnSpineStepChanged;
            music = RuntimeManager.CreateInstance(musicEvent);
            music.start();
        }

        public void Set(State state) => music.setParameterByName(MusicStateParameter, (float)state);

        /// <summary>Combat, with Phase following the sector's survive phases when it has them.</summary>
        public void EnterSector(Sector sector)
        {
            phases = sector.Module<PhaseSpineModule>();
            Set(State.Combat);
        }

        private void Update()
        {
            if (phases) music.setParameterByName(PhaseParameter, phases.Phase.Index);
        }

        private void OnSpineStepChanged(string step)
        {
            if (step == PhaseSpineModule.StepExtract) Set(State.Extract);
        }

        private void OnDestroy()
        {
            objectives.OnSpineStepChanged -= OnSpineStepChanged;
            music.stop(STOP_MODE.ALLOWFADEOUT);
            music.release();
        }
    }
}
