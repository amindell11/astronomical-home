using System;
using AI;
using AI.Observation;
using Combat.Conditions;
using Ships;
using Ships.Command;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The ML-Agents face of one episode ship: observes at the runner's decision boundary (the envelope bits come from the SAME boundary snapshot — never re-evaluate Gunsight from observation code) and pushes each received action into the <see cref="AgentChooser"/>. Lifecycle (reset, decision pacing, reward) is owned by the hosting loop; MaxStep stays 0 and OnEpisodeBegin stays a no-op.</summary>
    public sealed class ShipAgent : Agent
    {
        public const float HeuristicHoldRange = 15f;

        private Ship self;
        private Ship opponent;
        private AgentChooser chooser;
        private RewardSpec spec;
        private Vector2 arenaCenter;
        private IHeatReadout primaryHeat;
        private float primaryProjectileSpeed;
        private Scout scout;
        private EpisodeRunner runner;
        private BufferSensorComponent obstacleBuffer;
        private readonly float[] observationBuffer = new float[AgentObservations.CombatChannels];
        private readonly float[] tokenScratch = new float[AgentObservations.ObstacleTokenCap * AgentObservations.ObstacleTokenFloats];
        private readonly float[] token = new float[AgentObservations.ObstacleTokenFloats];

        public int DecisionsReceived { get; private set; }

        public void Configure(Ship self, Ship opponent, AgentChooser chooser, in RewardSpec spec, Vector2 arenaCenter, Scout scout, BufferSensorComponent obstacleBuffer)
        {
            this.self = self;
            this.opponent = opponent;
            this.chooser = chooser;
            this.spec = spec;
            this.arenaCenter = arenaCenter;
            this.scout = scout;
            this.obstacleBuffer = obstacleBuffer;
            primaryHeat = ResolvePrimaryHeat(self);
            primaryProjectileSpeed = self.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary);
        }

        /// <summary>Resolved once at compose time — the lasers-only loadout is an episode constant, so the mount (and its Heat) never changes under a live agent.</summary>
        private static IHeatReadout ResolvePrimaryHeat(Ship ship)
        {
            foreach (var readout in ship.Weapons.ReadoutContext.Readouts(WeaponSlot.Primary))
                if (readout is IHeatReadout heat)
                    return heat;
            return null;
        }

        public void BindEpisode(EpisodeRunner episodeRunner)
        {
            runner = episodeRunner;
            DecisionsReceived = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var snapshot = runner.BoundarySnapshot;
            var enemyKin = opponent.Kinematics;
            var target = new TargetView(true, enemyKin.pos, enemyKin.vel, enemyKin.Forward,
                opponent.HealthPct, opponent.ShieldPct);

            AgentObservations.Fill(observationBuffer, self, in target,
                snapshot.inMyEnvelope, snapshot.inEnemyEnvelope,
                self.Weapons.Context.IsReady(WeaponSlot.Primary),
                primaryHeat?.HeatPct ?? 0f, primaryProjectileSpeed,
                arenaCenter, spec.arenaRadius);

            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);

            var tokens = AgentObservations.BuildObstacleTokens(
                tokenScratch, AgentObservations.ObstacleTokenCap, self, spec.arenaRadius, scout.AsteroidScan);
            for (var t = 0; t < tokens; t++)
            {
                Array.Copy(tokenScratch, t * AgentObservations.ObstacleTokenFloats, token, 0, AgentObservations.ObstacleTokenFloats);
                obstacleBuffer.AppendObservation(token);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var action = AgentActions.Map(continuous[0], continuous[1], continuous[2],
                continuous[3], continuous[4], continuous[5]);
            var selfKin = self.Kinematics;
            var worldVelocity = AgentActions.ToWorldVelocity(
                action.velocityEgo, selfKin.Forward, self.MaxSpeed);
            var facingRad = AgentActions.ToFacingRad(action.facingEgo, selfKin.Forward);
            chooser.SetAction(worldVelocity, facingRad, action.fire, action.boost, self.BoostAvailable);
            DecisionsReceived++;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            var selfKin = self.Kinematics;
            var world = RangerChooser.HoldRangeVelocity(
                in selfKin, opponent.Kinematics, HeuristicHoldRange, self.MaxSpeed);
            var ego = AgentActions.ToEgoAction(world, selfKin.Forward, self.MaxSpeed);
            var bearingEgo = new EgoFrame(Vector2.zero, selfKin.Forward)
                .Direction(opponent.Kinematics.pos - selfKin.pos);
            bearingEgo = bearingEgo.sqrMagnitude > 1e-8f ? bearingEgo.normalized : Vector2.up;
            continuous[0] = ego.x;
            continuous[1] = ego.y;
            continuous[2] = 1f;
            continuous[3] = -1f;
            continuous[4] = bearingEgo.x;
            continuous[5] = bearingEgo.y;
        }

        // The hosting loop is the single reset owner; a policy-triggered begin here would race the pair-reset.
        public override void OnEpisodeBegin() { }
    }
}
