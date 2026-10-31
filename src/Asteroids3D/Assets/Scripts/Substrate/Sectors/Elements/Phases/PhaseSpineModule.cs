using System;
using System.Collections;
using System.Collections.Generic;
using Objectives;
using Objectives.Presentation;
using Objectives.States;
using Ships;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// A sector spine over its wave director's sector phases: survive them, then reach
    /// the extraction zone, which stays hidden until the phases are done and then appears on a clear
    /// point away from the hero. The hero's jump request inside commits extraction. Shortly before a
    /// sector phase that a lull follows ends, a lull beacon opens on a clear point away from the hero
    /// the same way and stays through the lull and a grace window into the next phase; flying into it
    /// claims that lull's pick (<see cref="BeaconClaimed"/>, read by the host), and an unclaimed one
    /// closes when its grace runs out or the next beacon opens. The director is held to observe its
    /// phase, never commanded.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1051
    /// </summary>
    public class PhaseSpineModule : SectorModule
    {
        public const string StepSurvive = "survive";
        public const string StepExtract = "extract";
        public const string StepCompleted = "completed";

        [SerializeField] private WaveDirector director;
        [SerializeField] private ExtractionZone extractionZone;

        [Header("Zone placement")]
        [Tooltip("Plane distance from the hero the zone appears at when extraction opens.")]
        [SerializeField, Min(0f)] private float zoneDistance = 150f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float zoneClearance = 35f;
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random angles tried for a clear point; the last one tried is used when none is clear.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        [Header("Lull beacon")]
        [SerializeField] private ExtractionZone beaconZone;
        [Tooltip("Plane distance from the hero a lull beacon appears at.")]
        [SerializeField, Min(0f)] private float beaconDistance = 150f;
        [Tooltip("Seconds before a sector phase ends that its lull's beacon appears, so the marker shows during the fight.")]
        [SerializeField, Min(0f)] private float beaconLeadSeconds = 8f;
        [Tooltip("Seconds into the next sector phase an unclaimed beacon stays before it closes.")]
        [SerializeField, Min(0f)] private float beaconGraceSeconds = 10f;

        private SpineObjectiveHandle spine;
        private Ship hero;
        private ExtractionJumpState extraction;
        private bool beaconsEnabled;
        private bool beaconOpen;
        // The last beacon's lull, kept after it closes so a claimed lull never reopens.
        private int beaconLull;

        public SectorPhaseMark Phase => director.Phase;

        /// <summary>True from a lull beacon's claim until the next opens; the host picks on its rising edge.</summary>
        public bool BeaconClaimed { get; private set; }

        /// <summary>What this sector's director can send, readable off the sector prefab before it loads.</summary>
        public SectorThreat Threat() => director.Threat();

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!director || !extractionZone || !beaconZone)
                throw new InvalidOperationException(
                    $"PhaseSpineModule on '{name}' needs its wave director, extraction zone and beacon zone wired.");

            hero = ctx.Hero;
            extractionZone.BindPlayer(hero ? hero.Body : null);
            extractionZone.gameObject.SetActive(false);
            beaconZone.BindPlayer(hero ? hero.Body : null);
            beaconZone.gameObject.SetActive(false);
            beaconsEnabled = ctx.PresentationEnabled && hero;
            beaconLull = -1;
            beaconOpen = false;
            BeaconClaimed = false;
            extraction = new ExtractionJumpState(extractionZone, hero,
                () => RequestSectorEnd(SectorResult.Extracted(extractionZone.DepartureHeading)), OpenExtraction);

            var mission = new MissionDefinition(StepSurvive, new Dictionary<string, string>
            {
                { StepSurvive, StepExtract },
                { StepExtract, StepCompleted }
            });
            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                [StepSurvive] = () => new SurviveState(director),
                [StepExtract] = () => extraction,
                [StepCompleted] = () => new CompletedState()
            };
            spine = ctx.Objectives.SetSpineObjective(mission, builders, phaseProgress: ReadPhaseProgress);
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ReleaseExtraction();
            if (extractionZone) extractionZone.Disarm();
            beaconsEnabled = false;
            if (beaconOpen) CloseBeacon();
            spine?.Close();
            spine = null;
            hero = null;
            yield break;
        }

        private void ReleaseExtraction()
        {
            extraction?.Dispose();
            extraction = null;
        }

        private void OnDestroy() => ReleaseExtraction();

        private void Update() => Tick();

        internal void Tick()
        {
            if (!beaconsEnabled || !hero) return;
            var mark = director.Phase;
            var lull = LullAhead(mark);
            if (lull >= 0 && lull != beaconLull)
            {
                if (beaconOpen) CloseBeacon();
                OpenBeacon(lull);
            }
            else if (beaconOpen && beaconZone.IsPlayerInZone) ClaimBeacon();
            else if (beaconOpen && BeaconExpired(mark)) CloseBeacon();
        }

        // The lull in progress, or the one within the lead of this phase's end.
        private int LullAhead(SectorPhaseMark mark)
        {
            if (mark.Done || mark.InOpeningLull) return -1;
            if (mark.InLull) return mark.Index;
            var lullFollows = mark.Index < director.PhaseCount - 1;
            return lullFollows && mark.SecondsLeft <= beaconLeadSeconds ? mark.Index + 1 : -1;
        }

        private bool BeaconExpired(SectorPhaseMark mark) =>
            mark.Done || !mark.InLull && (mark.Index > beaconLull
                || mark.Index == beaconLull && mark.ElapsedSeconds >= beaconGraceSeconds);

        private void OpenBeacon(int lull)
        {
            beaconZone.transform.position = ClearPointAround(GamePlane.WorldPointToPlane(hero.transform.position), beaconDistance);
            beaconZone.gameObject.SetActive(true);
            beaconZone.Arm();
            spine.Target = beaconZone.transform;
            beaconOpen = true;
            beaconLull = lull;
            BeaconClaimed = false;
        }

        private void ClaimBeacon()
        {
            BeaconClaimed = true;
            CloseBeacon();
        }

        private void CloseBeacon()
        {
            beaconOpen = false;
            beaconZone.Disarm();
            beaconZone.gameObject.SetActive(false);
            // Extraction may already hold the marker; the beacon clears only its own.
            if (spine.Target == beaconZone.transform) spine.Target = null;
        }

        private SectorPhaseProgress ReadPhaseProgress()
        {
            var mark = director.Phase;
            return new SectorPhaseProgress(mark.InLull ? mark.Index - 1 : mark.Index,
                director.PhaseCount, mark.CountdownSeconds, mark.Done);
        }

        private void OpenExtraction()
        {
            if (hero)
                extractionZone.transform.position = ClearPointAround(GamePlane.WorldPointToPlane(hero.transform.position), zoneDistance);
            extractionZone.gameObject.SetActive(true);
            extractionZone.Arm();
            spine.Target = extractionZone.transform;
        }

        private Vector3 ClearPointAround(Vector2 center, float distance)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                position = GamePlane.PlanePointToWorld(center + outward * distance);
                if (!Physics.CheckSphere(position, zoneClearance, asteroidMask)) break;
            }
            return position;
        }

        private static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private sealed class SurviveState : ObjectiveState
        {
            private readonly WaveDirector director;
            private string line;
            private int shownIndex = -1;
            private int shownSeconds = -1;
            private bool shownLull;

            public SurviveState(WaveDirector director) => this.director = director;

            public override ObjectiveType StateType => ObjectiveType.Survive;

            public override void Tick(float deltaTime) { }

            public override bool IsComplete => director.Phase.Done;

            // Rebuilt only when the phase or the shown second changes; the HUD polls it every frame.
            public override string StatusLine
            {
                get
                {
                    var mark = director.Phase;
                    var seconds = Mathf.CeilToInt(mark.SecondsLeft);
                    if (mark.Index == shownIndex && mark.InLull == shownLull && seconds == shownSeconds) return line;

                    shownIndex = mark.Index;
                    shownLull = mark.InLull;
                    shownSeconds = seconds;
                    var phase = $"PHASE {mark.Index + 1}/{director.PhaseCount}";
                    line = mark.InLull ? $"{phase} IN {Clock(seconds)}" : $"{phase}    {Clock(seconds)}";
                    return line;
                }
            }
        }

#if UNITY_EDITOR
        internal void Bind(WaveDirector director, ExtractionZone zone, ExtractionZone beacon,
            float? leadSeconds = null, float? graceSeconds = null)
        {
            this.director = director;
            extractionZone = zone;
            beaconZone = beacon;
            if (leadSeconds.HasValue) beaconLeadSeconds = leadSeconds.Value;
            if (graceSeconds.HasValue) beaconGraceSeconds = graceSeconds.Value;
        }
#endif
    }
}
