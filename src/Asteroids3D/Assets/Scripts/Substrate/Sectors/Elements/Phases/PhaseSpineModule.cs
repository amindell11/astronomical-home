using System;
using System.Collections;
using System.Collections.Generic;
using Objectives;
using Objectives.States;
using Ships;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// A sector spine over its wave director's sector phases: survive them, then reach
    /// the extraction zone, which stays hidden until the phases are done and then appears on a clear
    /// point away from the hero. The hero's jump request inside commits extraction. The director is held to
    /// observe its phase, never commanded. The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1051
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

        private SpineObjectiveHandle spine;
        private Ship hero;
        private ExtractionJumpState extraction;

        public SectorPhaseMark Phase => director.Phase;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!director || !extractionZone)
                throw new InvalidOperationException(
                    $"PhaseSpineModule on '{name}' needs its wave director and extraction zone wired.");

            hero = ctx.Hero;
            extractionZone.BindPlayer(hero ? hero.Body : null);
            extractionZone.gameObject.SetActive(false);
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
            spine = ctx.Objectives.SetSpineObjective(mission, builders);
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ReleaseExtraction();
            if (extractionZone) extractionZone.Disarm();
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

        private void OpenExtraction()
        {
            if (hero)
                extractionZone.transform.position = ClearPointAround(GamePlane.WorldPointToPlane(hero.transform.position));
            extractionZone.gameObject.SetActive(true);
            extractionZone.Arm();
            spine.Target = extractionZone.transform;
        }

        private Vector3 ClearPointAround(Vector2 center)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                position = GamePlane.PlanePointToWorld(center + outward * zoneDistance);
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
        internal void Bind(WaveDirector director, ExtractionZone zone)
        {
            this.director = director;
            extractionZone = zone;
        }
#endif
    }
}
