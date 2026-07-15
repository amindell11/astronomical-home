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
        public const string StepExplore = "explore";
        public const string StepKeyAcquired = "key-acquired";
        public const string StepReadyToExtract = "ready-to-extract";
        public const string StepCompleted = "completed";
        public const string StepFailed = "failed";

        [SerializeField] private KeyPickup keyPickup;
        [SerializeField] private ExtractionZone extractionZone;
        [SerializeField] private SignalPort explore;
        [SerializeField] private SignalPort keyAcquired;
        [SerializeField] private SignalPort readyToExtract;
        [SerializeField] private SignalPort completed;
        [SerializeField] private SignalPort failed;

        private IObjectiveService objectives;
        private SpineObjectiveHandle spine;
        private SectorEventBus bus;
        private Vector3? keyHome;

        public IEnumerable<(string Step, SignalPort Port)> StepPorts
        {
            get
            {
                yield return (StepExplore, explore);
                yield return (StepKeyAcquired, keyAcquired);
                yield return (StepReadyToExtract, readyToExtract);
                yield return (StepCompleted, completed);
                yield return (StepFailed, failed);
            }
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!keyPickup || !extractionZone)
            {
                Debug.LogError($"SectorSpineModule on '{name}' is missing a fixture reference — spine is inert.", this);
                yield break;
            }
            foreach (var (step, port) in StepPorts)
                if (!SignalPortGuards.ValidPortRef(this, port, step, ctx.Sector))
                    yield break;

            objectives = ctx.Services.ObjectiveService;
            bus = ctx.Bus;

            var playerBody = ctx.Player ? ctx.Player.Body : null;
            keyPickup.Initialize(playerBody);
            extractionZone.BindPlayer(playerBody);

            keyHome ??= keyPickup.transform.position;
            keyPickup.SpawnKey(keyHome.Value);

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
            bus?.Latch(PortFor(step));
            if (spine != null) spine.Target = TargetFor(step);
        }

        private SignalPort PortFor(string step)
        {
            switch (step)
            {
                case StepExplore: return explore;
                case StepKeyAcquired: return keyAcquired;
                case StepReadyToExtract: return readyToExtract;
                case StepCompleted: return completed;
                case StepFailed: return failed;
                default:
                    Debug.LogError($"SectorSpineModule on '{name}' has no port for unknown spine step '{step}'.", this);
                    return null;
            }
        }

        private Transform TargetFor(string step) => step switch
        {
            StepExplore or StepKeyAcquired => keyPickup ? keyPickup.transform : null,
            StepReadyToExtract => extractionZone ? extractionZone.transform : null,
            _ => null
        };

#if UNITY_EDITOR
        internal void Bind(KeyPickup key, ExtractionZone zone,
            SignalPort explore = null, SignalPort keyAcquired = null, SignalPort readyToExtract = null,
            SignalPort completed = null, SignalPort failed = null)
        {
            keyPickup = key;
            extractionZone = zone;
            this.explore = explore;
            this.keyAcquired = keyAcquired;
            this.readyToExtract = readyToExtract;
            this.completed = completed;
            this.failed = failed;
        }
#endif
    }
}
