using System;
using System.Collections;
using System.Collections.Generic;
using Game.Services;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Game.Sectors
{
    public class SectorSpineModule : SectorModule
    {
        public const string TokenPrefix = "spine:";
        public const string StepExplore = "explore";
        public const string StepKeyAcquired = "key-acquired";
        public const string StepReadyToExtract = "ready-to-extract";
        public const string StepCompleted = "completed";
        public const string StepFailed = "failed";

        [SerializeField] private KeyPickup keyPickup;
        [SerializeField] private ExtractionZone extractionZone;

        private IObjectiveService objectives;
        private SpineObjectiveHandle spine;
        private SectorEventBus bus;
        private Vector3 keyHome;
        private bool keyHomeCaptured;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!keyPickup || !extractionZone)
            {
                Debug.LogError($"SectorSpineModule on '{name}' is missing a fixture reference — spine is inert.", this);
                yield break;
            }

            objectives = ctx.Services.ObjectiveService;
            bus = ctx.Bus;

            var playerBody = ctx.Player ? ctx.Player.Body : null;
            keyPickup.Initialize(playerBody);
            extractionZone.BindPlayer(playerBody);

            if (!keyHomeCaptured)
            {
                keyHome = keyPickup.transform.position;
                keyHomeCaptured = true;
            }
            keyPickup.SpawnKey(keyHome);

            var mission = new MissionDefinition(
                StepExplore,
                new Dictionary<string, string>
                {
                    { StepExplore, StepKeyAcquired },
                    { StepKeyAcquired, StepReadyToExtract },
                    { StepReadyToExtract, StepCompleted }
                });

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                [StepExplore] = () => new ExploreState(keyPickup),
                [StepKeyAcquired] = () => new KeyAcquiredState(),
                [StepReadyToExtract] = () => new ExtractionChallengeState(extractionZone),
                [StepCompleted] = () => new CompletedState(
                    onEnter: () => RequestSectorEnd(SectorResult.Extracted())),
                [StepFailed] = () => new FailedState(
                    onEnter: () => RequestSectorEnd(SectorResult.Failed("spine_failed")))
            };

            // Subscribe before install so the initial step is published like every later one.
            objectives.OnSpineStepChanged += HandleSpineStepChanged;
            spine = objectives.SetSpineObjective(mission, builders, TargetFor(StepExplore));
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            Unbind(closeSpine: true);
            yield break;
        }

        // Session sweeps destroy sectors without running Teardown; a dead module must not stay subscribed.
        private void OnDestroy() => Unbind(closeSpine: false);

        private void Unbind(bool closeSpine)
        {
            if (objectives == null) return;
            objectives.OnSpineStepChanged -= HandleSpineStepChanged;
            if (closeSpine) spine?.Close();
            spine = null;
            objectives = null;
            bus = null;
        }

        private void HandleSpineStepChanged(string step)
        {
            bus?.Latch(TokenPrefix + step);
            if (spine != null) spine.Target = TargetFor(step);
        }

        private Transform TargetFor(string step) => step switch
        {
            StepExplore or StepKeyAcquired => keyPickup ? keyPickup.transform : null,
            StepReadyToExtract => extractionZone ? extractionZone.transform : null,
            _ => null
        };

#if UNITY_EDITOR
        internal void Bind(KeyPickup key, ExtractionZone zone)
        {
            keyPickup = key;
            extractionZone = zone;
        }
#endif
    }
}
