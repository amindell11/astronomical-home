using AI.Scanning;
using Ships.Command;
using UnityEngine;

namespace AI.Observation
{
    /// <summary>The observer's plane-space egocentric frame (origin at the ship, +Y forward, +X right): maps plane-space points/directions into that frame and back.</summary>
    public readonly struct EgoFrame
    {
        private readonly Vector2 origin;
        private readonly Vector2 forward;
        private readonly Vector2 right;

        public EgoFrame(Vector2 origin, Vector2 forward)
        {
            this.origin = origin;
            this.forward = forward;
            right = new Vector2(forward.y, -forward.x);
        }

        public Vector2 Point(Vector2 worldPlane) => Direction(worldPlane - origin);

        public Vector2 Direction(Vector2 planeDir) =>
            new(Vector2.Dot(planeDir, right), Vector2.Dot(planeDir, forward));

        public Vector2 ToPlane(Vector2 egoPoint) => origin + PlaneDirection(egoPoint);

        public Vector2 PlaneDirection(Vector2 egoDir) => right * egoDir.x + forward * egoDir.y;
    }

    /// <summary>Plane-space snapshot of the primary enemy handed to the extractor, decoupling it from <see cref="Context.EnemyTracker"/> so the transform math stays unit-testable.</summary>
    public readonly struct TargetView
    {
        public readonly bool has;
        public readonly Vector2 pos;
        public readonly Vector2 vel;
        public readonly Vector2 forward;
        public readonly float healthPct;
        public readonly float shieldPct;

        public TargetView(bool has, Vector2 pos, Vector2 vel, Vector2 forward, float healthPct, float shieldPct)
        {
            this.has = has;
            this.pos = pos;
            this.vel = vel;
            this.forward = forward;
            this.healthPct = healthPct;
            this.shieldPct = shieldPct;
        }

        public static readonly TargetView None = new(false, default, default, default, 0f, 0f);
    }

    /// <summary>Builds a <see cref="TacticalObservation"/> from a ship's live views; pure over its inputs so the frame math is directly testable — the physics scans it consumes are owned upstream (Scout, <see cref="ThreatScanner"/>).</summary>
    public static class ObservationExtractor
    {
        public static void Populate(
            TacticalObservation obs,
            IShipStatus self,
            in TargetView target,
            ThreatContact[] threats, int threatCount,
            ObstacleScan obstacles,
            float time)
        {
            obs.Clear();

            var kin = self.Kinematics;
            var frame = new EgoFrame(kin.pos, kin.Forward);

            obs.time = time;
            obs.self = BuildSelf(self, frame);
            obs.hasTarget = target.has;
            if (target.has)
                obs.target = BuildTarget(frame, kin.vel, target);

            for (var i = 0; i < threatCount; i++)
            {
                var t = threats[i];
                var relPos = frame.Point(t.planePos);
                obs.threats.Add(new ThreatToken(
                    relPos, relPos.magnitude, frame.Direction(t.planeVel - kin.vel), t.kind));
            }

            var buffer = obstacles.buffer;
            if (buffer == null) return;

            for (var i = 0; i < obstacles.count; i++)
            {
                var o = buffer[i];
                if (o.lobeCount > 0)
                {
                    for (var l = 0; l < o.lobeCount; l++)
                    {
                        var lobe = o.Lobe(l);
                        AddObstacle(obs, frame, lobe.center, lobe.radius);
                    }
                }
                else
                {
                    AddObstacle(obs, frame, o.position, o.radius);
                }
            }
        }

        private static void AddObstacle(TacticalObservation obs, EgoFrame frame, Vector2 planeCenter, float radius)
        {
            var relPos = frame.Point(planeCenter);
            obs.obstacles.Add(new ObstacleToken(relPos, relPos.magnitude, radius));
        }

        internal static SelfToken BuildSelf(IShipStatus self, EgoFrame frame)
        {
            var kin = self.Kinematics;
            var speedPct = self.MaxSpeed > 0f ? Mathf.Clamp01(kin.Speed / self.MaxSpeed) : 0f;
            var yawRatePct = self.MaxYawRate > 0f ? Mathf.Clamp(kin.yawRate / self.MaxYawRate, -1f, 1f) : 0f;
            var cooldown = self.Dynamics.boostCooldown;
            var boostCooldownPct = cooldown > 0f ? Mathf.Clamp01(self.BoostCooldownRemaining / cooldown) : 0f;

            return new SelfToken(
                frame.Direction(kin.vel), speedPct, yawRatePct,
                self.HealthPct, self.ShieldPct,
                self.BoostAvailable ? 1f : 0f, boostCooldownPct);
        }

        internal static TargetToken BuildTarget(EgoFrame frame, Vector2 selfVel, in TargetView target)
        {
            var relPos = frame.Point(target.pos);
            return new TargetToken(
                relPos, relPos.magnitude,
                frame.Direction(target.vel - selfVel),
                frame.Direction(target.forward),
                target.healthPct, target.shieldPct);
        }
    }
}
