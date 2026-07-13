using System;
using System.Collections;
using System.Collections.Generic;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Game.Encounters
{
    public class KeyPickupEncounter : Encounter
    {
        [SerializeField] private KeyPickup keyPickupPrefab;
        [SerializeField] private Vector2 keySpawnPosition;

        private KeyPickup keyPickupInstance;

        public override Transform ObjectiveTarget =>
            keyPickupInstance ? keyPickupInstance.transform : null;

        protected override IEnumerator OnSetup()
        {
            if (keyPickupPrefab)
            {
                var keyWorld = Services.Arena.Place(keySpawnPosition);
                // Parented under the encounter so the spawn dies with its owner even when Teardown never runs.
                keyPickupInstance = Instantiate(keyPickupPrefab, keyWorld, keyPickupPrefab.transform.rotation, transform);
                keyPickupInstance.SpawnKey(keyWorld);
            }

            var mission = new MissionDefinition(
                "explore",
                new Dictionary<string, string>
                {
                    { "explore", "key" },
                    { "key", "completed" }
                });

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(keyPickupInstance),
                ["key"] = () => new KeyAcquiredState(),
                ["completed"] = () => new CompletedState(
                    onEnter: () => CompleteEncounter(EncounterResult.Completed)),
                ["failed"] = () => new FailedState(
                    onEnter: () => CompleteEncounter(EncounterResult.Failed))
            };

            Services.ObjectiveService.SetObjective(mission, builders);

            yield break;
        }

        protected override void OnFail()
        {
            Services.ObjectiveService.Fail();
        }

        protected override IEnumerator OnTeardown()
        {
            Services.ObjectiveService.Clear();
            if (keyPickupInstance) Destroy(keyPickupInstance.gameObject);
            keyPickupInstance = null;
            yield break;
        }
    }
}
