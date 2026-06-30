using System;
using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// A polymorphic, hand-placed behavior unit dropped on a sector (root component) and reconciled
    /// into the sector's <c>modules[]</c> manifest by the same Sync crawl as spawners. Modules carry
    /// gameplay logic / control behaviors (e.g. spectator camera, encounter sequence). Pure
    /// presentation that can subscribe to a service channel is NOT a module.
    ///
    /// A module runs its <see cref="Setup"/> after the sector's content (adopt + spawners +
    /// <c>OnAfterContent</c>) and its <see cref="Teardown"/> before the sector's
    /// <c>OnBeforeTeardown</c>, in reverse list order. A module may end the sector by raising
    /// <see cref="SectorEndRequested"/> via <see cref="RequestSectorEnd"/>; the base sector
    /// auto-subscribes that event to its completion sink. A module that never raises never ends the
    /// sector.
    /// </summary>
    public abstract class SectorModule : MonoBehaviour
    {
        /// <summary>Raised when this module decides the sector should end. Auto-wired by the sector.</summary>
        public event Action<SectorResult> SectorEndRequested;

        /// <summary>Build this module's behavior. Runs after content, in manifest list order.</summary>
        public virtual IEnumerator Setup(SectorBuildContext ctx) { yield break; }

        /// <summary>Tear down this module's behavior. Runs before OnBeforeTeardown, in reverse order.</summary>
        public virtual IEnumerator Teardown(SectorBuildContext ctx) { yield break; }

        /// <summary>Request that the owning sector ends with the given result.</summary>
        protected void RequestSectorEnd(SectorResult result) => SectorEndRequested?.Invoke(result);
    }
}
