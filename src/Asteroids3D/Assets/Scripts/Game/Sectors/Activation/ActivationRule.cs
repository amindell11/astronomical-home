using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Fires exactly once when all terms hold, in causal order: OnFired (own effect) → Fired event → latch the fired output, so downstream rules run strictly after this rule's effect.</summary>
    public class ActivationRule : SectorModule, ISignalSource
    {
        public const string OutputFired = "fired";

        [SerializeField] private ActivationTerm[] terms = Array.Empty<ActivationTerm>();

        [Tooltip("Delay between the terms first holding (latched — leaving the area does not cancel) and the fire sequence running. Teardown or bus freeze cancels a pending fire.")]
        [SerializeField] private float fireDelaySeconds;

        public event Action Fired;

        public bool HasFired { get; private set; }

        public IReadOnlyList<ActivationTerm> Terms => terms;

        public virtual IEnumerable<SignalOutput> Outputs
        {
            get { yield return new SignalOutput(OutputFired, SignalKind.Latch); }
        }

        private SectorEventBus bus;
        private ActivationPredicate predicate;
        private float setupTime;
        private bool firePending;
        private float fireAtTime;

        public void Configure(ActivationTerm[] terms, float fireDelaySeconds = 0f)
        {
            this.terms = terms ?? Array.Empty<ActivationTerm>();
            this.fireDelaySeconds = fireDelaySeconds;
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!TermsValid(ctx.Sector)) yield break;
            if (fireDelaySeconds > 0f && !gameObject.activeInHierarchy)
            {
                Debug.LogError($"{GetType().Name} on '{name}' has a delayed fire but its GameObject is inactive — inert.", this);
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

        /// <summary>Effect seam: a subclass IS the effect (no post-Setup binding race); runs before the Fired event and output latch.</summary>
        protected virtual void OnFired() { }

        private bool TermsValid(Sector sector)
        {
            foreach (var term in terms)
                if (term.kind == ActivationTerm.TermKind.Signal &&
                    !SignalGuards.ValidRef(this, term.signal, "term", sector))
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

        private void OnBusChanged(SignalRef signal) => EvaluateNow();

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
            bus?.Latch(new SignalRef(this, OutputFired));
        }
    }
}
