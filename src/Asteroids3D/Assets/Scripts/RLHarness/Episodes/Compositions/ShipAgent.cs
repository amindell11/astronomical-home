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
        private enum StagedObservationKind { None, NextDecision, EpisodeEnd }

        public const float HeuristicHoldRange = 15f;

        private Ship self;
        private Ship opponent;
        private AgentChooser chooser;
        private RewardSpec spec;
        private Vector2 arenaCenter;
        private IHeatReadout primaryHeat;
        private IHeatReadout enemyHeat;
        private float primaryProjectileSpeed;
        private Scout scout;
        private EpisodeRunner runner;
        private DecisionTransitionRecorder transitionRecorder;
        private BufferSensorComponent obstacleBuffer;
        private DecisionObservation stagedObservation;
        private StagedObservationKind stagedObservationKind;
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
            // Resolved at the opponent-injection point (fixed for the episode), mirroring self's heat.
            enemyHeat = ResolvePrimaryHeat(opponent);
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

        public void BindEpisode(EpisodeRunner episodeRunner,
            DecisionTransitionRecorder decisionTransitionRecorder = null)
        {
            runner = episodeRunner;
            transitionRecorder = decisionTransitionRecorder;
            stagedObservation = default;
            stagedObservationKind = StagedObservationKind.None;
            DecisionsReceived = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (stagedObservationKind != StagedObservationKind.None)
            {
                Emit(sensor, in stagedObservation);
                if (stagedObservationKind == StagedObservationKind.NextDecision)
                    transitionRecorder.BeginDecision(in stagedObservation);
                stagedObservation = default;
                stagedObservationKind = StagedObservationKind.None;
                return;
            }

            var tokenCount = CaptureObservation();
            Emit(sensor, tokenCount);
            if (transitionRecorder == null) return;

            var observation = DecisionObservation.Copy(observationBuffer, tokenScratch, tokenCount);
            transitionRecorder.BeginDecision(in observation);
        }

        public void CompleteTransition(in BoundaryResult boundary)
        {
            if (transitionRecorder == null) return;

            var tokenCount = CaptureObservation();
            stagedObservation = DecisionObservation.Copy(observationBuffer, tokenScratch, tokenCount);
            transitionRecorder.Complete(in boundary, in stagedObservation);
            stagedObservationKind = boundary.endKind == EndKind.None
                ? StagedObservationKind.NextDecision
                : StagedObservationKind.EpisodeEnd;
        }

        private int CaptureObservation()
        {
            var snapshot = runner.BoundarySnapshot;
            var enemyKin = opponent.Kinematics;
            var target = new TargetView(true, enemyKin.pos, enemyKin.vel, enemyKin.Forward,
                opponent.HealthPct, opponent.ShieldPct);

            AgentObservations.Fill(observationBuffer, self, in target,
                snapshot.inMyEnvelope, snapshot.inEnemyEnvelope,
                self.Weapons.Context.IsReady(WeaponSlot.Primary),
                primaryHeat?.HeatPct ?? 0f, primaryProjectileSpeed,
                arenaCenter, spec.arenaRadius,
                opponent.Weapons.Context.IsReady(WeaponSlot.Primary), enemyHeat?.HeatPct ?? 0f);

            return AgentObservations.BuildObstacleTokens(
                tokenScratch, AgentObservations.ObstacleTokenCap, self, spec.arenaRadius, scout.AsteroidScan);
        }

        private void Emit(VectorSensor sensor, int tokenCount)
        {
            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);

            for (var t = 0; t < tokenCount; t++)
            {
                Array.Copy(tokenScratch, t * AgentObservations.ObstacleTokenFloats, token, 0, AgentObservations.ObstacleTokenFloats);
                obstacleBuffer.AppendObservation(token);
            }
        }

        private void Emit(VectorSensor sensor, in DecisionObservation observation)
        {
            for (var i = 0; i < observation.combat.Length; i++)
                sensor.AddObservation(observation.combat[i]);

            var tokenCount = observation.obstacleTokens.Length / AgentObservations.ObstacleTokenFloats;
            for (var t = 0; t < tokenCount; t++)
            {
                Array.Copy(observation.obstacleTokens,
                    t * AgentObservations.ObstacleTokenFloats,
                    token, 0, AgentObservations.ObstacleTokenFloats);
                obstacleBuffer.AppendObservation(token);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var discrete = actions.DiscreteActions;
            var action = AgentActions.Map(continuous[0], continuous[1], continuous[2],
                continuous[3], continuous[4], discrete[0], discrete[1], self.MaxSpeed);
            var boostExecuted = chooser.SetAction(in action, self.BoostAvailable);
            if (transitionRecorder != null)
            {
                var executed = new ExecutedDecisionAction
                {
                    continuous = new[]
                    {
                        continuous[0], continuous[1], continuous[2], continuous[3], continuous[4],
                    },
                    discrete = new[] { discrete[0], discrete[1] },
                    boostExecuted = boostExecuted,
                };
                transitionRecorder.RecordAction(in executed);
            }
            DecisionsReceived++;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            var discrete = actionsOut.DiscreteActions;
            var selfKin = self.Kinematics;
            var enemyKin = opponent.Kinematics;
            var world = RangerChooser.HoldRangeVelocity(
                in selfKin, enemyKin, HeuristicHoldRange, self.MaxSpeed);
            var los = enemyKin.pos - selfKin.pos;
            var losHat = los.sqrMagnitude > 1e-8f ? los.normalized : Vector2.up;
            var maxSpeed = Mathf.Max(self.MaxSpeed, 1e-3f);

            // Aim at intercept (offset 0, full weight), close along the LOS toward the hold band, no orbit, full velocity authority.
            continuous[0] = 0f;
            continuous[1] = 1f;
            continuous[2] = Mathf.Clamp(Vector2.Dot(world, losHat) / maxSpeed, -1f, 1f);
            continuous[3] = 0f;
            continuous[4] = 1f;
            discrete[0] = 1;
            discrete[1] = 0;
        }

        // The hosting loop is the single reset owner; a policy-triggered begin here would race the pair-reset.
        public override void OnEpisodeBegin() { }
    }
}
