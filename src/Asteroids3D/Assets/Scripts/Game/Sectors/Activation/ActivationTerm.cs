using System;
using UnityEngine;

namespace Game.Sectors
{
    [Serializable]
    public class ActivationTerm
    {
        public enum TermKind
        {
            Signal,
            Time,
        }

        public TermKind kind;
        public SignalRef signal;
        public float timeSeconds;

        public static ActivationTerm Signal(SignalRef signal) =>
            new ActivationTerm { kind = TermKind.Signal, signal = signal };

        public static ActivationTerm Signal(Component source, string output) =>
            Signal(new SignalRef(source, output));

        public static ActivationTerm Time(float seconds) =>
            new ActivationTerm { kind = TermKind.Time, timeSeconds = seconds };

        public bool IsSatisfied(SectorEventBus bus, float elapsedSeconds) => kind switch
        {
            TermKind.Signal => bus != null && bus.Get(signal),
            TermKind.Time => elapsedSeconds >= timeSeconds,
            _ => false,
        };
    }
}
