using System;
using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Fires exactly once when all terms hold, in causal order: OnFired (own effect) → Fired event → publish latched tokens, so downstream rules run strictly after this rule's effect.</summary>
    public class ActivationRule : SectorModule
    {
        [SerializeField] private ActivationTerm[] terms = Array.Empty<ActivationTerm>();
        [SerializeField] private string[] publishOnFired = Array.Empty<string>();

        [Tooltip("Delay between the terms first holding (latched — leaving the area does not cancel) and the fire sequence running. Teardown or bus freeze cancels a pending fire.")]
        [SerializeField] private float fireDelaySeconds;

        public event Action Fired;

        public bool HasFired { get; private set; }

        private SectorEventBus bus;
        private ActivationPredicate predicate;
        private float setupTime;
        private bool firePending;
        private float fireAtTime;

        public void Configure(ActivationTerm[] terms, string[] publishOnFired = null, float fireDelaySeconds = 0f)
        {
            this.terms = terms ?? Array.Empty<ActivationTerm>();
            this.publishOnFired = publishOnFired ?? Array.Empty<string>();
            this.fireDelaySeconds = fireDelaySeconds;
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!HasValidTokens())
            {
                Debug.LogError($"ActivationRule on '{name}' has a blank signal token — rule is inert.", this);
                yield break;
            }

            bus = ctx.Bus;
            predicate = new ActivationPredicate(terms);
            setupTime = Time.time;
            HasFired = false;
            firePending = false;
            if (bus != null) bus.Changed += OnBusChanged;
            EvaluateNow();
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            predicate = null;
            firePending = false;
            yield break;
        }

        /// <summary>Effect seam: a subclass IS the effect (no post-Setup binding race); runs before the Fired event and token publication.</summary>
        protected virtual void OnFired() { }

        private bool HasValidTokens()
        {
            foreach (var term in terms)
                if (term.kind == ActivationTerm.TermKind.Signal && string.IsNullOrWhiteSpace(term.signalToken))
                    return false;
            foreach (var token in publishOnFired)
                if (string.IsNullOrWhiteSpace(token))
                    return false;
            return true;
        }

        private void Update()
        {
            if (firePending)
            {
                if (bus == null || bus.Frozen) { firePending = false; return; }
                if (Time.time >= fireAtTime) Fire();
                return;
            }
            if (predicate == null || predicate.Satisfied || !predicate.HasTimeTerm) return;
            EvaluateNow();
        }

        private void OnBusChanged(string token) => EvaluateNow();

        private void EvaluateNow()
        {
            if (HasFired || firePending) return;
            if (bus != null && bus.Frozen) return;
            if (!predicate.Evaluate(bus, Time.time - setupTime)) return;
            if (fireDelaySeconds > 0f)
            {
                firePending = true;
                fireAtTime = Time.time + fireDelaySeconds;
                return;
            }
            Fire();
        }

        private void Fire()
        {
            firePending = false;
            HasFired = true;
            OnFired();
            Fired?.Invoke();
            foreach (var token in publishOnFired)
                bus?.Latch(token);
        }
    }
}
