using System.Collections.Generic;
using AI.Scanning;
using UnityEngine;

namespace AI.Observation
{
    /// <summary>Own kinematics and resources. Egocentric origin, so position is implicitly (0,0)
    /// and heading implicitly +Y — only velocity and resource scalars are carried.</summary>
    public readonly struct SelfToken
    {
        public readonly Vector2 velocity;      // ego frame: x = ship-right, y = ship-forward
        public readonly float speedPct;
        public readonly float yawRatePct;      // signed
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
        public readonly Vector2 relPosition;
        public readonly float distance;
        public readonly Vector2 relVelocity;   // enemyVel - selfVel, ego frame
        public readonly Vector2 facing;        // enemy forward (unit), ego frame
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
        public readonly Vector2 relPosition;
        public readonly float distance;
        public readonly Vector2 relVelocity;   // threatVel - selfVel, ego frame
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
        public readonly Vector2 relPosition;   // circle center, ego frame
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
    /// An egocentric, target-relative snapshot of one AI ship's tactical situation as typed entity
    /// tokens (self / target / threat-tracks / obstacle-lobes). Deliberately a plain mutable object
    /// with grow-as-needed lists: this is the observation <em>extraction</em>, not a frozen wire
    /// contract. Nearest-K pooling, fixed layouts, serialization, and any network encoding are the
    /// learner's concern and are added when a consumer exists — not here.
    /// </summary>
    public sealed class TacticalObservation
    {
        public float time;
        public SelfToken self;
        public bool hasTarget;
        public TargetToken target;
        public readonly List<ThreatToken> threats = new();
        public readonly List<ObstacleToken> obstacles = new();

        public void Clear()
        {
            hasTarget = false;
            threats.Clear();
            obstacles.Clear();
        }
    }
}
