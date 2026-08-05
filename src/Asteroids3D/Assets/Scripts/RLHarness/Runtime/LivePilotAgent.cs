using System;
using AI.Observation;
using Combat.Conditions;
using Ships;
using Ships.Command;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Game.RLHarness
{
    /// <summary>Gameplay inference host for one ship: observes the boundary state its <see cref="InferenceChooser"/> captured and pushes each decision into the chooser's mailbox. The chooser owns pacing on the Academy auto-clock; MaxStep stays 0 and OnEpisodeBegin stays a no-op. Reads the same <see cref="AgentObservations"/> / <see cref="AgentActions"/> statics the training host does, so gameplay and training cannot drift into two readings of one checkpoint.</summary>
    public sealed class LivePilotAgent : Agent
    {
        private AgentChooser mailbox;
        private AI.Scout scout;
        private BufferSensorComponent obstacleBuffer;
        private Ship self;
        private Ship target;
        private UnityEngine.Vector2 leashCenter;
        private float leashRadius;
        private CombatSnapshot boundary;
        private IHeatReadout primaryHeat;
        private IHeatReadout enemyHeat;
        private float primaryProjectileSpeed;
        private bool loadoutResolved;
        private readonly float[] observationBuffer = new float[AgentObservations.CombatChannels];
        private readonly float[] tokenScratch =
            new float[AgentObservations.ObstacleTokenCap * AgentObservations.ObstacleTokenFloats];
        private readonly float[] token = new float[AgentObservations.ObstacleTokenFloats];

        public int DecisionsReceived { get; private set; }

        public void Bind(AgentChooser mailbox, AI.Scout scout, BufferSensorComponent obstacleBuffer)
        {
            this.mailbox = mailbox;
            this.scout = scout;
            this.obstacleBuffer = obstacleBuffer;

            if (!obstacleBuffer)
                throw new InvalidOperationException(
                    "LivePilotAgent needs a BufferSensorComponent — without it the asteroid tokens vanish and the policy reads an empty attention buffer as an empty arena.");
        }

        public void CaptureBoundary(Ship self, Ship target, UnityEngine.Vector2 leashCenter, float leashRadius)
        {
            this.self = self;
            this.target = target;
            this.leashCenter = leashCenter;
            this.leashRadius = leashRadius;
            boundary = CombatSnapshotExtractor.Capture(self, target, leashCenter);
            ResolveLoadout(self);
            // The target can re-capture, so the enemy readout re-resolves per boundary — not under the self-loadout once-guard.
            enemyHeat = ResolvePrimaryHeat(target);
        }

        /// <summary>The lasers-only loadout is fixed for a pilot's lifetime, so the mount and its Heat are read once rather than per decision.</summary>
        private void ResolveLoadout(Ship ship)
        {
            if (loadoutResolved) return;
            primaryHeat = ResolvePrimaryHeat(ship);
            primaryProjectileSpeed = ship.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary);
            loadoutResolved = true;
        }

        private static IHeatReadout ResolvePrimaryHeat(Ship ship)
        {
            foreach (var readout in ship.Weapons.ReadoutContext.Readouts(WeaponSlot.Primary))
                if (readout is IHeatReadout heat)
                    return heat;
            return null;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var enemyKin = target.Kinematics;
            var view = new TargetView(true, enemyKin.pos, enemyKin.vel, enemyKin.Forward,
                target.HealthPct, target.ShieldPct);

            AgentObservations.Fill(observationBuffer, self, in view,
                boundary.inMyEnvelope, boundary.inEnemyEnvelope,
                self.Weapons.Context.IsReady(WeaponSlot.Primary),
                primaryHeat?.HeatPct ?? 0f, primaryProjectileSpeed,
                leashCenter, leashRadius,
                target.Weapons.Context.IsReady(WeaponSlot.Primary), enemyHeat?.HeatPct ?? 0f);

            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);

            var tokens = AgentObservations.BuildObstacleTokens(
                tokenScratch, AgentObservations.ObstacleTokenCap, self, leashRadius, scout.AsteroidScan);
            for (var t = 0; t < tokens; t++)
            {
                Array.Copy(tokenScratch, t * AgentObservations.ObstacleTokenFloats, token, 0,
                    AgentObservations.ObstacleTokenFloats);
                obstacleBuffer.AppendObservation(token);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var discrete = actions.DiscreteActions;
            var action = AgentActions.Map(continuous[0], continuous[1], continuous[2],
                continuous[3], continuous[4], discrete[0], discrete[1], self.MaxSpeed);

            mailbox.SetAction(in action, self.BoostAvailable);
            DecisionsReceived++;
        }

        public override void OnEpisodeBegin() { }
    }
}
