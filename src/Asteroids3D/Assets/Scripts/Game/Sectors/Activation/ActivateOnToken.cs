using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Activates its own (dormant) GameObject when the named bus token goes true — the actee subscribes; no peer commands it.</summary>
    public class ActivateOnToken : SectorModule
    {
        [SerializeField] private string token;

        private SectorEventBus bus;

        public void Configure(string token) => this.token = token;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.LogError($"ActivateOnToken on '{name}' has a blank token — module is inert.", this);
                yield break;
            }

            bus = ctx.Bus;
            if (bus == null) yield break;
            bus.Changed += OnBusChanged;
            if (bus.Get(token)) gameObject.SetActive(true);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            gameObject.SetActive(false);
            yield break;
        }

        private void OnBusChanged(string changed)
        {
            if (changed == token && bus.Get(token)) gameObject.SetActive(true);
        }
    }
}
