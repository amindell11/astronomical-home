using System;
using System.Collections.Generic;

namespace Game.Moments
{
    /// <summary>
    /// The moments of one run, in the order they were raised. The game host creates one when a run begins
    /// and hands it down through Initialize or a build context; it is never a static. Moments are data and
    /// listeners own any text, so a new moment type is one <see cref="IRunMoment"/> class plus its line slot
    /// on the line sheet.
    /// https://github.com/amindell11/astronomical-home/issues/1321#issuecomment-6618820730
    /// </summary>
    public sealed class RunMomentStream
    {
        private readonly List<IRunMoment> moments = new();

        public event Action<IRunMoment> Raised;

        public IReadOnlyList<IRunMoment> Moments => moments;

        public void Raise(IRunMoment moment)
        {
            if (moment == null) throw new ArgumentNullException(nameof(moment));
            moments.Add(moment);
            Raised?.Invoke(moment);
        }
    }
}
