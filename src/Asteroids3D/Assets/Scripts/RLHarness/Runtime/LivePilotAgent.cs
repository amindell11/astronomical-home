using AI.Observation;
using Combat.Conditions;
using Ships;
using Ships.Command;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Game.RLHarness
{
    /// <summary>Gameplay inference host for one ship: observes the boundary state its <see cref="InferenceChooser"/> captured and pushes each decision into the chooser's mailbox. The chooser owns pacing and Academy stepping; MaxStep stays 0 and OnEpisodeBegin stays a no-op.</summary>
    public sealed class LivePilotAgent : Agent
    {
        private AgentChooser mailbox;
        private AI.Scout scout;
        private Ship self;
        private Ship target;
        private UnityEngine.Vector2 leashCenter;
        private float leashRadius;
        private CombatSnapshot boundary;
        private IHeatReadout primaryHeat;
        private readonly float[] observationBuffer = new float[AgentObservations.Size];

        public int DecisionsReceived { get; private set; }

        public void Bind(AgentChooser mailbox, AI.Scout scout)
        {
            this.mailbox = mailbox;
            this.scout = scout;
        }

        public void CaptureBoundary(Ship self, Ship target, UnityEngine.Vector2 leashCenter, float leashRadius)
        {
            this.self = self;
            this.target = target;
            this.leashCenter = leashCenter;
            this.leashRadius = leashRadius;
            boundary = CombatSnapshotExtractor.Capture(self, target, leashCenter);
            primaryHeat ??= ResolvePrimaryHeat(self);
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
                primaryHeat?.HeatPct ?? 0f,
                leashCenter, leashRadius, scout.AsteroidScan);

            for (var i = 0; i < observationBuffer.Length; i++)
                sensor.AddObservation(observationBuffer[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var action = AgentActions.Map(continuous[0], continuous[1], continuous[2], continuous[3]);
            var worldVelocity = AgentActions.ToWorldVelocity(
                action.velocityEgo, self.Kinematics.Forward, self.MaxSpeed);
            mailbox.SetAction(worldVelocity, action.fire, action.boost, self.BoostAvailable);
            DecisionsReceived++;
        }

        public override void OnEpisodeBegin() { }
    }
}
