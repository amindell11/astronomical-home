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
    /// <summary>Gameplay inference host for one ship: observes the boundary state its <see cref="InferenceChooser"/> captured and pushes each decision into the chooser's mailbox. The chooser owns pacing on the Academy auto-clock; MaxStep stays 0 and OnEpisodeBegin stays a no-op. Serves both <see cref="PolicySurface"/>s off the same shared statics the training host uses, so gameplay and training can never drift into two readings of one checkpoint.</summary>
    public sealed class LivePilotAgent : Agent
    {
        private AgentChooser mailbox;
        private AI.Scout scout;
        private PolicySurface surface;
        private BufferSensorComponent obstacleBuffer;
        private Ship self;
        private Ship target;
        private UnityEngine.Vector2 leashCenter;
        private float leashRadius;
        private CombatSnapshot boundary;
        private IHeatReadout primaryHeat;
        private float primaryProjectileSpeed;
        private bool loadoutResolved;
        private float[] observationBuffer;
        private float[] tokenScratch;
        private readonly float[] token = new float[AgentObservations.ObstacleTokenFloats];

        public int DecisionsReceived { get; private set; }

        /// <summary><paramref name="obstacleBuffer"/> is required by <see cref="PolicySurface.ManualAim"/> and unused by the legacy surface, which packs asteroids into its flat vector.</summary>
        public void Bind(AgentChooser mailbox, AI.Scout scout, PolicySurface surface,
            BufferSensorComponent obstacleBuffer)
        {
            this.mailbox = mailbox;
            this.scout = scout;
            this.surface = surface;
            this.obstacleBuffer = obstacleBuffer;

            if (surface == PolicySurface.ManualAim && !obstacleBuffer)
                throw new InvalidOperationException(
                    "PolicySurface.ManualAim needs a BufferSensorComponent — without it the asteroid tokens vanish and the policy reads an empty attention buffer as an empty arena.");

            observationBuffer = new float[surface == PolicySurface.Legacy72
                ? LegacyAgentObservations.Size
                : AgentObservations.CombatChannels];
            tokenScratch = surface == PolicySurface.ManualAim
                ? new float[AgentObservations.ObstacleTokenCap * AgentObservations.ObstacleTokenFloats]
                : Array.Empty<float>();
        }

        public void CaptureBoundary(Ship self, Ship target, UnityEngine.Vector2 leashCenter, float leashRadius)
        {
            this.self = self;
            this.target = target;
            this.leashCenter = leashCenter;
            this.leashRadius = leashRadius;
            boundary = CombatSnapshotExtractor.Capture(self, target, leashCenter);
            ResolveLoadout(self);
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
            var primaryReady = self.Weapons.Context.IsReady(WeaponSlot.Primary);
            var heatPct = primaryHeat?.HeatPct ?? 0f;

            if (surface == PolicySurface.Legacy72)
                LegacyAgentObservations.Fill(observationBuffer, self, in view,
                    boundary.inMyEnvelope, boundary.inEnemyEnvelope, primaryReady, heatPct,
                    leashCenter, leashRadius, scout.AsteroidScan);
            else
                AgentObservations.Fill(observationBuffer, self, in view,
                    boundary.inMyEnvelope, boundary.inEnemyEnvelope, primaryReady, heatPct,
                    primaryProjectileSpeed, leashCenter, leashRadius);

            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);

            if (surface != PolicySurface.ManualAim) return;

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
            var forward = self.Kinematics.Forward;

            if (surface == PolicySurface.Legacy72)
            {
                var legacy = LegacyAgentObservations.Map(
                    continuous[0], continuous[1], continuous[2], continuous[3]);
                mailbox.SetLegacyAction(
                    AgentActions.ToWorldVelocity(legacy.velocityEgo, forward, self.MaxSpeed),
                    legacy.fire, legacy.boost, self.BoostAvailable);
            }
            else
            {
                var action = AgentActions.Map(continuous[0], continuous[1], continuous[2],
                    continuous[3], continuous[4], continuous[5]);
                mailbox.SetAction(
                    AgentActions.ToWorldVelocity(action.velocityEgo, forward, self.MaxSpeed),
                    AgentActions.ToFacingRad(action.facingEgo, forward),
                    AgentActions.ToFacingWeight(action.facingEgo),
                    action.fire, action.boost, self.BoostAvailable);
            }

            DecisionsReceived++;
        }

        public override void OnEpisodeBegin() { }
    }
}
