#if UNITY_EDITOR
using UnityEngine.Profiling;

namespace Movement.MPC
{
    public struct CostBreakdown
    {
        public float pos;
        public float vel;
        public float closing;
        public float heading;
        public float velocityTrack;
        public float facing;
        public float yawRate;
        public float obstacle;
        public float collision;
        public float los;
        public float exposure;
        public float tangential;
        public float missDistance;
        public float momentum;
        public float effort;
        public float boostEffort;
        public float smoothness;
        public float total;

        public void Add(CostBreakdown other)
        {
            pos += other.pos;
            vel += other.vel;
            closing += other.closing;
            heading += other.heading;
            velocityTrack += other.velocityTrack;
            facing += other.facing;
            yawRate += other.yawRate;
            obstacle += other.obstacle;
            collision += other.collision;
            los += other.los;
            exposure += other.exposure;
            tangential += other.tangential;
            missDistance += other.missDistance;
            momentum += other.momentum;
            effort += other.effort;
            boostEffort += other.boostEffort;
            smoothness += other.smoothness;
            total += other.total;
        }
    }

    internal readonly partial struct EditorProfilingScope
    {
        static partial void BeginSample(string sampleName) => Profiler.BeginSample(sampleName);

        static partial void EndSample() => Profiler.EndSample();
    }
}
#endif
