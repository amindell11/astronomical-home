using System;
using Ships;

namespace Substrate.Sectors.Elements.Phases
{
    public interface IWaveSignal
    {
        /// <summary>A wave was sent: the zero-based sector index and the rival leading it.</summary>
        event Action<int, Ship> WaveSent;

        /// <summary>A wave's rival died while its minions still fly: the zero-based sector index.</summary>
        event Action<int> RivalFell;
    }
}
