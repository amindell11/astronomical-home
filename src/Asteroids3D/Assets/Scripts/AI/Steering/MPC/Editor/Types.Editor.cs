#if UNITY_EDITOR
using UnityEngine.Profiling;

namespace Movement.MPC
{
    public struct CostBreakdown
    {
        public float pos;
        public float vel;
        public float heading;
        public float facing;
        public float yawRate;
        public float obstacle;
        public float los;
        public float exposure;
        public float tangential;
        public float momentum;
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
            los += other.los;
            exposure += other.exposure;
            tangential += other.tangential;
            momentum += other.momentum;
            effort += other.effort;
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
