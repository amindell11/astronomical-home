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
using UnityEngine.InputSystem;

namespace Game.Preview.Fixtures
{
    /// <summary>
    /// The crowded Playtest departure: real rocks surround an extraction zone, and a fresh F press
    /// commits synchronously so GameHost protects the player before another simulation step.
    /// Normal sector extraction does not use this fixture.
    /// </summary>
    public sealed class CrowdedJumpGateModule : SectorModule
    {
        [SerializeField] internal ExtractionZone zone;
        [SerializeField] internal float gateHeading = 70f;
        [SerializeField] internal AsteroidSpawner asteroids;
        [SerializeField] internal Transform[] rockPlacements;

        private InputAction jump;
        private SpineObjectiveHandle objective;
        private bool committed;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!zone || !asteroids || rockPlacements == null || rockPlacements.Length == 0)
                throw new InvalidOperationException("The crowded jump fixture requires its zone and rock placements.");
            if (!ctx.Hero)
                throw new InvalidOperationException("The crowded jump fixture requires a player.");
            committed = false;
            asteroids.SetPresentation(ctx.PresentationEnabled);
            zone.BindPlayer(ctx.Hero.Body);
            zone.Arm();
            objective = ctx.Objectives.SetSpineObjective(
                new MissionDefinition("jump", new Dictionary<string, string> { ["jump"] = "departed" }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    ["jump"] = () => new JumpState(this),
                    ["departed"] = () => new CompletedState()
                }, zone.transform);
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/f");
            jump.performed += Commit;
            jump.Enable();
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

        private void Commit(InputAction.CallbackContext context)
        {
            if (committed || !zone.IsPlayerInZone) return;
            committed = true;
            jump.Disable();
            RequestSectorEnd(SectorResult.Extracted(gateHeading));
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ReleaseInput();
            zone.Disarm();
            objective.Close();
            asteroids.DespawnAll();
            yield break;
        }

        private void ReleaseInput()
        {
            jump?.Dispose();
            jump = null;
        }

        private void OnDestroy() => ReleaseInput();

        private sealed class JumpState : ObjectiveState
        {
            private readonly CrowdedJumpGateModule owner;
            public JumpState(CrowdedJumpGateModule owner) => this.owner = owner;
            public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
            public override bool IsComplete => owner.committed;
            public override string StatusLine => owner.zone.IsPlayerInZone
                ? "PRESS F TO JUMP" : "REACH THE JUMP GATE";
            public override void Tick(float deltaTime) { }
        }
    }
}
