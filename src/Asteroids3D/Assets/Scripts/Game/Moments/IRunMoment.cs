using System.Collections.Generic;

namespace Game.Moments
{
    public interface IRunMoment
    {
        /// <summary>The id of the line slot that speaks for this moment.</summary>
        string Slot { get; }

        /// <summary>Named facts a line quotes as <c>{name}</c>.</summary>
        IReadOnlyDictionary<string, string> Facts { get; }
    }
}
