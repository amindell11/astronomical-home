using System;
using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Fires exactly once when all terms hold, then publishes latched tokens so rules chain in data.</summary>
    public class ActivationRule : SectorModule
    {
        [SerializeField] private ActivationTerm[] terms = Array.Empty<ActivationTerm>();
        [SerializeField] private string[] publishOnFired = Array.Empty<string>();

        public event Action Fired;

        public bool HasFired => predicate != null && predicate.Satisfied;

        private SectorEventBus bus;
        private ActivationPredicate predicate;
        private float setupTime;

        public void Configure(ActivationTerm[] terms, string[] publishOnFired = null)
        {
            this.terms = terms ?? Array.Empty<ActivationTerm>();
            this.publishOnFired = publishOnFired ?? Array.Empty<string>();
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            bus = ctx.Bus;
            predicate = new ActivationPredicate(terms);
            setupTime = Time.time;
            if (bus != null) bus.Changed += OnBusChanged;
            EvaluateNow();
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            predicate = null;
            yield break;
        }

        private void Update()
        {
            if (predicate == null || predicate.Satisfied || !predicate.HasTimeTerm) return;
            EvaluateNow();
        }

        private void OnBusChanged(string token) => EvaluateNow();

        private void EvaluateNow()
        {
            if (!predicate.Evaluate(bus, Time.time - setupTime)) return;
            foreach (var token in publishOnFired)
                bus?.Latch(token);
            Fired?.Invoke();
        }
    }
}
