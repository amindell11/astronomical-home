using System.Collections;
using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// A polymorphic, hand-placed child GameObject that procedurally produces content at sector
    /// setup. Production may be one-shot (e.g. a ring of AI ships, all created up front) or
    /// continuous and internally managed (e.g. an asteroid field that keeps spawning over its
    /// lifetime); either way <see cref="Build"/>/<see cref="Teardown"/> bracket its lifecycle.
    /// Unlike adopt content — where the placed object is the runtime object — a spawner holds a
    /// template + parameters and creates its own instances. Creating a new kind of spawner =
    /// subclass this. The spawner's transform is its origin and <see cref="OnDrawGizmos"/> previews
    /// the result in scene view before launch.
    /// </summary>
    public abstract class SectorSpawner : MonoBehaviour
    {
        /// <summary>
        /// Ships produced during the last <see cref="Build"/> call (for injection / wiring). Optional —
        /// non-ship producers (e.g. an asteroid field) leave this empty.
        /// </summary>
        public IReadOnlyList<Ship> Spawned { get; protected set; } = System.Array.Empty<Ship>();

        /// <summary>Instantiate this spawner's content. Populate <see cref="Spawned"/>.</summary>
        public abstract IEnumerator Build(SectorBuildContext ctx);

        /// <summary>Tear down loose instances this spawner owns. Service-owned ships are NOT torn down here.</summary>
        public virtual IEnumerator Teardown(SectorBuildContext ctx) { yield break; }

        /// <summary>Editor-only preview hook; concrete spawners draw placement gizmos here.</summary>
        protected virtual void OnDrawGizmos() { }
    }
}
