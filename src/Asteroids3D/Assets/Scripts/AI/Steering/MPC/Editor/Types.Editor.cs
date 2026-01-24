#if UNITY_EDITOR
namespace AI.Steering.MPC
{
    public struct CostBreakdown
    {
        public float pos;
        public float vel;
        public float heading;
        public float facing;
        public float yawRate;
        public float obstacle;
        public float effort;
        public float smoothness;
        public float total;

        public void Add(CostBreakdown other)
        {
            pos += other.pos;
            vel += other.vel;
            heading += other.heading;
            facing += other.facing;
            yawRate += other.yawRate;
            obstacle += other.obstacle;
            effort += other.effort;
            smoothness += other.smoothness;
            total += other.total;
        }
    }
}
#endif
