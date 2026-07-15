using System.Collections;
using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Hand-placed procedural producer (template + parameters, creates its own instances — unlike adopt content); subclasses implement <see cref="Produce"/>/<see cref="OnTeardown"/>, while <see cref="Build"/>/<see cref="Teardown"/> own the sector lifecycle and the activation-mode gate.</summary>
    public abstract class SectorSpawner : MonoBehaviour
    {
        public enum ActivationMode
        {
            Eager,
            Gated,
        }

        [Tooltip("Eager produces at Build; Gated stays dormant and produces exactly once when its activation signal first goes true.")]
        [SerializeField] private ActivationMode activationMode = ActivationMode.Eager;

        [SerializeField] private SignalPort activationSignal;

        private SectorBuildContext ctx;
        private SectorEventBus bus;

        public ActivationMode Mode => activationMode;

        public SignalPort ActivationSignal => activationSignal;

        /// <summary>Ships produced during the last <see cref="Produce"/> run; non-ship producers leave this empty.</summary>
        public IReadOnlyList<Ship> Spawned { get; protected set; } = System.Array.Empty<Ship>();

        /// <summary>Sealed lifecycle entry: produces now, or arms the activation signal and produces on its latch.</summary>
        public IEnumerator Build(SectorBuildContext buildCtx)
        {
            ctx = buildCtx;

            if (activationMode == ActivationMode.Eager)
            {
                var produce = Produce(ctx);
                while (produce.MoveNext()) yield return produce.Current;
                yield break;
            }

            if (!SignalPortGuards.ValidPortRef(this, activationSignal, "activation", ctx.Sector)) yield break;
            bus = ctx.Bus;
            if (bus == null)
            {
                Debug.LogError($"{GetType().Name} on '{name}' is signal-gated but has no bus — inert.", this);
                yield break;
            }
            bus.Changed += OnBusChanged;
            if (bus.Get(activationSignal)) ProduceNow();
        }

        /// <summary>Sealed lifecycle exit: disarms the signal gate, then runs <see cref="OnTeardown"/>.</summary>
        public IEnumerator Teardown(SectorBuildContext teardownCtx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            var teardown = OnTeardown(teardownCtx);
            while (teardown.MoveNext()) yield return teardown.Current;
        }

        public void ConfigureEager()
        {
            activationMode = ActivationMode.Eager;
            activationSignal = null;
        }

        public void ConfigureGated(SignalPort signal)
        {
            activationMode = ActivationMode.Gated;
            activationSignal = signal;
        }

        /// <summary>Instantiate this spawner's content. Populate <see cref="Spawned"/>.</summary>
        protected abstract IEnumerator Produce(SectorBuildContext ctx);

        /// <summary>Tear down produced instances; the default despawns every ship in <see cref="Spawned"/> so restart starts from a clean unit set (the player is never a spawner product).</summary>
        protected virtual IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            foreach (var ship in Spawned)
                if (ship) ctx.Services.UnitService.DespawnShip(ship);
            Spawned = System.Array.Empty<Ship>();
            yield break;
        }

        private void OnBusChanged(SignalPort port)
        {
            if (port != activationSignal || !bus.Get(activationSignal)) return;
            ProduceNow();
        }

        // Gated production drains synchronously — it must land in the same frame as the latch (parity with the eager path's same-Setup production).
        private void ProduceNow()
        {
            bus.Changed -= OnBusChanged;
            var produce = Produce(ctx);
            while (produce.MoveNext()) { }
        }

        /// <summary>Editor-only preview hook; concrete spawners draw placement gizmos here.</summary>
        protected virtual void OnDrawGizmos() { }
    }
}
