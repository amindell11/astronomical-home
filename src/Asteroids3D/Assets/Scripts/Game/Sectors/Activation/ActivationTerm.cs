using System;

namespace Game.Sectors.Activation
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
        public string signalToken;
        public float timeSeconds;

        public static ActivationTerm Signal(string token) =>
            new ActivationTerm { kind = TermKind.Signal, signalToken = token };

        public static ActivationTerm Time(float seconds) =>
            new ActivationTerm { kind = TermKind.Time, timeSeconds = seconds };

        public bool IsSatisfied(SectorEventBus bus, float elapsedSeconds) => kind switch
        {
            TermKind.Signal => bus != null && bus.Get(signalToken),
            TermKind.Time => elapsedSeconds >= timeSeconds,
            _ => false,
        };
    }
}
