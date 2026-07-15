using System;

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
        public SignalPort signal;
        public float timeSeconds;

        public static ActivationTerm Signal(SignalPort port) =>
            new ActivationTerm { kind = TermKind.Signal, signal = port };

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
