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

            // The spine state decides which transform ObjectiveTarget returns, so re-report on every state change.
            if (Services?.ObjectiveService != null)
            {
                Services.ObjectiveService.OnSpineStateChanged += HandleSpineStateChanged;
                Services.ObjectiveService.SetSpineTarget(ObjectiveTarget);
            }
        }

        public IEnumerator Teardown()
        {
            Phase = EncounterPhase.TearingDown;
            if (Services?.ObjectiveService != null)
            {
                Services.ObjectiveService.OnSpineStateChanged -= HandleSpineStateChanged;
                Services.ObjectiveService.SetSpineTarget(null);
            }
            yield return OnTeardown();
            Phase = EncounterPhase.Disposed;
        }

        private void HandleSpineStateChanged(Objectives.ObjectiveType from, Objectives.ObjectiveType to)
            => Services?.ObjectiveService?.SetSpineTarget(ObjectiveTarget);

        // Session sweeps destroy sectors without running Teardown; a dead encounter must not stay subscribed.
        private void OnDestroy()
        {
            if (Services?.ObjectiveService != null)
                Services.ObjectiveService.OnSpineStateChanged -= HandleSpineStateChanged;
        }

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
