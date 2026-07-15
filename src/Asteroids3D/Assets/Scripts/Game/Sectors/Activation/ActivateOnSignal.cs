using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Activates its own (dormant) GameObject when the referenced signal goes true — the actee subscribes; no peer commands it.</summary>
    public class ActivateOnSignal : SectorModule
    {
        [SerializeField] private SignalRef signal;

        private SectorEventBus bus;

        public SignalRef Signal => signal;

        public void Configure(SignalRef signal) => this.signal = signal;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!SignalGuards.ValidRef(this, signal, "activation", ctx.Sector)) yield break;

            bus = ctx.Bus;
            if (bus == null) yield break;
            bus.Changed += OnBusChanged;
            OnBusChanged(signal);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            gameObject.SetActive(false);
            yield break;
        }

        private void OnBusChanged(SignalRef changed)
        {
            if (changed.Equals(signal) && bus.Get(signal)) gameObject.SetActive(true);
        }
    }
}
