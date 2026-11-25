using System;
using Ships;
using Ships.Command;

namespace AI.Context
{
    /// <summary>
    /// The world model the AI reasons over for a single tick: the live views
    /// (<see cref="Self"/>, <see cref="Combat"/>) over the sensing tier
    /// (<see cref="Scout"/>), plus the distance its primary weapon asks it to hold
    /// (<see cref="HoldRange"/>), how long since the ship was last hit
    /// (<see cref="SecondsSinceHit"/>), and the <see cref="Leader"/> and <see cref="Place"/>
    /// a minion was given. Holds no actuators (navigator/gunner) — those are
    /// owned by the host that acts on the context.
    /// </summary>
    [Serializable]
    public class AIContext
    {
        public IShipStatus Self { get; private set; }
        public EnemyTracker Combat { get; private set; }
        public Scout Scout { get; private set; }

        public float? HoldRange { get; private set; }

        public float SecondsSinceHit { get; private set; } = float.PositiveInfinity;

        public Ship Leader { get; private set; }
        public int Place { get; private set; }

        private float lastHealthPct;
        private float lastShieldPct;

        public AIContext(IShipStatus self, Scout scout, float? holdRange,
            float combatExitDelay = 3f)
        {
            if (self == null || scout == null) return;

            Self = self;
            Scout = scout;
            HoldRange = holdRange;
            Combat = new EnemyTracker(scout, combatExitDelay);
            lastHealthPct = self.HealthPct;
            lastShieldPct = self.ShieldPct;
        }

        public void Update(float deltaTime)
        {
            Combat.Update(deltaTime);
            var health = Self.HealthPct;
            var shield = Self.ShieldPct;
            SecondsSinceHit = health < lastHealthPct || shield < lastShieldPct ? 0f : SecondsSinceHit + deltaTime;
            lastHealthPct = health;
            lastShieldPct = shield;
        }

        internal void FollowLeader(Ship leader, int place)
        {
            if (leader == null) throw new ArgumentNullException(nameof(leader));
            Leader = leader;
            Place = place;
        }
    }
}
