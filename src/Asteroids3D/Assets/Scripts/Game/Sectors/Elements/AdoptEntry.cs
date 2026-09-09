using System;
using UnityEngine;

namespace Game.Sectors.Elements
{
    /// <summary>
    /// A baked, serialized reference to a hand-placed content child of a sector prefab that is
    /// wired into services in place at load (the object you placed IS the runtime object).
    /// The set of adoptable target types is recognised by type-dispatch in the adopter
    /// (<c>Ship</c> / <c>WorldRoot</c> / <c>UpdatingAsteroidField</c>); see <see cref="Sector"/>.
    /// Annotations are authored in the sector's inspector list, never on a per-child wrapper.
    /// Pose comes from the child's transform.
    /// </summary>
    [Serializable]
    public struct AdoptEntry
    {
        [Tooltip("Placed child to adopt: a Ship, WorldRoot, or UpdatingAsteroidField.")]
        public Component target;

        [Tooltip("Team number applied to an adopted Ship (ignored for non-ship targets).")]
        public int team;

        [Tooltip("If false, the adopted object is deactivated immediately after adoption (e.g. a chaser revealed later).")]
        public bool startActive;

        [Tooltip("Optional respawn rule for an adopted Ship (origin None = no respawn). Ignored for non-ship targets.")]
        public RespawnPolicy respawn;
    }
}
