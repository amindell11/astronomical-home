using AI.Scanning;
using UnityEngine;

namespace AI.Observation
{
    public enum TokenKind
    {
        Self = 0,
        Target = 1,
        Threat = 2,
        Obstacle = 3,
    }

    /// <summary>Own kinematics and resources. Egocentric origin, so position is implicitly (0,0)
    /// and heading implicitly +Y — only velocity and resource scalars are carried.</summary>
    public readonly struct SelfToken
    {
        public readonly Vector2 velocity;      // ego frame: x = ship-right, y = ship-forward
        public readonly float speedPct;        // speed / maxSpeed
        public readonly float yawRatePct;      // yawRate / maxYawRate, signed
        public readonly float healthPct;
        public readonly float shieldPct;
        public readonly float boostAvailable;  // 1 ready, 0 on cooldown
        public readonly float boostCooldownPct;

        public SelfToken(Vector2 velocity, float speedPct, float yawRatePct, float healthPct,
            float shieldPct, float boostAvailable, float boostCooldownPct)
        {
            this.velocity = velocity;
            this.speedPct = speedPct;
            this.yawRatePct = yawRatePct;
            this.healthPct = healthPct;
            this.shieldPct = shieldPct;
            this.boostAvailable = boostAvailable;
            this.boostCooldownPct = boostCooldownPct;
        }
    }

    /// <summary>Primary enemy, expressed relative to and rotated into the observer's frame.</summary>
    public readonly struct TargetToken
    {
        public readonly Vector2 relPosition;   // ego frame
        public readonly float distance;
        public readonly Vector2 relVelocity;   // ego frame, enemyVel - selfVel
        public readonly Vector2 facing;        // ego frame, enemy forward (unit)
        public readonly float healthPct;
        public readonly float shieldPct;

        public TargetToken(Vector2 relPosition, float distance, Vector2 relVelocity,
            Vector2 facing, float healthPct, float shieldPct)
        {
            this.relPosition = relPosition;
            this.distance = distance;
            this.relVelocity = relVelocity;
            this.facing = facing;
            this.healthPct = healthPct;
            this.shieldPct = shieldPct;
        }
    }

    /// <summary>An in-flight dangerous object (missile now; mines later) as a tracked kinematic
    /// entity, separate from the enemy ship. The channel that makes per-weapon evasion learnable.</summary>
    public readonly struct ThreatToken
    {
        public readonly Vector2 relPosition;   // ego frame
        public readonly float distance;
        public readonly Vector2 relVelocity;   // ego frame, threatVel - selfVel
        public readonly ThreatKind kind;

        public ThreatToken(Vector2 relPosition, float distance, Vector2 relVelocity, ThreatKind kind)
        {
            this.relPosition = relPosition;
            this.distance = distance;
            this.relVelocity = relVelocity;
            this.kind = kind;
        }
    }

    /// <summary>One covering circle of a nearby obstacle (a Scout-merged lobe), ego frame.</summary>
    public readonly struct ObstacleToken
    {
        public readonly Vector2 relPosition;   // ego frame, circle center
        public readonly float distance;
        public readonly float radius;

        public ObstacleToken(Vector2 relPosition, float distance, float radius)
        {
            this.relPosition = relPosition;
            this.distance = distance;
            this.radius = radius;
        }
    }

    /// <summary>
    /// An egocentric, target-relative snapshot of one AI ship's tactical situation as a list of
    /// typed entity tokens (self / target / threat-tracks / obstacle-lobes). RL-ready substrate:
    /// a stable schema with the full token set + counts. Nearest-K pooling and any network
    /// encoding are deliberately out of scope — those are network-side, later.
    /// </summary>
    public sealed class TacticalObservation
    {
        public const int SchemaVersion = 1;

        public float time;

        public SelfToken self;

        public bool hasTarget;
        public TargetToken target;

        public readonly ThreatToken[] threats;
        public int threatCount;

        public readonly ObstacleToken[] obstacles;
        public int obstacleCount;

        public TacticalObservation(int threatCapacity, int obstacleCapacity)
        {
            threats = new ThreatToken[threatCapacity];
            obstacles = new ObstacleToken[obstacleCapacity];
        }
    }
}
