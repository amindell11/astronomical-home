using System;
using System.Collections.Generic;

namespace Game.Sectors.Activation
{
    /// <summary>Standing AND over terms — reads current bus levels each evaluation (never edge-subscribes) and latches on first full satisfaction.</summary>
    public class ActivationPredicate
    {
        private readonly IReadOnlyList<ActivationTerm> terms;

        public ActivationPredicate(IReadOnlyList<ActivationTerm> terms)
        {
            this.terms = terms ?? Array.Empty<ActivationTerm>();
            foreach (var term in this.terms)
                if (term.kind == ActivationTerm.TermKind.Time) HasTimeTerm = true;
        }

        public bool Satisfied { get; private set; }

        public bool HasTimeTerm { get; }

        /// <summary>Returns true only on the evaluation that latches; false forever after.</summary>
        public bool Evaluate(SectorEventBus bus, float elapsedSeconds)
        {
            if (Satisfied) return false;
            foreach (var term in terms)
                if (!term.IsSatisfied(bus, elapsedSeconds)) return false;
            Satisfied = true;
            return true;
        }
    }
}
