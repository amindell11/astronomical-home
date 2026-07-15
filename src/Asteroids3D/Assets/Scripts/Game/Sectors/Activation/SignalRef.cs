using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Sectors
{
    public enum SignalKind
    {
        Level,
        Latch,
    }

    public readonly struct SignalOutput
    {
        public readonly string Id;
        public readonly SignalKind Kind;

        public SignalOutput(string id, SignalKind kind)
        {
            Id = id;
            Kind = kind;
        }
    }

    /// <summary>A publisher declares its signal outputs in code — there is no authored name surface, so a consumer can never reference a signal no publisher owns.</summary>
    public interface ISignalSource
    {
        IEnumerable<SignalOutput> Outputs { get; }
    }

    [Serializable]
    public struct SignalRef : IEquatable<SignalRef>
    {
        public Component source;
        public string output;

        public SignalRef(Component source, string output)
        {
            this.source = source;
            this.output = output;
        }

        public bool IsAssigned => source != null && !string.IsNullOrEmpty(output);

        public bool Equals(SignalRef other) => source == other.source && output == other.output;

        public override bool Equals(object obj) => obj is SignalRef other && Equals(other);

        public override int GetHashCode() =>
            (source ? source.GetInstanceID() : 0) * 397 ^ (output?.GetHashCode() ?? 0);
    }

    internal static class SignalGuards
    {
        public static bool ValidRef(Component owner, SignalRef signal, string role, Sector sector)
        {
            if (!signal.IsAssigned)
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' has an unassigned {role} signal — inert.", owner);
                return false;
            }
            if (sector && !signal.source.transform.IsChildOf(sector.transform))
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' references {role} signal '{signal.output}' outside its own sector — inert.", owner);
                return false;
            }
            if (!Declares(signal.source, signal.output))
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' references {role} output '{signal.output}' that {signal.source.GetType().Name} '{signal.source.name}' does not declare — inert.", owner);
                return false;
            }
            return true;
        }

        public static bool Declares(Component source, string output)
        {
            if (source is not ISignalSource publisher) return false;
            foreach (var declared in publisher.Outputs)
                if (declared.Id == output)
                    return true;
            return false;
        }
    }
}
