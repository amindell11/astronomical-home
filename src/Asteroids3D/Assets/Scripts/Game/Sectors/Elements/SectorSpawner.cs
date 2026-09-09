using System.Collections;
using System.Collections.Generic;
using Ships;
using UnityEngine;
using Game.Sectors.Activation;

namespace Game.Sectors.Elements
{
    /// <summary>Hand-placed procedural producer (template + parameters, creates its own instances — unlike adopt content); subclasses implement <see cref="Produce"/>/<see cref="OnTeardown"/>, while <see cref="Build"/>/<see cref="Teardown"/> own the sector lifecycle and the optional activation-token gate.</summary>
    public abstract class SectorSpawner : MonoBehaviour
    {
        [Tooltip("Optional bus token gating production: empty = produce at Build; set = stay dormant and produce exactly once when the token goes true.")]
        [SerializeField] private string activationToken;

        private SectorBuildContext ctx;
        private SectorEventBus bus;

        /// <summary>Ships produced during the last <see cref="Produce"/> run; non-ship producers leave this empty.</summary>
        public IReadOnlyList<Ship> Spawned { get; protected set; } = System.Array.Empty<Ship>();

        /// <summary>Sealed lifecycle entry: produces now, or arms the activation token and produces on its latch.</summary>
        public IEnumerator Build(SectorBuildContext buildCtx)
        {
            ctx = buildCtx;

            if (string.IsNullOrWhiteSpace(activationToken))
            {
                var produce = Produce(ctx);
                while (produce.MoveNext()) yield return produce.Current;
                yield break;
            }

            bus = ctx.Bus;
            if (bus == null)
            {
                Debug.LogError($"SectorSpawner on '{name}' has an activation token but no bus — spawner is inert.", this);
                yield break;
            }
            bus.Changed += OnBusChanged;
            if (bus.Get(activationToken)) ProduceNow();
        }

        /// <summary>Sealed lifecycle exit: disarms the token gate, then runs <see cref="OnTeardown"/>.</summary>
        public IEnumerator Teardown(SectorBuildContext teardownCtx)
        {
            if (bus != null) bus.Changed -= OnBusChanged;
            bus = null;
            var teardown = OnTeardown(teardownCtx);
            while (teardown.MoveNext()) yield return teardown.Current;
        }

        public void Configure(string activationToken) => this.activationToken = activationToken;

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

        private void OnBusChanged(string token)
        {
            if (token != activationToken || !bus.Get(activationToken)) return;
            ProduceNow();
        }

        // Token-gated production drains synchronously — it must land in the same frame as the latch (parity with the eager path's same-Setup production).
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
