using System;
using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Objectives;
using Objectives.Presentation;
using Objectives.States;
using Ships;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Placement;
using Substrate.Services.Objectives;
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// A sector spine over its wave director's sector phases: survive them, then reach the extraction
    /// zone, hidden until the table is done, then placed on a clear point away from the hero. A jump
    /// request inside commits extraction. Shortly before a lull, a beacon opens the same way and stays
    /// through that lull and a grace window after it: into the next phase, or into the elite fight for
    /// the lull before the elite. Flying in claims it (<see cref="BeaconClaimed"/>); an unclaimed one
    /// closes when its grace runs out or the next beacon opens. While the hunt runs, the HUD countdown
    /// reads the hunt. The director, the hunt and the elite are held to observe, never commanded. With
    /// an elite, a lull follows the last phase; the director summons the elite when that lull ends, and
    /// extraction opens on the elite's death.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1051
    /// </summary>
    public class PhaseSpineModule : SectorModule
    {
        public const string StepSurvive = "survive";
        public const string StepElite = "elite";
        public const string StepExtract = "extract";
        public const string StepCompleted = "completed";

        [SerializeField] private WaveDirector director;
        [Tooltip("The sector's opening hunt, read for the HUD's countdown until the hunt releases; unset for none.")]
        [SerializeField] private SectorHunt hunt;
        [SerializeField] private ExtractionZone extractionZone;

        [Header("Zone placement")]
        [Tooltip("Plane distance from the hero the zone appears at when extraction opens.")]
        [SerializeField, Min(0f)] private float zoneDistance = 150f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float zoneClearance = 35f;
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
        [Tooltip("Seconds before an unclaimed beacon closes that its marker starts blinking.")]
        [SerializeField, Min(0f)] private float beaconWarningSeconds = 3f;

        private SpineObjectiveHandle spine;
        private Ship hero;
        private IObstacleField field;
        private ExtractionJumpState extraction;
        private bool beaconsEnabled;
        private bool beaconOpen;
        // The last beacon's lull, kept after it closes so a claimed lull never reopens.
        private int beaconLull;

        public SectorPhaseMark Phase => director.Phase;

        public IEliteStatus Elite => director.Elite;

        public SectorHunt Hunt => hunt ? hunt : null;

        public IWaveSignal Waves => director;

        public bool IsRival(ShipId ship) => director.IsRival(ship);

        /// <summary>True from a lull beacon's claim until the next opens.</summary>
        public bool BeaconClaimed { get; private set; }

        /// <summary>What this sector's director can send from <paramref name="row"/>, readable off the sector prefab before it loads.</summary>
        public SectorThreat Threat(SectorEntry row) => director.Threat(row);

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!director || !extractionZone || !beaconZone)
                throw new InvalidOperationException(
                    $"PhaseSpineModule on '{name}' needs its wave director, extraction zone and beacon zone wired.");

            hero = ctx.Hero;
            field = ctx.Field;
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

            var steps = new Dictionary<string, string>
            {
                { StepSurvive, StepExtract },
                { StepExtract, StepCompleted }
            };
            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                [StepSurvive] = () => new SurviveState(director),
                [StepExtract] = () => extraction,
                [StepCompleted] = () => new CompletedState()
            };
            var elite = director.Elite;
            if (elite != null)
            {
                steps[StepSurvive] = StepElite;
                steps[StepElite] = StepExtract;
                builders[StepElite] = () => new EliteState(elite);
            }
            var mission = new MissionDefinition(StepSurvive, steps);
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
            field = null;
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
        // The closing lull is numbered PhaseCount, apart from the lull before the last phase.
        private int LullAhead(SectorPhaseMark mark)
        {
            if (mark.Done || mark.InOpeningLull) return -1;
            if (mark.InLull) return mark.Closing ? director.PhaseCount : mark.Index;
            var last = mark.Index == director.PhaseCount - 1;
            var lullFollows = !last || director.ClosingLull;
            return lullFollows && mark.SecondsLeft <= beaconLeadSeconds ? mark.Index + 1 : -1;
        }

        private bool InBeaconGrace(SectorPhaseMark mark) =>
            !mark.InLull && (mark.Done ? beaconLull == director.PhaseCount : mark.Index == beaconLull);

        private bool BeaconExpired(SectorPhaseMark mark) =>
            InBeaconGrace(mark)
                ? mark.ElapsedSeconds >= beaconGraceSeconds
                : !mark.InLull && (mark.Done || mark.Index > beaconLull);

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

        private float? BeaconGraceLeft(SectorPhaseMark mark) =>
            beaconOpen && InBeaconGrace(mark)
                ? Mathf.Max(0f, beaconGraceSeconds - mark.ElapsedSeconds)
                : null;

        private SectorPhaseProgress ReadPhaseProgress()
        {
            var mark = director.Phase;
            if (hunt && mark.InOpeningLull)
            {
                if (!hunt.Alarmed)
                    return new SectorPhaseProgress(-1, director.PhaseCount, hunt.SearchSecondsLeft, false,
                        hunt.SpottedFill > 0f ? HuntReading.Spotted : HuntReading.Searching, hunt.SpottedFill);
                if (!hunt.Released)
                    return new SectorPhaseProgress(-1, director.PhaseCount, hunt.GraceSecondsLeft, false,
                        HuntReading.Coming);
            }

            var graceLeft = BeaconGraceLeft(mark);
            var beaconFill = graceLeft.HasValue && beaconGraceSeconds > 0f ? graceLeft.Value / beaconGraceSeconds : 1f;
            var beaconExpiring = graceLeft <= beaconWarningSeconds;
            ReadCaption(mark, out var caption, out var captionSeconds);
            var index = mark.InLull && !mark.Closing ? mark.Index - 1 : mark.Index;
            return new SectorPhaseProgress(index,
                director.PhaseCount, mark.SecondsToEnd, mark.Done,
                beaconFill: beaconFill, beaconExpiring: beaconExpiring,
                caption: caption, captionSeconds: captionSeconds);
        }

        private void ReadCaption(SectorPhaseMark mark, out PhaseCaption caption, out float seconds)
        {
            if (mark.InLull || director.EarnedLull)
            {
                caption = PhaseCaption.Lull;
                seconds = mark.CountdownSeconds;
                return;
            }

            if (director.WaveAhead)
            {
                caption = PhaseCaption.NextWave;
                seconds = director.NextWaveSeconds;
                return;
            }

            caption = director.Elite != null ? PhaseCaption.Elite : PhaseCaption.Extraction;
            seconds = 0f;
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
                var planePoint = center + outward * distance;
                position = GamePlane.PlanePointToWorld(planePoint);
                if (ClearPoint.IsClear(field, planePoint, zoneClearance)) break;
            }
            return position;
        }

        private static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private sealed class EliteState : ObjectiveState
        {
            private readonly IEliteStatus elite;

            public EliteState(IEliteStatus elite) => this.elite = elite;

            public override ObjectiveType StateType => ObjectiveType.ClearHostiles;

            public override void Tick(float deltaTime) { }

            public override bool IsComplete => elite.IsBeaten;
        }

        private sealed class SurviveState : ObjectiveState
        {
            private readonly WaveDirector director;
            private string line;
            private int shownIndex = -1;
            private int shownSeconds = -1;
            private bool shownLull;
            private bool shownClosing;

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
                    if (mark.Index == shownIndex && mark.InLull == shownLull && mark.Closing == shownClosing
                        && seconds == shownSeconds)
                        return line;

                    shownIndex = mark.Index;
                    shownLull = mark.InLull;
                    shownClosing = mark.Closing;
                    shownSeconds = seconds;
                    if (mark.Closing)
                        line = $"ELITE IN {Clock(seconds)}";
                    else
                    {
                        var phase = $"PHASE {mark.Index + 1}/{director.PhaseCount}";
                        line = mark.InLull ? $"{phase} IN {Clock(seconds)}" : $"{phase}    {Clock(seconds)}";
                    }
                    return line;
                }
            }
        }

#if UNITY_EDITOR
        internal void Bind(WaveDirector director, ExtractionZone zone, ExtractionZone beacon,
            float? leadSeconds = null, float? graceSeconds = null, SectorHunt hunt = null,
            float? warningSeconds = null)
        {
            this.director = director;
            this.hunt = hunt;
            extractionZone = zone;
            beaconZone = beacon;
            if (leadSeconds.HasValue) beaconLeadSeconds = leadSeconds.Value;
            if (graceSeconds.HasValue) beaconGraceSeconds = graceSeconds.Value;
            if (warningSeconds.HasValue) beaconWarningSeconds = warningSeconds.Value;
        }
#endif
    }
}
