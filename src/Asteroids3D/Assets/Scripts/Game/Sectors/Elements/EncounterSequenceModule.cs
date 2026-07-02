using System.Collections;
using Game.Encounters;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Behavior module that drives a Combat sector's encounter sequence: instantiate each
    /// <see cref="Encounter"/> template one at a time, advance on completion, and end the sector when
    /// the sequence finishes or fails. Relocated from <c>CombatSector</c>'s inline logic. The module
    /// knows nothing of the sector's completion sink — it only raises <see cref="SectorModule.RequestSectorEnd"/>,
    /// which the base sector auto-wires. The objective marker is driven by the Stage-3 objective-service
    /// channel (the encounter reports its target via the <c>Encounter</c> base), so the module does
    /// not touch the marker.
    /// </summary>
    public partial class EncounterSequenceModule : SectorModule
    {
        [Tooltip("Encounter templates, instantiated and run one at a time in order. NOT adopted.")]
        [SerializeField] private Encounter[] encounters;

        [Tooltip("Dragged reference to the chaser ship, handed to an ExtractionEncounter via SetChaser.")]
        [SerializeField] private Ship chaser;

        private IGameServices _services;
        private Ship _player;
        private Encounter _activeEncounter;
        private int _encounterIndex;

        /// <summary>The currently running encounter instance (test/diagnostics seam).</summary>
        public Encounter Active => _activeEncounter;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            _services = ctx.Services;
            _player = ctx.Player;

            if (encounters == null || encounters.Length == 0)
                yield break;

            yield return StartEncounter(0);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (_activeEncounter != null)
            {
                yield return _activeEncounter.Teardown();
                Destroy(_activeEncounter.gameObject);
                _activeEncounter = null;
            }

            ctx.Services?.ObjectiveService?.Clear();
        }

        private IEnumerator StartEncounter(int index)
        {
            _encounterIndex = index;
            var encounter = Instantiate(encounters[index]);
            encounter.Initialize(_services, _player);

            if (encounter is ExtractionEncounter extraction)
                extraction.SetChaser(chaser);

            encounter.OnEncounterComplete += HandleEncounterComplete;
            yield return encounter.Setup();
            _activeEncounter = encounter;
        }

        private void HandleEncounterComplete(Encounter enc, EncounterResult result)
        {
            if (result == EncounterResult.Failed)
            {
                RequestSectorEnd(SectorResult.Failed("encounter_failed"));
                return;
            }
            StartCoroutine(TransitionToNextEncounter(enc));
        }

        private IEnumerator TransitionToNextEncounter(Encounter enc)
        {
            yield return enc.Teardown();
            Destroy(enc.gameObject);
            _activeEncounter = null;

            _encounterIndex++;
            if (_encounterIndex < encounters.Length)
                yield return StartEncounter(_encounterIndex);
            else
                RequestSectorEnd(SectorResult.Extracted());
        }
    }
}
