using System;
using AI;
using AI.Observation;
using Combat.Conditions;
using Ships;
using Ships.Command;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Game.RLHarness
{
    /// <summary>Gameplay inference host for one ship: observes the boundary state its <see cref="InferenceBrain"/> captured and pushes each decision into the brain's mailbox. The brain owns pacing on the Academy auto-clock; MaxStep stays 0 and OnEpisodeBegin stays a no-op. Reads the same <see cref="AgentObservations"/> / <see cref="AgentActions"/> statics the training host does, so gameplay and training cannot drift into two readings of one checkpoint.</summary>
    public sealed class LivePilotAgent : Agent
    {
        private PolicyBrain mailbox;
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
        private float speedRef;
        private readonly float[] observationBuffer = new float[AgentObservations.CombatChannels];
        private readonly float[] tokenScratch =
            new float[AgentObservations.ObstacleTokenCap * AgentObservations.ObstacleTokenFloats];
        private readonly float[] token = new float[AgentObservations.ObstacleTokenFloats];
        private readonly RockSlotRoster rockSlots = new();
        private readonly AsteroidRef[] boundScratch = new AsteroidRef[3];

        public int DecisionsReceived { get; private set; }

        public void Bind(PolicyBrain mailbox, AI.Scout scout, BufferSensorComponent obstacleBuffer, float speedRef)
        {
            this.mailbox = mailbox;
            this.scout = scout;
            this.obstacleBuffer = obstacleBuffer;
            this.speedRef = speedRef;

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
            var boundCount = mailbox.GetBoundRocks(boundScratch);
            rockSlots.Update(self.Kinematics.pos, enemyKin.pos, scout.AsteroidScan,
                boundScratch.AsSpan(0, boundCount));
            var view = new TargetView(true, enemyKin.pos, enemyKin.vel, enemyKin.Forward,
                target.HealthPct, target.ShieldPct);

            AgentObservations.Fill(observationBuffer, self, in view,
                boundary.inMyEnvelope, boundary.inEnemyEnvelope,
                self.Weapons.Context.IsReady(WeaponSlot.Primary),
                primaryHeat?.HeatPct ?? 0f, primaryProjectileSpeed,
                leashCenter, leashRadius,
                target.Weapons.Context.IsReady(WeaponSlot.Primary), enemyHeat?.HeatPct ?? 0f,
                rockSlots);

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

        // Gameplay always runs the released vocabulary — the curriculum pin is a trainer-only state.
        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask) =>
            AgentActions.WriteMask(actionMask, rockSlots, released: true);

        public override void OnActionReceived(ActionBuffers actions)
        {
            var action = AgentActions.Map(in actions, rockSlots, speedRef, leashRadius);
            mailbox.SetAction(in action, self.BoostAvailable);
            DecisionsReceived++;
        }

        public override void OnEpisodeBegin() { }
    }
}
