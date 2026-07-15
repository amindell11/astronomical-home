using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Activates its own (dormant) GameObject when the referenced port goes true — the actee subscribes; no peer commands it.</summary>
    public class ActivateOnSignal : SectorModule
    {
        [SerializeField] private SignalPort signal;

        private SectorEventBus bus;

        public SignalPort Signal => signal;

        public void Configure(SignalPort signal) => this.signal = signal;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!SignalPortGuards.ValidPortRef(this, signal, "activation", ctx.Sector)) yield break;

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

        private void OnBusChanged(SignalPort changed)
        {
            if (changed == signal && bus.Get(signal)) gameObject.SetActive(true);
        }
    }
}
