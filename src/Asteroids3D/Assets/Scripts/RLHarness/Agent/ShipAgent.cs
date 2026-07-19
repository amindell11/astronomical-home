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
        private Scout scout;
        private EpisodeRunner runner;
        private readonly float[] observationBuffer = new float[AgentObservations.Size];

        public int DecisionsReceived { get; private set; }

        public void Configure(Ship self, Ship opponent, AgentChooser chooser, in RewardSpec spec, Vector2 arenaCenter, Scout scout)
        {
            this.self = self;
            this.opponent = opponent;
            this.chooser = chooser;
            this.spec = spec;
            this.arenaCenter = arenaCenter;
            this.scout = scout;
            primaryHeat = ResolvePrimaryHeat(self);
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
                primaryHeat?.HeatPct ?? 0f,
                arenaCenter, spec.arenaRadius, scout.AsteroidScan);

            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var action = AgentActions.Map(continuous[0], continuous[1], continuous[2], continuous[3]);
            var worldVelocity = AgentActions.ToWorldVelocity(
                action.velocityEgo, self.Kinematics.Forward, self.MaxSpeed);
            chooser.SetAction(worldVelocity, action.fire, action.boost, self.BoostAvailable);
            DecisionsReceived++;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            var selfKin = self.Kinematics;
            var world = RangerChooser.HoldRangeVelocity(
                in selfKin, opponent.Kinematics, HeuristicHoldRange, self.MaxSpeed);
            var ego = AgentActions.ToEgoAction(world, selfKin.Forward, self.MaxSpeed);
            continuous[0] = ego.x;
            continuous[1] = ego.y;
            continuous[2] = 1f;
            continuous[3] = -1f;
        }

        // The hosting loop is the single reset owner; a policy-triggered begin here would race the pair-reset.
        public override void OnEpisodeBegin() { }
    }
}
