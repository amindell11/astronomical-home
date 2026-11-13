using FMOD.Studio;
using FMODUnity;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Locales;
using Substrate.Services.Objectives;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Game.Audio
{
    /// <summary>
    /// The run's music: one Studio event held for the session, told the run's state, the active
    /// sector's locale and survive phase. Studio owns every transition between states. <see cref="GameHost"/>'s
    /// run loop sets the state, except the elite fight and extraction, which follow the sector spine's step;
    /// extraction after an elite stays on Elite, so its cue carries on without restarting.
    /// </summary>
    public sealed class GameMusic : MonoBehaviour
    {
        // Values are the indices of the MusicState labels in Studio.
        public enum State { Hangar = 0, Combat = 1, Extract = 2, Recap = 3, Victory = 4, Defeat = 5, Elite = 6 }

        private const string MusicStateParameter = "MusicState";
        private const string PhaseParameter = "Phase";
        private const string LocaleParameter = "Locale";

        [Tooltip("Looping 2D FMOD event fed the run's MusicState, the sector's locale as the Locale label, and the survive phase's index as Phase.")]
        [SerializeField] private EventReference musicEvent;

        private IObjectiveService objectives;
        private PhaseSpineModule phases;
        private EventInstance music;
        private string spineStep;

        public void Initialize(IObjectiveService objectiveService)
        {
            objectives = objectiveService;
            objectives.OnSpineStepChanged += OnSpineStepChanged;
            music = RuntimeManager.CreateInstance(musicEvent);
            music.start();
        }

        public void Set(State state) => music.setParameterByName(MusicStateParameter, (float)state);

        /// <summary>Combat, with Locale the sector's locale and Phase following its survive phases, if any.</summary>
        public void EnterSector(Sector sector, LocaleSky locale)
        {
            phases = sector.Module<PhaseSpineModule>();
            music.setParameterByNameWithLabel(LocaleParameter, locale.name);
            Set(State.Combat);
        }

        private void Update()
        {
            if (phases) music.setParameterByName(PhaseParameter, phases.Phase.Index);
        }

        private void OnSpineStepChanged(string step)
        {
            if (step == PhaseSpineModule.StepElite) Set(State.Elite);
            else if (step == PhaseSpineModule.StepExtract && spineStep != PhaseSpineModule.StepElite) Set(State.Extract);
            spineStep = step;
        }

        private void OnDestroy()
        {
            objectives.OnSpineStepChanged -= OnSpineStepChanged;
            music.stop(STOP_MODE.ALLOWFADEOUT);
            music.release();
        }
    }
}
