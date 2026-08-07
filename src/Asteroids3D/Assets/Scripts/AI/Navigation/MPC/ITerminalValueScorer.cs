using Unity.Collections;

namespace Movement.MPC
{
    public interface ITerminalValueScorer
    {
        void Score(NativeArray<State> terminalStates, NativeArray<float> values, int count);
    }
}
