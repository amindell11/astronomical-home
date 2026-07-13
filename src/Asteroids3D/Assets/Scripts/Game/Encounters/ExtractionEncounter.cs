using System;
using System.Collections;
using System.Collections.Generic;
using Objectives;
using Objectives.States;
using Ships;
using UnityEngine;

namespace Game.Encounters
{
    public class ExtractionEncounter : Encounter
    {
        [SerializeField] private ExtractionZone extractionZonePrefab;
        [SerializeField] private Vector2 extractionZonePosition = new Vector2(50f, 50f);

        private ExtractionZone extractionZoneInstance;
        private Ship chaser;

        public override Transform ObjectiveTarget =>
            extractionZoneInstance ? extractionZoneInstance.transform : null;

        public void SetChaser(Ship chaser) { this.chaser = chaser; }

        protected override IEnumerator OnSetup()
        {
            if (extractionZonePrefab)
            {
                extractionZoneInstance = Instantiate(
                    extractionZonePrefab,
                    GamePlane.PlanePointToWorld(extractionZonePosition),
                    extractionZonePrefab.transform.rotation);
                extractionZoneInstance.Initialize(chaser ? chaser.transform : null);
            }

            if (chaser) chaser.gameObject.SetActive(true);

            // Physics needs a frame to process the new collider so OnTriggerEnter fires if the player already overlaps the zone.
            yield return null;

            var mission = new MissionDefinition(
                "extraction",
                new Dictionary<string, string>
                {
                    { "extraction", "completed" }
                });

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["extraction"] = () => new ExtractionChallengeState(extractionZoneInstance),
                ["completed"] = () => new CompletedState(
                    onEnter: () => CompleteEncounter(EncounterResult.Completed)),
                ["failed"] = () => new FailedState(
                    onEnter: () => CompleteEncounter(EncounterResult.Failed))
            };

            Services.ObjectiveService.SetSpineObjective(mission, builders);
        }

        protected override void OnFail()
        {
            Services.ObjectiveService.FailSpine();
        }

        protected override IEnumerator OnTeardown()
        {
            Services.ObjectiveService.ClearSpine();
            if (extractionZoneInstance) Destroy(extractionZoneInstance.gameObject);
            if (chaser) chaser.gameObject.SetActive(false);
            extractionZoneInstance = null;
            yield break;
        }
    }
}
