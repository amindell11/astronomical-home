using System;
using System.Collections;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Encounters
{
    public enum EncounterResult { Completed, Failed }
    public enum EncounterPhase { Inactive, SettingUp, Running, TearingDown, Disposed }

    public abstract class Encounter : MonoBehaviour
    {
        public event Action<Encounter, EncounterResult> OnEncounterComplete;
        public EncounterPhase Phase { get; private set; } = EncounterPhase.Inactive;

        protected IGameServices Services { get; private set; }
        protected Ship Player { get; private set; }

        public void Initialize(IGameServices services, Ship player)
        {
            Services = services;
            Player = player;
        }

        public IEnumerator Setup()
        {
            Phase = EncounterPhase.SettingUp;
            yield return OnSetup();
            Phase = EncounterPhase.Running;

            // Report this encounter's objective target to the service channel. The objective state
            // determines which transform ObjectiveTarget returns, so re-report on every state change.
            // UI (the minimap marker) reads CurrentTarget and self-decides visibility by CurrentState.
            if (Services?.ObjectiveService != null)
            {
                Services.ObjectiveService.OnStateChanged += HandleObjectiveStateChanged;
                Services.ObjectiveService.SetTarget(ObjectiveTarget);
            }
        }

        public IEnumerator Teardown()
        {
            Phase = EncounterPhase.TearingDown;
            if (Services?.ObjectiveService != null)
            {
                Services.ObjectiveService.OnStateChanged -= HandleObjectiveStateChanged;
                Services.ObjectiveService.SetTarget(null);
            }
            yield return OnTeardown();
            Phase = EncounterPhase.Disposed;
        }

        private void HandleObjectiveStateChanged(Objectives.ObjectiveType from, Objectives.ObjectiveType to)
            => Services?.ObjectiveService?.SetTarget(ObjectiveTarget);

        public void Fail()
        {
            if (Phase == EncounterPhase.Running)
                OnFail();
        }

        public abstract Transform ObjectiveTarget { get; }

        protected abstract IEnumerator OnSetup();
        protected abstract IEnumerator OnTeardown();
        protected virtual void OnFail() { }

        protected void CompleteEncounter(EncounterResult result)
        {
            if (Phase != EncounterPhase.Running) return;
            Phase = EncounterPhase.TearingDown;
            OnEncounterComplete?.Invoke(this, result);
        }
    }
}
