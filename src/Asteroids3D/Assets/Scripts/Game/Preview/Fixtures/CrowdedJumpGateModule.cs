using System;
using System.Collections;
using System.Collections.Generic;
using Asteroids.Spawning;
using Objectives;
using Objectives.States;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Game.Preview.Fixtures
{
    /// <summary>
    /// Real rocks surround a Playtest extraction zone using the same commitment state as production sectors.
    /// </summary>
    public sealed class CrowdedJumpGateModule : SectorModule
    {
        [SerializeField] internal ExtractionZone zone;
        [SerializeField] internal AsteroidSpawner asteroids;
        [SerializeField] internal Transform[] rockPlacements;

        private ExtractionJumpState extraction;
        private SpineObjectiveHandle objective;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!zone || !asteroids || rockPlacements == null || rockPlacements.Length == 0)
                throw new InvalidOperationException("The crowded jump fixture requires its zone and rock placements.");
            if (!ctx.Hero)
                throw new InvalidOperationException("The crowded jump fixture requires a player.");
            asteroids.SetPresentation(ctx.PresentationEnabled);
            zone.BindPlayer(ctx.Hero.Body);
            zone.Arm();
            extraction = new ExtractionJumpState(zone, ctx.Hero,
                () => RequestSectorEnd(SectorResult.Extracted(zone.DepartureHeading)));
            objective = ctx.Objectives.SetSpineObjective(
                new MissionDefinition("jump", new Dictionary<string, string> { ["jump"] = "departed" }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    ["jump"] = () => extraction,
                    ["departed"] = () => new CompletedState()
                }, zone.transform);
            yield break;
        }

        private void Start()
        {
            var settings = asteroids.Settings;
            for (var i = 0; i < rockPlacements.Length; i++)
            {
                var placement = rockPlacements[i];
                var meshIndex = i % settings.meshInfos.Length;
                var mesh = settings.meshInfos[meshIndex];
                var scale = placement.localScale.x;
                var attributes = new AsteroidAttributes(mesh, meshIndex,
                    mesh.cachedVolume * scale * scale * scale * settings.density,
                    scale, Vector3.zero, Vector3.zero);
                asteroids.Spawn(new Pose(placement.position, placement.rotation), attributes, (uint)(713 + i));
            }
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ReleaseExtraction();
            zone.Disarm();
            objective.Close();
            asteroids.DespawnAll();
            yield break;
        }

        private void ReleaseExtraction()
        {
            extraction?.Dispose();
            extraction = null;
        }

        private void OnDestroy() => ReleaseExtraction();

    }
}
