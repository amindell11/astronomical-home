using System;

namespace Substrate.Sectors.Elements.Phases
{
    public interface IWaveSignal
    {
        /// <summary>A wave was sent: the zero-based sector index.</summary>
        event Action<int> WaveSent;
    }
}
