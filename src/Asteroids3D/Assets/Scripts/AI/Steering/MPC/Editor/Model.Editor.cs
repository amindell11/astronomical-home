#if UNITY_EDITOR
using UnityEngine.Profiling;

namespace Movement.MPC
{
    public static partial class Model
    {
        static partial void BeginStepProfiling() => Profiler.BeginSample("MPC.Model.Step");

        static partial void EndStepProfiling() => Profiler.EndSample();
    }
}
#endif
